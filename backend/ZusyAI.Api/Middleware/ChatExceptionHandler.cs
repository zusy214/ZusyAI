using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZusyAI.Api.Contracts;
using ZusyAI.Api.Services;

namespace ZusyAI.Api.Middleware;

public sealed class ChatExceptionHandler(
    ILogger<ChatExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            OllamaUnavailableException unavailable when unavailable.Provider == ChatProvider.Cloud =>
                (StatusCodes.Status503ServiceUnavailable,
                    "Ollama Cloud unavailable",
                    "Check your internet connection and try again."),
            OllamaUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "Local Ollama unavailable",
                    "Start Ollama and make sure it is listening at the configured address."),
            OllamaCloudApiKeyMissingException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "Ollama Cloud API key missing",
                    "Set OLLAMA_API_KEY for the .NET API process, then restart the API."),
            OllamaCloudAuthenticationException =>
                (StatusCodes.Status502BadGateway,
                    "Ollama Cloud authentication failed",
                    "Check that OLLAMA_API_KEY is valid and has access to the selected model."),
            OllamaModelNotFoundException missingModel =>
                (StatusCodes.Status503ServiceUnavailable,
                    "Ollama model not found",
                    missingModel.Provider == ChatProvider.Local
                        ? $"Run 'ollama pull {missingModel.Model}' and try again."
                        : $"The cloud model '{missingModel.Model}' is unavailable to this account. Choose a supported cloud model."),
            OllamaUpstreamException upstream =>
                (StatusCodes.Status502BadGateway,
                    "Ollama request failed",
                    upstream.Provider == ChatProvider.Cloud
                        ? "Ollama Cloud could not complete the request. Check the selected model and cloud usage limits."
                        : "Ollama could not complete the request. Check that the configured model supports the selected options."),
            _ => (0, string.Empty, string.Empty)
        };

        if (statusCode == 0)
        {
            return false;
        }

        logger.LogWarning(exception, "Chat request failed with status {StatusCode}.", statusCode);
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
