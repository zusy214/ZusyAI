using ZusyAI.Api.Contracts;

namespace ZusyAI.Api.Services;

public interface IChatService
{
    Task<ChatResponse> GetReplyAsync(ChatRequest request, CancellationToken cancellationToken);

    Task<LocalModelsResponse> GetLocalModelsAsync(CancellationToken cancellationToken);
}
