namespace PyRagix.Win.Models;

/// <summary>
/// Represents a single message in the chat conversation.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// The raw text content returned by the model, including any &lt;think&gt; blocks.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// True if the message was sent by the user; false for assistant responses.
    /// </summary>
    public bool IsUser { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// The model's extracted chain-of-thought reasoning, or <c>null</c> if absent
    /// or the "Show Thinking Blocks" setting is off.
    /// </summary>
    public string? ThinkingContent { get; init; }

    /// <summary>
    /// The response text with all &lt;think&gt;…&lt;/think&gt; blocks removed.
    /// For user messages this equals <see cref="Content"/>.
    /// </summary>
    public required string DisplayContent { get; init; }

    /// <summary>
    /// True when there is thinking content to show in the collapsible expander.
    /// </summary>
    public bool HasThinking => ThinkingContent is not null;
}
