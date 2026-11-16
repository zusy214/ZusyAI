using System.Text.Json.Serialization;
using ZusyAI.Api.Endpoints;
using ZusyAI.Api.Middleware;
using ZusyAI.Api.Options;
using ZusyAI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ChatExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddPolicy("DesktopClient", policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .Validate(options => IsHttpUrl(options.LocalBaseUrl),
        "Ollama:LocalBaseUrl must be an absolute HTTP or HTTPS URL.")
    .Validate(options => IsHttpUrl(options.CloudBaseUrl),
        "Ollama:CloudBaseUrl must be an absolute HTTP or HTTPS URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.LocalModel),
        "Ollama:LocalModel must not be empty.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.LocalVisionModel),
        "Ollama:LocalVisionModel must not be empty.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CloudModel),
        "Ollama:CloudModel must not be empty.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CloudVisionModel),
        "Ollama:CloudVisionModel must not be empty.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IChatService, OllamaChatService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("DesktopClient");
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapChat();
app.MapLocalModels();

app.Run();

static bool IsHttpUrl(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var address) &&
    (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
