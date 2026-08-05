using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Common;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.Entities;

public interface IEntityRepository
{
    Task<PagedResult<Entity>> GetPagedAsync(
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

    Task DeleteAsync(
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
    private readonly ILogger<EntityRepository> _logger;
    private readonly string _siteId;
    private readonly string _listId;

    public EntityRepository(
        ISharePointListClient sharePointClient,
        ILogger<EntityRepository> logger,
        IConfiguration configuration)
    {
        _sharePointClient = sharePointClient;
        _logger = logger;

        _siteId = configuration["SharePoint:SiteId"]
            ?? throw new InvalidOperationException(
                "SharePoint SiteId configuration missing.");

        _listId = configuration["SharePoint:Lists:Entities"]
            ?? EntityList.ListId;
    }

    public async Task<PagedResult<Entity>> GetPagedAsync(
        EntityFilter filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Loading Entity page {PageNumber}",
                filter.PageNumber);

            // Do not set OrderBy or Filter on the Graph query — SharePoint list
            // columns (Title, field_*) are often not indexed and Graph will reject
            // the request. Filter and sort in memory instead.
            var query = new ListQuery
            {
                PageSize = filter.PageSize
            };

            var response = await _sharePointClient.GetItemsAsync(
                _siteId,
                _listId,
                SelectFields,
                query,
                cancellationToken);

            var entities = response.Items
                .Select(Map)
                .Where(x => x != null)
                .Cast<Entity>()
                .ToList();

            entities = ApplyInMemoryFilter(entities, filter);

            return new PagedResult<Entity>
            {
                Items = entities,
                TotalCount = entities.Count,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
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
                _siteId,
                _listId,
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
        var page = await GetPagedAsync(
            new EntityFilter
            {
                SearchText = entityId,
                PageSize = 500
            },
            cancellationToken);

        return page.Items
            .FirstOrDefault(x =>
                x.EntityId.Equals(
                    entityId,
                    StringComparison.OrdinalIgnoreCase));
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
                _siteId,
                _listId,
                EntityMapper.ToFieldDictionary(entity),
                cancellationToken);

            return Map(response)
                ?? throw new InvalidOperationException("Entity mapping failed.");
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
                _siteId,
                _listId,
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

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sharePointClient.DeleteItemAsync(
                _siteId,
                _listId,
                id.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entity deletion failed");
            throw;
        }
    }

    private static List<Entity> ApplyInMemoryFilter(
        IEnumerable<Entity> entities,
        EntityFilter filter)
    {
        var query = entities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            query = query.Where(x =>
                x.Title.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase)
                || x.EntityId.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase)
                || x.EntityType.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(x =>
                x.EntityType.Equals(filter.EntityType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x =>
                x.Status.Equals(filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive);
        }

        if (filter.CreatedFrom.HasValue)
        {
            query = query.Where(x => x.Created >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            query = query.Where(x => x.Created <= filter.CreatedTo.Value);
        }

        if (filter.ModifiedFrom.HasValue)
        {
            query = query.Where(x => x.Modified >= filter.ModifiedFrom.Value);
        }

        if (filter.ModifiedTo.HasValue)
        {
            query = query.Where(x => x.Modified <= filter.ModifiedTo.Value);
        }

        return query
            .OrderBy(x => x.Title)
            .ToList();
    }

    private static Entity? Map(ListItem item)
    {
        if (item.Fields?.AdditionalData == null)
        {
            return null;
        }

        return EntityMapper.FromDictionary(item.Fields.AdditionalData);
    }
}
