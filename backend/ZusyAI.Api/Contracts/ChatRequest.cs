using System.ComponentModel.DataAnnotations;

namespace ZusyAI.Api.Contracts;

/// <summary>A conversation sent to the selected Ollama model.</summary>
public sealed record ChatRequest
{
    /// <summary>Conversation messages in chronological order.</summary>
    [Required, MinLength(1), MaxLength(40)]
    public required IReadOnlyList<ChatMessageRequest> Messages { get; init; }

    /// <summary>Whether to use local Ollama or Ollama Cloud.</summary>
    public ChatProvider Provider { get; init; } = ChatProvider.Local;

    /// <summary>Whether to request the model's reasoning mode when supported.</summary>
    public bool Think { get; init; }

    /// <summary>Optional local text model selected from the installed Ollama models.</summary>
    [StringLength(200, MinimumLength = 1)]
    public string? Model { get; init; }

    /// <summary>Optional local-only memory and document context for the model.</summary>
    [StringLength(8000)]
    public string? LocalContext { get; init; }
}
