using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ZusyAI.Api.Contracts;
using ZusyAI.Api.Options;

namespace ZusyAI.Api.Services;

public sealed class OllamaChatService(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    IConfiguration configuration) : IChatService
{
    public async Task<ChatResponse> GetReplyAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var isCloud = request.Provider == ChatProvider.Cloud;
        var includesImages = request.Messages.Any(message => message.Images is { Count: > 0 });
        var providerBaseUrl = isCloud ? settings.CloudBaseUrl : settings.LocalBaseUrl;
        var model = (isCloud, includesImages) switch
        {
            (true, true) => settings.CloudVisionModel,
            (true, false) => settings.CloudModel,
            (false, true) => settings.LocalVisionModel,
            _ => request.Model ?? settings.LocalModel
        };
        var apiKey = isCloud ? configuration["OLLAMA_API_KEY"] : null;

        if (isCloud && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new OllamaCloudApiKeyMissingException();
        }

        if (IsRuntimeModelQuestion(request))
        {
            return new ChatResponse(BuildRuntimeModelAnswer(isCloud, model), model);
        }

        var systemContent = $"""
            Runtime metadata: this reply is generated using Ollama model {JsonSerializer.Serialize(model)}.
            If asked which model is running, report that exact identifier. Do not guess from training data
            or claim a different runtime provider.
            """;
        if (!string.IsNullOrWhiteSpace(request.LocalContext))
        {
            systemContent = $"{systemContent}\n\n{request.LocalContext}";
        }

        var messages = new[]
            {
                new OllamaMessage("system", systemContent, null)
            }
            .Concat(request.Messages.Select(message => new OllamaMessage(
            message.Role.ToString().ToLowerInvariant(),
            BuildMessageContent(message),
            message.Images is { Count: > 0 } ? message.Images : null)));

        var ollamaRequest = new OllamaChatRequest(
            model,
            messages,
            request.Think,
            false);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{providerBaseUrl.TrimEnd('/')}/chat")
        {
            Content = JsonContent.Create(ollamaRequest)
        };

        if (isCloud)
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        try
        {
            using var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new OllamaModelNotFoundException(request.Provider, model);
            }

            if (isCloud && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new OllamaCloudAuthenticationException();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OllamaUpstreamException(request.Provider, (int)response.StatusCode);
            }

            OllamaChatResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new OllamaUpstreamException(
                    request.Provider,
                    (int)HttpStatusCode.BadGateway,
                    exception);
            }

            if (string.IsNullOrWhiteSpace(result?.Message?.Content))
            {
                throw new OllamaUpstreamException(request.Provider, (int)HttpStatusCode.BadGateway);
            }

            var actualModel = string.IsNullOrWhiteSpace(result.Model) ? model : result.Model;
            return new ChatResponse(result.Message.Content, actualModel);
        }
        catch (HttpRequestException exception)
        {
            throw new OllamaUnavailableException(request.Provider, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OllamaUnavailableException(request.Provider, exception);
        }
    }

    public async Task<LocalModelsResponse> GetLocalModelsAsync(
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.LocalBaseUrl.TrimEnd('/')}/tags");

        try
        {
            using var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new OllamaUpstreamException(ChatProvider.Local, (int)response.StatusCode);
            }

            OllamaTagsResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                    timeout.Token);
            }
            catch (JsonException exception)
            {
                throw new OllamaUpstreamException(
                    ChatProvider.Local,
                    (int)HttpStatusCode.BadGateway,
                    exception);
            }

            var models = (result?.Models ?? [])
                .Where(model => !string.IsNullOrWhiteSpace(model.Name))
                .Select(model => new LocalModelInfo(
                    model.Name!,
                    model.Details is { ContextLength: > 0 } details
                        ? details.ContextLength
                        : null))
                .GroupBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new LocalModelsResponse(settings.LocalModel, settings.LocalVisionModel, models);
        }
        catch (HttpRequestException exception)
        {
            throw new OllamaUnavailableException(ChatProvider.Local, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OllamaUnavailableException(ChatProvider.Local, exception);
        }
    }

    private static string BuildMessageContent(ChatMessageRequest message)
    {
        if (message.Documents is not { Count: > 0 })
        {
            return message.Content;
        }

        var documents = message.Documents.Select(document =>
            $"[Attached document: {document.Name}]\n{document.Text}");
        return $"{message.Content}\n\n{string.Join("\n\n", documents)}";
    }

    private static bool IsRuntimeModelQuestion(ChatRequest request)
    {
        var content = request.Messages
            .LastOrDefault(message => message.Role == ChatRole.User)
            ?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var normalized = content.ToLowerInvariant();
        var asksForModel = normalized.Contains("which model") ||
                           normalized.Contains("what model") ||
                           normalized.Contains("model id") ||
                           normalized.Contains("model identifier") ||
                           normalized.Contains("hosts your model") ||
                           normalized.Contains("hosting your model") ||
                           normalized.Contains("runs your model");
        var refersToCurrentRuntime = normalized.Contains("you") ||
                                    normalized.Contains("your model") ||
                                    normalized.Contains("this reply") ||
                                    normalized.Contains("this response") ||
                                    normalized.Contains("this answer") ||
                                    normalized.Contains("current");
        var asksAboutRuntime = normalized.Contains("running") ||
                               normalized.Contains("generating") ||
                               normalized.Contains("host") ||
                               normalized.Contains("serving") ||
                               normalized.Contains("runs your model");
        return asksForModel && refersToCurrentRuntime && asksAboutRuntime;
    }

    private static string BuildRuntimeModelAnswer(bool isCloud, string model)
    {
        var providerDescription = isCloud
            ? "Ollama Cloud hosts it"
            : "it is served by the configured Local Ollama service";
        var developerDescription = model.StartsWith("qwen", StringComparison.OrdinalIgnoreCase)
            ? " The Qwen model family is developed by Alibaba Cloud; that is separate from the runtime host."
            : string.Empty;

        return $"App runtime info: the selected model is `{model}`; {providerDescription}. " +
               $"ZusyAI is the chat app.{developerDescription}";
    }

    private sealed record OllamaChatRequest(
        string Model,
        IEnumerable<OllamaMessage> Messages,
        bool Think,
        bool Stream);

    private sealed record OllamaMessage(
        string Role,
        string Content,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? Images);

    private sealed record OllamaChatResponse(string? Model, OllamaResponseMessage? Message);

    private sealed record OllamaResponseMessage(string? Content);

    private sealed record OllamaTagsResponse(IReadOnlyList<OllamaModelInfo>? Models);

    private sealed record OllamaModelInfo(string? Name, OllamaModelDetails? Details);

    private sealed record OllamaModelDetails(
        [property: JsonPropertyName("context_length")]
        int? ContextLength);
}

public sealed class OllamaUnavailableException(ChatProvider provider, Exception innerException)
    : Exception("The selected Ollama service is unavailable.", innerException)
{
    public ChatProvider Provider { get; } = provider;
}

public sealed class OllamaModelNotFoundException(ChatProvider provider, string model)
    : Exception($"The configured Ollama model '{model}' was not found.")
{
    public ChatProvider Provider { get; } = provider;

    public string Model { get; } = model;
}

public sealed class OllamaCloudApiKeyMissingException()
    : Exception("Ollama Cloud requires an API key.");

public sealed class OllamaCloudAuthenticationException()
    : Exception("Ollama Cloud rejected the configured API key.");

public sealed class OllamaUpstreamException(
    ChatProvider provider,
    int statusCode,
    Exception? innerException = null)
    : Exception($"Ollama returned an unsuccessful response ({statusCode}).", innerException)
{
    public ChatProvider Provider { get; } = provider;

    public int StatusCode { get; } = statusCode;
}
