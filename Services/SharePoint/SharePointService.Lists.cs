using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    // =====================================================
    // LIST ID RESOLUTION
    // =====================================================
    protected async Task<string> GetListIdByNameAsync(string listName, CancellationToken ct = default)
    {
        EnforceAdminOrTrainer();

        if (string.IsNullOrWhiteSpace(listName))
            throw new ArgumentException("List name is required.", nameof(listName));

        var safe = EscapeODataString(listName);

        var response = await ExecuteWithRetryAsync(
            token => _graphClient
                .Sites[SiteId]
                .Lists
                .GetAsync(r =>
                {
                    r.QueryParameters.Filter = $"displayName eq '{safe}'";
                    r.QueryParameters.Select = ["id", "displayName"];
                    r.QueryParameters.Top = 10;
                }, token),
            $"GetListIdByName({listName})",
            ct).ConfigureAwait(false);

        var list = response?.Value?.FirstOrDefault();

        if (list == null || string.IsNullOrWhiteSpace(list.Id))
            throw new InvalidOperationException($"List not found: {listName}");

        return list.Id!;
    }

    // =====================================================
    // CREATE ITEM (generic helper)
    // =====================================================
    protected async Task CreateListItemAsync(
        string listId,
        Dictionary<string, object?> fields,
        CancellationToken ct)
    {
        EnforceAdminOrTrainer();

        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("ListId is required.", nameof(listId));

        var clean = NormalizeFields(fields);

        await ExecuteWithRetryAsync<ListItem>(async token =>
        {
            var item = new ListItem
            {
                Fields = new FieldValueSet
                {
                    AdditionalData = clean
                }
            };

            var result = await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .PostAsync(item, cancellationToken: token)
                .ConfigureAwait(false);

            return result!; // ✅ REQUIRED → fixes CS0411
        },
        $"CreateListItem(list={listId})",
        ct).ConfigureAwait(false);
    }

    // =====================================================
    // UPDATE ITEM (generic helper)
    // =====================================================
    protected async Task UpdateListItemFieldsAsync(
        string listId,
        string itemId,
        Dictionary<string, object?> fields,
        CancellationToken ct)
    {
        EnforceAdminOrTrainer();

        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("ListId is required.", nameof(listId));

        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("ItemId is required.", nameof(itemId));

        var clean = NormalizeFields(fields);
        var patch = new FieldValueSet { AdditionalData = clean };

        // ✅ SINGLE OWNER of PATCH /fields
        await PatchListItemFieldsAsync(listId, itemId, patch, ct).ConfigureAwait(false);

        // ✅ Compile-safe audit (logs only)
        await WriteAuditAsync($"Updated list item fields. listId={listId}, itemId={itemId}", ct).ConfigureAwait(false);
    }

    // =====================================================
    // FIND ITEM BY FIELD (filtered)
    // =====================================================
    protected async Task<ListItem?> FindItemByFieldAsync(
        string listId,
        string field,
        string value,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(listId))
            throw new ArgumentException("ListId is required.", nameof(listId));

        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Field is required.", nameof(field));

        var safeValue = EscapeODataString(value);

        var response = await ExecuteWithRetryAsync(
            token => _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .GetAsync(r =>
                {
                    r.QueryParameters.Expand = ["fields"];
                    r.QueryParameters.Select = ["id", "fields"];
                    r.QueryParameters.Top = 1;
                    r.QueryParameters.Filter = $"fields/{field} eq '{safeValue}'";
                }, token),
            $"FindItemByField(list={listId},field={field})",
            ct).ConfigureAwait(false);

        return response?.Value?.FirstOrDefault();
    }

    // =====================================================
    // UPSERT (filtered + minimal)
    // =====================================================
    protected async Task UpsertItemAsync(
        string listId,
        string keyField,
        string keyValue,
        Dictionary<string, object?> fields,
        CancellationToken ct)
    {
        var existing = await FindItemByFieldAsync(listId, keyField, keyValue, ct).ConfigureAwait(false);

        if (existing != null && !string.IsNullOrWhiteSpace(existing.Id))
            await UpdateListItemFieldsAsync(listId, existing.Id!, fields, ct).ConfigureAwait(false);
        else
            await CreateListItemAsync(listId, fields, ct).ConfigureAwait(false);
    }

    public async Task UpsertAsync(
        string listName,
        string keyField,
        string keyValue,
        Dictionary<string, object?> fields,
        CancellationToken ct)
    {
        var listId = await GetListIdByNameAsync(listName, ct).ConfigureAwait(false);
        await UpsertItemAsync(listId, keyField, keyValue, fields, ct).ConfigureAwait(false);
    }

    public async Task UpsertAsync(
        string listName,
        Dictionary<string, object?> fields,
        CancellationToken ct)
    {
        const string keyField = "Title";
        var keyValue =
            fields.TryGetValue(keyField, out var v) && v != null
                ? v.ToString()!
                : Guid.NewGuid().ToString();

        var listId = await GetListIdByNameAsync(listName, ct).ConfigureAwait(false);
        await UpsertItemAsync(listId, keyField, keyValue, fields, ct).ConfigureAwait(false);
    }

    public Task UpsertAsync(string listName, Dictionary<string, object?> fields)
        => UpsertAsync(listName, fields, CancellationToken.None);

    // =====================================================
    // ✅ SINGLE DEFINITION — PATCH /fields (Graph v5-safe)
    // =====================================================
    protected async Task PatchListItemFieldsAsync(
        string listId,
        string itemId,
        FieldValueSet patch,
        CancellationToken ct)
    {
        await ExecuteWithRetryAsync<bool>(async token =>
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.PATCH,
                UrlTemplate = "{+baseurl}/sites/{siteId}/lists/{listId}/items/{itemId}/fields",
                PathParameters =
            {
                ["siteId"] = SiteId,
                ["listId"] = listId,
                ["itemId"] = itemId
            }
            };

            requestInfo.Headers.TryAdd("Accept", "application/json");
            requestInfo.SetContentFromParsable(_graphClient.RequestAdapter, "application/json", patch);

            await _graphClient.RequestAdapter
                .SendNoContentAsync(requestInfo, cancellationToken: token)
                .ConfigureAwait(false);

            return true; // ✅ REQUIRED for ExecuteWithRetryAsync<T>
        },
        $"PatchListItemFields(list={listId},item={itemId})",
        ct).ConfigureAwait(false);
    }

}