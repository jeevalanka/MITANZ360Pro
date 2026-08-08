using MITANZ360Pro.Web.Modules.ReferenceData;

namespace MITANZ360Pro.Web.Services
{
    /// <summary>
    /// Legacy SharePointService Reference Data API.
    /// Prefer <see cref="IReferenceDataService"/> for new UI.
    /// Uses the single SharePoint:Lists:ReferenceData list.
    /// </summary>
    public partial class SharePointService
    {
        private string GetReferenceListId(string? module = null)
        {
            var listId = _configuration["SharePoint:Lists:ReferenceData"];
            if (string.IsNullOrWhiteSpace(listId))
            {
                throw new InvalidOperationException(
                    "SharePoint Lists:ReferenceData configuration missing.");
            }

            return listId;
        }

        public async Task<List<ReferenceDataItem>> GetReferenceDataAsync(string module)
        {
            var listId = GetReferenceListId(module);

            var response = await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .GetAsync(config =>
                {
                    config.QueryParameters.Top = 500;
                    config.QueryParameters.Expand = ["fields"];
                });

            var items = new List<ReferenceDataItem>();

            foreach (var item in response?.Value ?? [])
            {
                var fields = item.Fields?.AdditionalData;
                items.Add(ReferenceDataMapper.FromDictionary(fields, item.Id));
            }

            return items
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();
        }

        public async Task<List<string>> GetReferenceCategoriesAsync(string module)
        {
            var items = await GetReferenceDataAsync(module);

            return items
                .Select(x => x.Category)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        public async Task<List<ReferenceDataItem>> GetReferenceDataByCategoryAsync(
            string module,
            string category)
        {
            var items = await GetReferenceDataAsync(module);

            return items
                .Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();
        }

        public async Task<ReferenceDataItem?> GetReferenceDataAsync(string module, int id)
        {
            var items = await GetReferenceDataAsync(module);
            return items.FirstOrDefault(x => x.Id == id);
        }

        public async Task<int> CreateReferenceDataAsync(string module, ReferenceDataItem item)
        {
            var listId = GetReferenceListId(module);
            var fields = ReferenceDataMapper.ToFieldDictionary(item);

            var result = await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .PostAsync(new Microsoft.Graph.Models.ListItem
                {
                    Fields = new Microsoft.Graph.Models.FieldValueSet
                    {
                        AdditionalData = fields
                    }
                });

            return int.TryParse(result?.Id, out var id) ? id : 0;
        }

        public async Task UpdateReferenceDataAsync(string module, ReferenceDataItem item)
        {
            var listId = GetReferenceListId(module);
            var fields = ReferenceDataMapper.ToFieldDictionary(item);

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items[item.Id.ToString()]
                .Fields
                .PatchAsync(new Microsoft.Graph.Models.FieldValueSet
                {
                    AdditionalData = fields
                });
        }

        public async Task DeleteReferenceDataAsync(string module, int id)
        {
            var listId = GetReferenceListId(module);

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items[id.ToString()]
                .DeleteAsync();
        }

        public async Task<bool> ReferenceDataExistsAsync(
            string module,
            string category,
            string code)
        {
            var items = await GetReferenceDataAsync(module);

            return items.Any(x =>
                x.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }
    }
}
