using Microsoft.Graph.Models;

namespace MITANZ360Pro.Web.Services
{
    // =====================================================
    // ✅ MODEL
    // =====================================================
    public class ReferenceDataItem
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Code { get; set; } = "";

        public string Category { get; set; } = "";

        public string Description { get; set; } = "";

        public string Icon { get; set; } = "";

        public string Color { get; set; } = "";

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public bool IsDefault { get; set; }
    }

    public partial class SharePointService
    {
        // =====================================================
        // ✅ LIST RESOLVER
        // =====================================================
        private string GetReferenceListId(string module)
        {
            return module.ToUpper() switch
            {
                "LMS" => _configuration["SharePoint:Lists:LMS_ReferenceData"] ?? "",
                "SMS" => _configuration["SharePoint:Lists:SMS_ReferenceData"] ?? "",
                "HRM" => _configuration["SharePoint:Lists:HRM_ReferenceData"] ?? "",
                "CRM" => _configuration["SharePoint:Lists:CRM_ReferenceData"] ?? "",
                "FIN" => _configuration["SharePoint:Lists:FIN_ReferenceData"] ?? "",

                _ => throw new InvalidOperationException(
                    $"Unknown Reference Module: {module}")
            };
        }

        // =====================================================
        // ✅ GET ALL
        // =====================================================
        public async Task<List<ReferenceDataItem>>
            GetReferenceDataAsync(string module)
        {
            var listId = GetReferenceListId(module);

            var response = await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .GetAsync(config =>
                {
                    config.QueryParameters.Top = 500;
                    config.QueryParameters.Expand =
                        new[] { "fields" };
                });

            var items = new List<ReferenceDataItem>();

            foreach (var item in response?.Value ?? Enumerable.Empty<ListItem>())
            {
                var fields = item.Fields?.AdditionalData;

                items.Add(new ReferenceDataItem
                {
                    Id = int.TryParse(item.Id, out var id) ? id : 0,

                    Title = GetField(fields, "Title"),

                    Code = GetField(fields, "field_1"),

                    Category = GetField(fields, "field_2"),

                    Description = GetField(fields, "field_3"),

                    Icon = GetField(fields, "field_4"),

                    Color = GetField(fields, "field_5"),

                    SortOrder =
                        int.TryParse(
                            GetField(fields, "field_6"),
                            out var sort)
                            ? sort
                            : 0,

                    IsActive =
                        bool.TryParse(
                            GetField(fields, "field_7"),
                            out var active)
                            && active,

                    IsDefault =
                        bool.TryParse(
                            GetField(fields, "field_8"),
                            out var def)
                            && def
                });
            }

            return items
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();
        }

        // =====================================================
        // ✅ GET CATEGORIES
        // =====================================================
        public async Task<List<string>>
            GetReferenceCategoriesAsync(string module)
        {
            var items = await GetReferenceDataAsync(module);

            return items
                .Select(x => x.Category)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        // =====================================================
        // ✅ GET BY CATEGORY
        // =====================================================
        public async Task<List<ReferenceDataItem>>
            GetReferenceDataByCategoryAsync(string module,string category)
            {
                var items = await GetReferenceDataAsync(module);

                return items
                    .Where(x => x.Category.Equals( category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Title)
                    .ToList();
            }

        // =====================================================
        // ✅ GET BY ID
        // =====================================================
        public async Task<ReferenceDataItem?>
            GetReferenceDataAsync(
                string module,
                int id)
        {
            var items =
                await GetReferenceDataAsync(module);

            return items
                .FirstOrDefault(x => x.Id == id);
        }

        // =====================================================
        // ✅ CREATE
        // =====================================================
        public async Task<int>
            CreateReferenceDataAsync(
                string module,
                ReferenceDataItem item)
        {
            var listId =
                GetReferenceListId(module);

            var fields =
                new Dictionary<string, object>
                {
                    { "Title", item.Title },

                    { "field_1", item.Code },

                    { "field_2", item.Category },

                    { "field_3", item.Description },

                    { "field_4", item.Icon },

                    { "field_5", item.Color },

                    { "field_6", item.SortOrder },

                    { "field_7", item.IsActive },

                    { "field_8", item.IsDefault }
                };

            var result =
                await _graphClient
                    .Sites[SiteId]
                    .Lists[listId]
                    .Items
                    .PostAsync(
                        new ListItem
                        {
                            Fields =
                                new FieldValueSet
                                {
                                    AdditionalData = fields
                                }
                        });

            return int.TryParse(result?.Id, out var id)
                ? id
                : 0;
        }

        // =====================================================
        // ✅ UPDATE
        // =====================================================
        public async Task UpdateReferenceDataAsync(
            string module,
            ReferenceDataItem item)
        {
            var listId =
                GetReferenceListId(module);

            var fields =
                new Dictionary<string, object>
                {
                    { "Title", item.Title },

                    { "field_1", item.Code },

                    { "field_2", item.Category },

                    { "field_3", item.Description },

                    { "field_4", item.Icon },

                    { "field_5", item.Color },

                    { "field_6", item.SortOrder },

                    { "field_7", item.IsActive },

                    { "field_8", item.IsDefault }
                };

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items[item.Id.ToString()]
                .Fields
                .PatchAsync(
                    new FieldValueSet
                    {
                        AdditionalData = fields
                    });
        }

        // =====================================================
        // ✅ DELETE
        // =====================================================
        public async Task DeleteReferenceDataAsync(
            string module,
            int id)
        {
            var listId =
                GetReferenceListId(module);

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items[id.ToString()]
                .DeleteAsync();
        }

        // =====================================================
        // ✅ EXISTS
        // Category + Code Unique
        // =====================================================
        public async Task<bool>
            ReferenceDataExistsAsync(
                string module,
                string category,
                string code)
        {
            var items =
                await GetReferenceDataAsync(module);

            return items.Any(x =>
                x.Category.Equals(
                    category,
                    StringComparison.OrdinalIgnoreCase)
                &&
                x.Code.Equals(
                    code,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}