namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntitySequenceService
{
    Task<string> GenerateNextEntityIdAsync(CancellationToken cancellationToken = default);
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
        var page = await _repository.GetPagedAsync(
            new EntityFilter { PageSize = 500, PageNumber = 1 },
            cancellationToken);

        var max = 0;

        foreach (var entity in page.Items)
        {
            if (TryParseSequence(entity.EntityId, out var number) && number > max)
            {
                max = number;
            }
        }

        var next = max + 1;
        var generated = $"ENT-{next:D7}";

        _logger.LogInformation("Generated entity id {EntityId}", generated);
        return generated;
    }

    private static bool TryParseSequence(string entityId, out int number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        var parts = entityId.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return int.TryParse(entityId, out number);
        }

        return int.TryParse(parts[^1], out number);
    }
}
