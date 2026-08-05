using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Sites.Item.Lists.Item.Items;
using MITANZ360Pro.Web.Common;
using MITANZ360Pro.Web.Infrastructure.SharePoint;

namespace MITANZ360Pro.Web.Infrastructure.SharePoint;

public sealed class SharePointListClient : ISharePointListClient
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<SharePointListClient> _logger;

    public SharePointListClient(
        GraphServiceClient graphClient,
        ILogger<SharePointListClient> logger)
    {
        _graphClient = graphClient;
        _logger = logger;
    }

    public async Task<PagedResult<ListItem>> GetItemsAsync(
        string siteId,
        string listId,
        string[] selectFields,
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ListItemCollectionResponse? response;

            if (!string.IsNullOrWhiteSpace(query.NextLink))
            {
                var requestBuilder = new ItemsRequestBuilder(query.NextLink, _graphClient.RequestAdapter);
                response = await requestBuilder.GetAsync(cancellationToken: cancellationToken);
            }
            else
            {
                response = await _graphClient
                    .Sites[siteId]
                    .Lists[listId]
                    .Items
                    .GetAsync(config =>
                    {
                        if (selectFields is { Length: > 0 })
                        {
                            config.QueryParameters.Expand =
                            [
                                $"fields($select={string.Join(",", selectFields)})"
                            ];
                        }
                        else
                        {
                            config.QueryParameters.Expand = ["fields"];
                        }

                        config.QueryParameters.Top = query.PageSize;

                        if (!string.IsNullOrWhiteSpace(query.Filter))
                        {
                            config.QueryParameters.Filter = query.Filter;
                        }

                        if (!string.IsNullOrWhiteSpace(query.OrderBy))
                        {
                            config.QueryParameters.Orderby = [query.OrderBy];
                        }
                    }, cancellationToken);
            }

            var items = response?.Value ?? [];

            return new PagedResult<ListItem>(
                items,
                response?.OdataNextLink,
                query.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list items from site {SiteId}, list {ListId}", siteId, listId);
            throw;
        }
    }

    public async Task<int> GetItemCountAsync(
        string siteId,
        string listId,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _graphClient
                .Sites[siteId]
                .Lists[listId]
                .Items
                .GetAsync(config =>
                {
                    config.QueryParameters.Count = true;
                    config.QueryParameters.Top = 1;

                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        config.QueryParameters.Filter = filter;
                    }

                    config.Headers.Add("ConsistencyLevel", "eventual");
                }, cancellationToken);

            if (response?.OdataCount.HasValue == true)
            {
                return (int)response.OdataCount.Value;
            }

            return response?.Value?.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Count query failed for list {ListId}; falling back to zero.", listId);
            return 0;
        }
    }

    public async Task<ListItem?> GetItemByIdAsync(
        string siteId,
        string listId,
        string itemId,
        string[] selectFields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _graphClient
                .Sites[siteId]
                .Lists[listId]
                .Items[itemId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Expand =
                    [
                        $"fields($select={string.Join(",", selectFields)})"
                    ];
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list item {ItemId}", itemId);
            throw;
        }
    }

    public async Task<ListItem> CreateItemAsync(
        string siteId,
        string listId,
        Dictionary<string, object> fields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = new ListItem
            {
                Fields = new FieldValueSet { AdditionalData = fields }
            };

            var created = await _graphClient
                .Sites[siteId]
                .Lists[listId]
                .Items
                .PostAsync(item, cancellationToken: cancellationToken);

            if (created == null)
            {
                throw new InvalidOperationException("Create returned null.");
            }

            if (created.Fields?.AdditionalData == null || created.Fields.AdditionalData.Count == 0)
            {
                var refreshed = await GetItemByIdAsync(
                    siteId,
                    listId,
                    created.Id!,
                    fields.Keys.ToArray(),
                    cancellationToken);

                return refreshed ?? created;
            }

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating list item");
            throw;
        }
    }

    public async Task UpdateItemAsync(
        string siteId,
        string listId,
        string itemId,
        Dictionary<string, object> fields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _graphClient
                .Sites[siteId]
                .Lists[listId]
                .Items[itemId]
                .Fields
                .PatchAsync(new FieldValueSet { AdditionalData = fields }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating list item {ItemId}", itemId);
            throw;
        }
    }

    public async Task DeleteItemAsync(
        string siteId,
        string listId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _graphClient
                .Sites[siteId]
                .Lists[listId]
                .Items[itemId]
                .DeleteAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting list item {ItemId}", itemId);
            throw;
        }
    }
}
