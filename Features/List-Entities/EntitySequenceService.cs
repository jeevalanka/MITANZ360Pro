namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntitySequenceService
{
    Task<string> GenerateNextEntityIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Student auto-number in display form ST000123 (uses existing sequence scan).</summary>
    Task<string> GenerateNextStudentNumberAsync(CancellationToken cancellationToken = default);
}

public sealed class EntitySequenceService : IEntitySequenceService
{
    private readonly IEntityRepository _repository;
    private readonly ILogger<EntitySequenceService> _logger;

    public EntitySequenceService(
        IEntityRepository repository,
        ILogger<EntitySequenceService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> GenerateNextEntityIdAsync(CancellationToken cancellationToken = default)
    {
        var next = await GetNextSequenceNumberAsync("ENT-", cancellationToken);
        var generated = $"ENT-{next:D7}";
        _logger.LogInformation("Generated entity id {EntityId}", generated);
        return generated;
    }

    public async Task<string> GenerateNextStudentNumberAsync(CancellationToken cancellationToken = default)
    {
        var next = await GetNextSequenceNumberAsync("ST", cancellationToken);
        var generated = $"ST{next:D6}";
        _logger.LogInformation("Generated student number {EntityId}", generated);
        return generated;
    }

    private async Task<int> GetNextSequenceNumberAsync(string prefix, CancellationToken cancellationToken)
    {
        var page = await _repository.GetPagedAsync(
            new EntityFilter { PageSize = 500, PageNumber = 1 },
            cancellationToken);

        var max = 0;

        foreach (var entity in page.Items)
        {
            if (TryParseSequence(entity.EntityId, prefix, out var number) && number > max)
            {
                max = number;
            }
        }

        return max + 1;
    }

    private static bool TryParseSequence(string entityId, string prefix, out int number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        // Student numbers: ST000123 (no hyphen) or ST-000123
        if (prefix.Equals("ST", StringComparison.OrdinalIgnoreCase))
        {
            if (entityId.StartsWith("ST", StringComparison.OrdinalIgnoreCase))
            {
                var digits = entityId[2..].TrimStart('-');
                return int.TryParse(digits, out number);
            }

            return false;
        }

        // Default ENT-####### (and legacy hyphenated ids)
        if (!entityId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !entityId.StartsWith("ENT", StringComparison.OrdinalIgnoreCase))
        {
            // Fall back: any hyphenated suffix for generic ENT generation
            var parts = entityId.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return int.TryParse(parts[^1], out number);
            }

            return int.TryParse(entityId, out number);
        }

        var remainder = entityId;
        if (remainder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            remainder = remainder[prefix.Length..].TrimStart('-');
        }

        return int.TryParse(remainder, out number);
    }
}
