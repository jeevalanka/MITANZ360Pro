using MITANZ360Pro.Web.Services.AI;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiWorkflowResult
{
    public bool Success { get; set; }

    public AiExecutionStatus Status { get; set; } = AiExecutionStatus.Completed;

    public AiTaskType TaskType { get; set; }

    public AiProviderType Provider { get; set; }

    public string Model { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;

    /// <summary>
    /// Parsed structured result from AI
    /// </summary>
    public object? Result { get; set; }

    public List<AiError> Errors { get; set; } = new();

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? CorrelationId { get; set; }

    public string? TraceId { get; set; }

    public long DurationMs { get; set; }

    public long? QueueDurationMs { get; set; }

    public long? ProcessingDurationMs { get; set; }

    public AiTokenUsage? TokenUsage { get; set; }

    /// <summary>
    /// Raw debug trace (only in debug mode)
    /// </summary>
    public string? DebugTrace { get; set; }
}