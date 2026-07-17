namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiExecutionAudit
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public string WorkflowName { get; set; } =
        string.Empty;

    public string Provider { get; set; } =
        string.Empty;

    public string Model { get; set; } =
        string.Empty;

    public bool Success { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public long DurationMs { get; set; }

    public string? ErrorMessage { get; set; }

    public string? CorrelationId { get; set; }
}
