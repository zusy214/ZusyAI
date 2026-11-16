using Microsoft.AspNetCore.Http.HttpResults;
using ZusyAI.Api.Contracts;
using ZusyAI.Api.Services;

namespace ZusyAI.Api.Endpoints;

public static class ChatEndpoints
{
    private const int MaximumImagesPerRequest = 5;
    private const int MaximumImageBytes = 3 * 1024 * 1024;
    private const int MaximumDocumentsPerRequest = 10;
    private const int MaximumDocumentCharactersPerRequest = 100_000;

    public static void MapChat(this WebApplication app)
    {
        app.MapPost("/api/chat", async Task<Results<Ok<ChatResponse>, ValidationProblem>> (
            ChatRequest request,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var response = await chatService.GetReplyAsync(request, cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("Chat")
        .WithSummary("Send a conversation to an Ollama model")
        .WithDescription("Forwards conversation history, optional user attachments, and an optional installed local text-model selection to the selected Ollama provider.")
        .Produces<ChatResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static Dictionary<string, string[]> Validate(ChatRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var imageCount = 0;
        var documentCount = 0;
        var documentCharacters = 0;

        if (request.Messages is null || request.Messages.Count == 0 || request.Messages.Count > 40)
        {
            errors["messages"] = ["Provide between 1 and 40 messages."];
            return errors;
        }

        for (var index = 0; index < request.Messages.Count; index++)
        {
            var message = request.Messages[index];
            if (message is null)
            {
                errors[$"messages[{index}]"] = ["Message must not be null."];
                continue;
            }

            if (!Enum.IsDefined(typeof(ChatRole), message.Role))
            {
                errors[$"messages[{index}].role"] = ["Role must be User or Assistant."];
            }

            if (string.IsNullOrWhiteSpace(message.Content) || message.Content.Length > 12000)
            {
                errors[$"messages[{index}].content"] = ["Content must contain 1 to 12000 characters."];
            }

            var hasAttachments = (message.Images?.Count ?? 0) > 0 ||
                                 (message.Documents?.Count ?? 0) > 0;
            if (hasAttachments && message.Role != ChatRole.User)
            {
                errors[$"messages[{index}].attachments"] = ["Attachments are only allowed on user messages."];
            }

            if (message.Images is { Count: > 0 })
            {
                if (message.Images.Count > MaximumImagesPerRequest)
                {
                    errors[$"messages[{index}].images"] = [$"A message can include up to {MaximumImagesPerRequest} images."];
                }

                foreach (var image in message.Images)
                {
                    imageCount++;
                    if (imageCount > MaximumImagesPerRequest ||
                        !IsValidImage(image))
                    {
                        errors[$"messages[{index}].images"] =
                            [$"Provide no more than {MaximumImagesPerRequest} valid images of at most 3 MB each per request."];
                        break;
                    }
                }
            }

            if (message.Documents is { Count: > 0 })
            {
                if (message.Documents.Count > 4)
                {
                    errors[$"messages[{index}].documents"] = ["A message can include up to 4 documents."];
                }

                foreach (var document in message.Documents)
                {
                    documentCount++;
                    if (documentCount > MaximumDocumentsPerRequest)
                    {
                        errors[$"messages[{index}].documents"] =
                            [$"A request can include up to {MaximumDocumentsPerRequest} documents."];
                        break;
                    }

                    if (document is null ||
                        string.IsNullOrWhiteSpace(document.Name) ||
                        document.Name.Length > 200 ||
                        string.IsNullOrWhiteSpace(document.Text) ||
                        document.Text.Length > 10000)
                    {
                        errors[$"messages[{index}].documents"] =
                            ["Each document must have a filename and contain 1 to 10000 extracted characters."];
                        continue;
                    }

                    documentCharacters += document.Text.Length;
                    if (documentCharacters > MaximumDocumentCharactersPerRequest)
                    {
                        errors[$"messages[{index}].documents"] =
                            [$"Extracted document text cannot exceed {MaximumDocumentCharactersPerRequest} characters per request."];
                        break;
                    }
                }
            }
        }

        if (!Enum.IsDefined(typeof(ChatProvider), request.Provider))
        {
            errors["provider"] = ["Provider must be Local or Cloud."];
        }

        if (request.Model is not null &&
            (string.IsNullOrWhiteSpace(request.Model) || request.Model.Length > 200))
        {
            errors["model"] = ["Model must contain 1 to 200 non-whitespace characters."];
        }

        if (request.Provider == ChatProvider.Cloud &&
            !string.IsNullOrWhiteSpace(request.Model))
        {
            errors["model"] = ["A local model can only be selected with the Local provider."];
        }

        if (request.LocalContext is { Length: > 8000 })
        {
            errors["localContext"] = ["Local context must not exceed 8000 characters."];
        }

        if (request.Provider == ChatProvider.Cloud &&
            !string.IsNullOrWhiteSpace(request.LocalContext))
        {
            errors["localContext"] = ["Local memory context is only available with the Local provider."];
        }

        return errors;
    }

    private static bool IsValidImage(string? image)
    {
        var maximumBase64Characters = ((MaximumImageBytes + 2) / 3) * 4;
        if (string.IsNullOrWhiteSpace(image) || image.Length > maximumBase64Characters)
        {
            return false;
        }

        var buffer = new byte[MaximumImageBytes];
        return Convert.TryFromBase64String(image, buffer, out var bytesWritten) &&
               bytesWritten <= MaximumImageBytes;
    }
}
