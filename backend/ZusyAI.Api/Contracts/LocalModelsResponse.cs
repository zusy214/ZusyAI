namespace ZusyAI.Api.Contracts;

/// <summary>Local Ollama models available for selection in the chat UI.</summary>
public sealed record LocalModelsResponse(
    string DefaultModel,
    string VisionModel,
    IReadOnlyList<LocalModelInfo> Models);

/// <summary>Installed local Ollama model and its advertised context window.</summary>
public sealed record LocalModelInfo(string Name, int? ContextLength);
