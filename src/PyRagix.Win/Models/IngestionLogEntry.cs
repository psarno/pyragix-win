namespace PyRagix.Win.Models;

public enum LogLevel { Info, Success, Warning, Error }

/// <summary>
/// Represents a single line in the ingestion activity log.
/// </summary>
public sealed class IngestionLogEntry
{
    public required string Message { get; init; }
    public LogLevel Level { get; init; } = LogLevel.Info;
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";
}
