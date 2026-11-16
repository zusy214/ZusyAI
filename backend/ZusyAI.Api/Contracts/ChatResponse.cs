namespace ZusyAI.Api.Contracts;

/// <summary>The assistant response returned by the selected Ollama model.</summary>
public sealed record ChatResponse(
    /// <summary>The assistant's plain-text reply.</summary>
    string Message,
    /// <summary>The model name used to generate this reply.</summary>
    string Model);
