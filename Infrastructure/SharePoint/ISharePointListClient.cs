using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Common;

namespace MITANZ360Pro.Web.Infrastructure.SharePoint;

public interface ISharePointListClient
{
    Task<PagedResult<ListItem>> GetItemsAsync(
        string siteId,
        string listId,
        string[] selectFields,
        ListQuery query,
        CancellationToken cancellationToken = default);

    Task<int> GetItemCountAsync(
        string siteId,
        string listId,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<ListItem?> GetItemByIdAsync(
        string siteId,
        string listId,
        string itemId,
        string[] selectFields,
        CancellationToken cancellationToken = default);

    Task<ListItem> CreateItemAsync(
        string siteId,
        string listId,
        Dictionary<string, object> fields,
        CancellationToken cancellationToken = default);

    Task UpdateItemAsync(
        string siteId,
        string listId,
        string itemId,
        Dictionary<string, object> fields,
        CancellationToken cancellationToken = default);

    Task DeleteItemAsync(
        string siteId,
        string listId,
        string itemId,
        CancellationToken cancellationToken = default);
}
