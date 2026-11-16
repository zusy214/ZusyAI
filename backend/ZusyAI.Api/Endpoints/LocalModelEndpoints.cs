using Microsoft.AspNetCore.Http.HttpResults;
using ZusyAI.Api.Contracts;
using ZusyAI.Api.Services;

namespace ZusyAI.Api.Endpoints;

public static class LocalModelEndpoints
{
    public static void MapLocalModels(this WebApplication app)
    {
        app.MapGet("/api/models/local", async Task<Ok<LocalModelsResponse>> (
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var response = await chatService.GetLocalModelsAsync(cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("GetLocalModels")
        .WithSummary("List installed local Ollama models")
        .WithDescription("Returns the models available from the configured local Ollama service and the configured text and vision defaults.")
        .Produces<LocalModelsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
