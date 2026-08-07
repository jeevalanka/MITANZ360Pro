using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Common;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Modules.ReferenceData;

public interface IReferenceDataRepository
{
    Task<IReadOnlyList<ReferenceDataItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ReferenceDataItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ReferenceDataItem> CreateAsync(ReferenceDataItem item, CancellationToken cancellationToken = default);

    Task<ReferenceDataItem> UpdateAsync(ReferenceDataItem item, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class ReferenceDataRepository : IReferenceDataRepository
{
    private static readonly string[] SelectFields =
    [
        ReferenceDataFields.Title,
        ReferenceDataFields.Code,
        ReferenceDataFields.Category,
        ReferenceDataFields.Description,
        ReferenceDataFields.Icon,
        ReferenceDataFields.Color,
        ReferenceDataFields.SortOrder,
        ReferenceDataFields.IsActive,
        ReferenceDataFields.IsDefault
    ];

    private readonly ISharePointListClient _sharePointClient;
    private readonly SharePointOptions _options;
    private readonly ILogger<ReferenceDataRepository> _logger;

    public ReferenceDataRepository(
        ISharePointListClient sharePointClient,
        IOptions<SharePointOptions> options,
        ILogger<ReferenceDataRepository> logger)
    {
        _sharePointClient = sharePointClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.SiteId))
        {
            throw new InvalidOperationException("SharePoint SiteId configuration missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.Lists.ReferenceData))
        {
            throw new InvalidOperationException("SharePoint Lists:ReferenceData configuration missing.");
        }
    }

    public async Task<IReadOnlyList<ReferenceDataItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var all = new List<ReferenceDataItem>();
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
                    _options.Lists.ReferenceData,
                    SelectFields,
                    query,
                    cancellationToken);

                all.AddRange(response.Items
                    .Select(Map)
                    .Where(x => x != null)
                    .Cast<ReferenceDataItem>());

                nextLink = response.NextLink;
            }
            while (!string.IsNullOrWhiteSpace(nextLink));

            return all
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load reference data");
            throw;
        }
    }

    public async Task<ReferenceDataItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _sharePointClient.GetItemByIdAsync(
                _options.SiteId,
                _options.Lists.ReferenceData,
                id.ToString(),
                SelectFields,
                cancellationToken);

            return item == null ? null : Map(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading reference data {Id}", id);
            throw;
        }
    }

    public async Task<ReferenceDataItem> CreateAsync(
        ReferenceDataItem item,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _sharePointClient.CreateItemAsync(
                _options.SiteId,
                _options.Lists.ReferenceData,
                ReferenceDataMapper.ToFieldDictionary(item),
                cancellationToken);

            var mapped = Map(response)
                ?? throw new InvalidOperationException("Reference data mapping failed after create.");

            if (mapped.Id <= 0 && !string.IsNullOrWhiteSpace(response.Id) &&
                int.TryParse(response.Id, out var createdId))
            {
                mapped.Id = createdId;
            }

            if (mapped.Id <= 0)
            {
                // Reload full list and match Category+Code
                var all = await GetAllAsync(cancellationToken);
                var match = all.FirstOrDefault(x =>
                    x.Category.Equals(item.Category, StringComparison.OrdinalIgnoreCase) &&
                    x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            return mapped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reference data create failed");
            throw;
        }
    }

    public async Task<ReferenceDataItem> UpdateAsync(
        ReferenceDataItem item,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sharePointClient.UpdateItemAsync(
                _options.SiteId,
                _options.Lists.ReferenceData,
                item.Id.ToString(),
                ReferenceDataMapper.ToFieldDictionary(item),
                cancellationToken);

            return await GetByIdAsync(item.Id, cancellationToken)
                ?? throw new InvalidOperationException("Updated reference item not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reference data update failed for {Id}", item.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sharePointClient.DeleteItemAsync(
                _options.SiteId,
                _options.Lists.ReferenceData,
                id.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reference data delete failed for {Id}", id);
            throw;
        }
    }

    private ReferenceDataItem? Map(ListItem item)
    {
        if (item.Fields?.AdditionalData == null)
        {
            if (!string.IsNullOrWhiteSpace(item.Id) && int.TryParse(item.Id, out var idOnly))
            {
                return new ReferenceDataItem { Id = idOnly };
            }

            return null;
        }

        return ReferenceDataMapper.FromDictionary(item.Fields.AdditionalData, item.Id);
    }
}
