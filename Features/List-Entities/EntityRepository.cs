using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Common;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityRepository
{
    Task<EntityPagedResult<Entity>> GetPagedAsync(
        EntityFilter filter,
        CancellationToken cancellationToken = default);

    Task<Entity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Entity?> GetByEntityIdAsync(
        string entityId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<Entity> CreateAsync(
        Entity entity,
        CancellationToken cancellationToken = default);

    Task<Entity> UpdateAsync(
        Entity entity,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public sealed class EntityRepository : IEntityRepository
{
    private static readonly string[] SelectFields =
    [
        EntityFields.Id,
        EntityFields.Title,
        EntityFields.EntityId,
        EntityFields.EntityType,
        EntityFields.Status,
        EntityFields.IsActive,
        EntityFields.MetadataJson,
        EntityFields.Created,
        EntityFields.Modified,
        EntityFields.Author,
        EntityFields.Editor
    ];

    private readonly ISharePointListClient _sharePointClient;
    private readonly SharePointOptions _options;
    private readonly ILogger<EntityRepository> _logger;

    public EntityRepository(
        ISharePointListClient sharePointClient,
        IOptions<SharePointOptions> options,
        ILogger<EntityRepository> logger)
    {
        _sharePointClient = sharePointClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.SiteId))
        {
            throw new InvalidOperationException("SharePoint SiteId configuration missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.Lists.Entities))
        {
            throw new InvalidOperationException("SharePoint Lists:Entities configuration missing.");
        }
    }

    public async Task<EntityPagedResult<Entity>> GetPagedAsync(
        EntityFilter filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading Entity page {PageNumber}", filter.PageNumber);

            var oDataFilter = BuildODataFilter(filter);
            var requiresMetadataSearch = RequiresMetadataSearch(filter);

            if (requiresMetadataSearch)
            {
                return await GetPagedWithMetadataSearchAsync(filter, oDataFilter, cancellationToken);
            }

            var totalCount = await _sharePointClient.GetItemCountAsync(
                _options.SiteId,
                _options.Lists.Entities,
                oDataFilter,
                cancellationToken);

            var skip = Math.Max(0, (filter.PageNumber - 1) * filter.PageSize);
            var collected = new List<Entity>();
            string? nextLink = filter.NextLink;
            var skipped = 0;

            while (collected.Count < filter.PageSize)
            {
                var query = new ListQuery
                {
                    PageSize = filter.PageSize,
                    Filter = string.IsNullOrWhiteSpace(nextLink) ? oDataFilter : null,
                    OrderBy = $"fields/{EntityFields.Title}",
                    NextLink = nextLink
                };

                var response = await _sharePointClient.GetItemsAsync(
                    _options.SiteId,
                    _options.Lists.Entities,
                    SelectFields,
                    query,
                    cancellationToken);

                var batch = response.Items
                    .Select(item => Map(item))
                    .Where(x => x != null)
                    .Cast<Entity>()
                    .ToList();

                foreach (var entity in batch)
                {
                    if (skipped < skip)
                    {
                        skipped++;
                        continue;
                    }

                    collected.Add(entity);

                    if (collected.Count >= filter.PageSize)
                    {
                        break;
                    }
                }

                nextLink = response.NextLink;

                if (string.IsNullOrWhiteSpace(nextLink) || batch.Count == 0)
                {
                    break;
                }
            }

            if (totalCount == 0 && collected.Count > 0)
            {
                totalCount = skip + collected.Count + (string.IsNullOrWhiteSpace(nextLink) ? 0 : filter.PageSize);
            }

            return new EntityPagedResult<Entity>
            {
                Items = collected,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                NextLink = nextLink
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entities");
            throw;
        }
    }

    public async Task<Entity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _sharePointClient.GetItemByIdAsync(
                _options.SiteId,
                _options.Lists.Entities,
                id.ToString(),
                SelectFields,
                cancellationToken);

            return item == null ? null : Map(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading entity {Id}", id);
            throw;
        }
    }

    public async Task<Entity?> GetByEntityIdAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        try
        {
            var safe = EscapeOData(entityId);
            var filter = $"fields/{EntityFields.EntityId} eq '{safe}'";

            var query = new ListQuery
            {
                PageSize = 5,
                Filter = filter
            };

            var response = await _sharePointClient.GetItemsAsync(
                _options.SiteId,
                _options.Lists.Entities,
                SelectFields,
                query,
                cancellationToken);

            var item = response.Items.FirstOrDefault();
            if (item != null)
            {
                return Map(item);
            }
        }
        catch (Exception ex)
        {
            // field_1 may not be indexed — fall back to scanning pages in memory.
            _logger.LogWarning(
                ex,
                "OData lookup for EntityId '{EntityId}' failed; falling back to in-memory search.",
                entityId);
        }

        return await FindByEntityIdInMemoryAsync(entityId, cancellationToken);
    }

    private async Task<Entity?> FindByEntityIdInMemoryAsync(
        string entityId,
        CancellationToken cancellationToken)
    {
        var pageNumber = 1;
        const int pageSize = 200;

        while (true)
        {
            var page = await GetPagedAsync(
                new EntityFilter { PageNumber = pageNumber, PageSize = pageSize },
                cancellationToken);

            var match = page.Items.FirstOrDefault(x =>
                string.Equals(x.EntityId, entityId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }

            if (page.Items.Count < pageSize || pageNumber * pageSize >= page.TotalCount)
            {
                return null;
            }

            pageNumber++;
        }
    }

    public async Task<bool> ExistsAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByEntityIdAsync(entityId, cancellationToken);

        if (existing == null)
        {
            return false;
        }

        if (excludeId.HasValue && existing.Id == excludeId.Value)
        {
            return false;
        }

        return true;
    }

    public async Task<Entity> CreateAsync(
        Entity entity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _sharePointClient.CreateItemAsync(
                _options.SiteId,
                _options.Lists.Entities,
                EntityMapper.ToFieldDictionary(entity),
                cancellationToken);

            var mapped = Map(response)
                ?? throw new InvalidOperationException("Entity mapping failed.");

            // Graph create responses sometimes omit Fields; ensure we always have the list item Id.
            if (mapped.Id <= 0 && !string.IsNullOrWhiteSpace(response.Id) &&
                int.TryParse(response.Id, out var createdId))
            {
                mapped.Id = createdId;
            }

            if (mapped.Id <= 0 && !string.IsNullOrWhiteSpace(entity.EntityId))
            {
                var reloaded = await GetByEntityIdAsync(entity.EntityId, cancellationToken);
                if (reloaded != null)
                {
                    return reloaded;
                }
            }

            if (mapped.Id <= 0)
            {
                throw new InvalidOperationException(
                    "Entity was created but SharePoint list item Id could not be resolved.");
            }

            return mapped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entity creation failed");
            throw;
        }
    }

    public async Task<Entity> UpdateAsync(
        Entity entity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sharePointClient.UpdateItemAsync(
                _options.SiteId,
                _options.Lists.Entities,
                entity.Id.ToString(),
                EntityMapper.ToFieldDictionary(entity),
                cancellationToken);

            return await GetByIdAsync(entity.Id, cancellationToken)
                ?? throw new InvalidOperationException("Updated entity not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entity update failed");
            throw;
        }
    }

    public async Task ArchiveAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Entity not found.");

        entity.Status = EntityStatuses.Archived;
        entity.IsActive = false;

        await UpdateAsync(entity, cancellationToken);
    }

    public async Task RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Entity not found.");

        entity.Status = EntityStatuses.Active;
        entity.IsActive = true;

        await UpdateAsync(entity, cancellationToken);
    }

    private async Task<EntityPagedResult<Entity>> GetPagedWithMetadataSearchAsync(
        EntityFilter filter,
        string? oDataFilter,
        CancellationToken cancellationToken)
    {
        var all = new List<Entity>();
        string? nextLink = null;

        do
        {
            var query = new ListQuery
            {
                PageSize = 200,
                Filter = string.IsNullOrWhiteSpace(nextLink) ? oDataFilter : null,
                OrderBy = $"fields/{EntityFields.Title}",
                NextLink = nextLink
            };

            var response = await _sharePointClient.GetItemsAsync(
                _options.SiteId,
                _options.Lists.Entities,
                SelectFields,
                query,
                cancellationToken);

            all.AddRange(response.Items
                .Select(Map)
                .Where(x => x != null)
                .Cast<Entity>());

            nextLink = response.NextLink;
        }
        while (!string.IsNullOrWhiteSpace(nextLink));

        var filtered = ApplyMetadataSearch(all, filter);
        var skip = Math.Max(0, (filter.PageNumber - 1) * filter.PageSize);

        var pageItems = filtered
            .Skip(skip)
            .Take(filter.PageSize)
            .ToList();

        return new EntityPagedResult<Entity>
        {
            Items = pageItems,
            TotalCount = filtered.Count,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
    }

    private static bool RequiresMetadataSearch(EntityFilter filter)
    {
        return !string.IsNullOrWhiteSpace(filter.SearchText);
    }

    private static string? BuildODataFilter(EntityFilter filter)
    {
        var clauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            clauses.Add($"fields/{EntityFields.EntityType} eq '{EscapeOData(filter.EntityType)}'");
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            clauses.Add($"fields/{EntityFields.Status} eq '{EscapeOData(filter.Status)}'");
        }

        if (filter.IsActive.HasValue)
        {
            clauses.Add($"fields/{EntityFields.IsActive} eq {filter.IsActive.Value.ToString().ToLowerInvariant()}");
        }

        if (filter.CreatedFrom.HasValue)
        {
            clauses.Add($"fields/{EntityFields.Created} ge '{filter.CreatedFrom.Value:O}'");
        }

        if (filter.CreatedTo.HasValue)
        {
            clauses.Add($"fields/{EntityFields.Created} le '{filter.CreatedTo.Value:O}'");
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText) && !RequiresMetadataSearchOnly(filter))
        {
            var safe = EscapeOData(filter.SearchText);
            clauses.Add(
                $"contains(fields/{EntityFields.Title},'{safe}') or " +
                $"contains(fields/{EntityFields.EntityId},'{safe}') or " +
                $"contains(fields/{EntityFields.EntityType},'{safe}')");
        }

        return clauses.Count == 0 ? null : string.Join(" and ", clauses);
    }

    private static bool RequiresMetadataSearchOnly(EntityFilter filter)
    {
        return !string.IsNullOrWhiteSpace(filter.SearchText);
    }

    private static List<Entity> ApplyMetadataSearch(IEnumerable<Entity> entities, EntityFilter filter)
    {
        var query = entities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText;

            query = query.Where(x =>
                x.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.EntityId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.EntityType.Contains(search, StringComparison.OrdinalIgnoreCase)
                || MetadataContains(x.Metadata, search));
        }

        return query
            .OrderBy(x => x.Title)
            .ToList();
    }

    private static bool MetadataContains(Dictionary<string, object?> metadata, string search)
    {
        foreach (var value in metadata.Values)
        {
            if (value?.ToString()?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeOData(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private Entity? Map(ListItem item)
    {
        if (item.Fields?.AdditionalData == null)
        {
            // Create/update responses can return an item with Id but no Fields payload.
            if (!string.IsNullOrWhiteSpace(item.Id) && int.TryParse(item.Id, out var idOnly))
            {
                return new Entity { Id = idOnly };
            }

            return null;
        }

        var entity = EntityMapper.FromDictionary(item.Fields.AdditionalData, item.Id);

        if (entity.Id <= 0)
        {
            _logger.LogWarning(
                "Mapped entity '{EntityId}' has missing SharePoint list item Id (ListItem.Id={ListItemId}).",
                entity.EntityId,
                item.Id);
        }

        return entity;
    }
}
