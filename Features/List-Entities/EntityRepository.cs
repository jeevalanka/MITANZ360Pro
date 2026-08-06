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

    Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> IsReferencedAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<Entity?> FindStudentByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<Entity?> FindByEmailVerifyTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<Entity?> FindReferralPartnerByCodeAsync(
        string referralCode,
        CancellationToken cancellationToken = default);
}

public sealed class EntityRepository : IEntityRepository
{
    private static readonly string[] SelectFields =
    [
        // Do not include "id" here — Graph fields($select=id,...) is invalid.
        // List item Id comes from ListItem.Id in Map().
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

            // Never send Filter/OrderBy to Graph — Title/field_* are often not indexed
            // and Graph returns: "Field 'X' cannot be referenced in filter or orderby".
            // Load pages with NextLink only, then filter/sort/page in memory.
            var all = new List<Entity>();
            string? nextLink = null;

            do
            {
                var query = new ListQuery
                {
                    PageSize = 200,
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

            var filtered = ApplyInMemoryFilter(all, filter);
            var skip = Math.Max(0, (filter.PageNumber - 1) * filter.PageSize);

            var pageItems = filtered
                .Skip(skip)
                .Take(filter.PageSize)
                .ToList();

            _logger.LogInformation(
                "Loaded {Total} entities from SharePoint; returning page {Page} ({Count} items).",
                filtered.Count,
                filter.PageNumber,
                pageItems.Count);

            return new EntityPagedResult<Entity>
            {
                Items = pageItems,
                TotalCount = filtered.Count,
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

        // Always scan in memory — OData filter on field_1 fails when the column is not indexed.
        var page = await GetPagedAsync(
            new EntityFilter
            {
                SearchText = entityId,
                PageNumber = 1,
                PageSize = 500
            },
            cancellationToken);

        return page.Items.FirstOrDefault(x =>
            string.Equals(x.EntityId, entityId, StringComparison.OrdinalIgnoreCase));
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

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sharePointClient.DeleteItemAsync(
                _options.SiteId,
                _options.Lists.Entities,
                id.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entity delete failed for {Id}", id);
            throw;
        }
    }

    public async Task<Entity?> FindStudentByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim();
        var page = await GetPagedAsync(
            new EntityFilter
            {
                EntityType = "Student",
                PageNumber = 1,
                PageSize = 500
            },
            cancellationToken);

        return page.Items.FirstOrDefault(x =>
            string.Equals(
                GetMetaString(x.Metadata, "Email"),
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Entity?> FindByEmailVerifyTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var page = await GetPagedAsync(
            new EntityFilter
            {
                EntityType = "Student",
                PageNumber = 1,
                PageSize = 500
            },
            cancellationToken);

        return page.Items.FirstOrDefault(x =>
            string.Equals(
                GetMetaString(x.Metadata, EntityFields.EmailVerifyTokenMeta),
                token.Trim(),
                StringComparison.Ordinal));
    }

    public async Task<Entity?> FindReferralPartnerByCodeAsync(
        string referralCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
        {
            return null;
        }

        var code = referralCode.Trim().ToUpperInvariant();
        var page = await GetPagedAsync(
            new EntityFilter
            {
                EntityType = "Agent",
                PageNumber = 1,
                PageSize = 500
            },
            cancellationToken);

        return page.Items.FirstOrDefault(x =>
            string.Equals(
                GetMetaString(x.Metadata, "ReferralCode"),
                code,
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsReferencedAsync(
        string entityId,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        // Soft reference check: other Entity records whose metadata/title mention this EntityId.
        var page = await GetPagedAsync(
            new EntityFilter { PageNumber = 1, PageSize = 500 },
            cancellationToken);

        foreach (var other in page.Items)
        {
            if (excludeId.HasValue && other.Id == excludeId.Value)
            {
                continue;
            }

            if (string.Equals(other.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (other.Title.Contains(entityId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var value in other.Metadata.Values)
            {
                if (value?.ToString()?.Contains(entityId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<Entity> ApplyInMemoryFilter(IEnumerable<Entity> entities, EntityFilter filter)
    {
        IEnumerable<Entity> query = entities;

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(x =>
                string.Equals(x.EntityType, filter.EntityType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x =>
                string.Equals(x.Status, filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }

        if (filter.CreatedFrom.HasValue)
        {
            query = query.Where(x => x.Created >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            query = query.Where(x => x.Created <= filter.CreatedTo.Value);
        }

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

    private static string GetMetaString(Dictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
        {
            return string.Empty;
        }

        return value switch
        {
            System.Text.Json.JsonElement json when json.ValueKind == System.Text.Json.JsonValueKind.String
                => json.GetString() ?? string.Empty,
            System.Text.Json.JsonElement json => json.ToString(),
            _ => value.ToString() ?? string.Empty
        };
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
