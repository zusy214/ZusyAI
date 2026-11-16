using System.ComponentModel.DataAnnotations;

namespace ZusyAI.Api.Contracts;

/// <summary>Text extracted from a user-attached document.</summary>
public sealed record ChatDocumentRequest
{
    /// <summary>The original document filename.</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>Extracted plain text from the document.</summary>
    [Required, StringLength(10000, MinimumLength = 1)]
    public required string Text { get; init; }
}
