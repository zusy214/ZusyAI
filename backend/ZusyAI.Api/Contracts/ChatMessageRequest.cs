using System.ComponentModel.DataAnnotations;

namespace ZusyAI.Api.Contracts;

/// <summary>A single message included in a chat request.</summary>
public sealed record ChatMessageRequest
{
    /// <summary>Who authored the message.</summary>
    [Required]
    public required ChatRole Role { get; init; }

    /// <summary>The plain-text message content.</summary>
    [Required, StringLength(12000, MinimumLength = 1)]
    public required string Content { get; init; }

    /// <summary>Base64-encoded image attachments for vision-capable models.</summary>
    [MaxLength(5)]
    public IReadOnlyList<string>? Images { get; init; }

    /// <summary>Extracted text from attached documents.</summary>
    [MaxLength(4)]
    public IReadOnlyList<ChatDocumentRequest>? Documents { get; init; }
}
