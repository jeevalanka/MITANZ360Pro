namespace MITANZ360Pro.AI;

public sealed class AiRequestContext
{
    public required string UserId { get; init; }
    public required string Role { get; init; }
    public bool CanUseAI { get; init; }

    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }
}