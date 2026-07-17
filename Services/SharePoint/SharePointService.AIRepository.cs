using Microsoft.Graph;
using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Services.AI;
using System.Text.Json;
using System.Threading;

namespace MITANZ360Pro.Web.Services
{
    public class AIRepositoryItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Metadata { get; set; } = "";
        public string Summary { get; set; } = "";
        public string HtmlReport { get; set; } = "";
        public string Status { get; set; } = "";
        public string Tags { get; set; } = "";
        public decimal Score { get; set; }
    }

    public partial class SharePointService
    {
        // =====================================================
        // ✅ SIMPLE GET
        // =====================================================
        public async Task<List<AIRepositoryItem>> GetAIRepositoryItemsAsync(string? orderBy)
        {
            return await GetAIRepositoryItemsAsync(null, orderBy, null, null, CancellationToken.None);
        }

        // =====================================================
        // ✅ MAIN GET (FIXED)
        // =====================================================
        public async Task<List<AIRepositoryItem>> GetAIRepositoryItemsAsync(
            string? filter,
            string? orderBy,
            decimal? top,
            string? select,
            CancellationToken cancellationToken)
        {
            var listId = _configuration["SharePoint:Lists:AIRepository"];

            if (string.IsNullOrWhiteSpace(listId))
                throw new InvalidOperationException("AIRepository ListId missing.");

            var topInt = top.HasValue ? (int)top.Value : 50;

            var response = await _graphClient
                .Sites[SiteId]   // ✅ FIXED (CRITICAL)
                .Lists[listId]
                .Items
                .GetAsync(request =>
                {
                    request.QueryParameters.Top = topInt;

                    // ✅ CRITICAL FIX → Select + Expand together
                    request.QueryParameters.Select = new[] { "id", "fields" };
                    request.QueryParameters.Expand = new[] { "fields" };

                    if (!string.IsNullOrWhiteSpace(filter))
                        request.QueryParameters.Filter = filter;

                    if (!string.IsNullOrWhiteSpace(orderBy))
                        request.QueryParameters.Orderby = new[] { orderBy };

                }, cancellationToken: cancellationToken);

            var items = new List<AIRepositoryItem>();

            foreach (var item in response?.Value ?? Enumerable.Empty<ListItem>())
            {
                var fields = item.Fields?.AdditionalData;

                _logger.LogDebug("AIRepo Fields: {Fields}",
                    fields == null ? "NULL" : JsonSerializer.Serialize(fields));

                items.Add(new AIRepositoryItem
                {
                    Id = int.TryParse(item.Id, out var id) ? id : 0,
                    Title = GetField(fields, "Title"),
                    Metadata = GetField(fields, "Metadata"),
                    Summary = GetField(fields, "Summary"),
                    HtmlReport = GetField(fields, "HtmlReport"),
                    Status = GetField(fields, "Status"),
                    Tags = GetField(fields, "Tags"),
                    Score = decimal.TryParse(GetField(fields, "Score"), out var s) ? s : 0
                });
            }

            return items;
        }

        // =====================================================
        // ✅ CREATE
        // =====================================================
        public async Task CreateAIRepositoryItemAsync(AIRepositoryItem item)
        {
            var listId = _configuration["SharePoint:Lists:AIRepository"];

            var fields = new Dictionary<string, object>
            {
                { "Title", item.Title },
                { "Metadata", item.Metadata },
                { "Summary", item.Summary },
                { "HtmlReport", item.HtmlReport },
                { "Status", item.Status },
                { "Tags", item.Tags },
                { "Score", item.Score }
            };

            await _graphClient
                .Sites[SiteId]   // ✅ FIXED
                .Lists[listId]
                .Items
                .PostAsync(new ListItem
                {
                    Fields = new FieldValueSet
                    {
                        AdditionalData = fields
                    }
                });
        }

        // =====================================================
        // ✅ UPDATE
        // =====================================================
        public async Task UpdateAIRepositoryItemAsync(AIRepositoryItem item)
        {
            await UpdateAIRepositoryItemAsync(item, CancellationToken.None);
        }

        public async Task UpdateAIRepositoryItemAsync(
            AIRepositoryItem item,
            CancellationToken cancellationToken)
        {
            var listId = _configuration["SharePoint:Lists:AIRepository"];

            var fields = new Dictionary<string, object>
            {
                { "Title", item.Title },
                { "Metadata", item.Metadata },
                { "Summary", item.Summary },
                { "HtmlReport", item.HtmlReport },
                { "Status", item.Status },
                { "Tags", item.Tags },
                { "Score", item.Score }
            };

            await _graphClient
                .Sites[SiteId]   // ✅ FIXED
                .Lists[listId]
                .Items[item.Id.ToString()]
                .Fields
                .PatchAsync(
                    new FieldValueSet
                    {
                        AdditionalData = fields
                    },
                    null,
                    cancellationToken);
        }

        // =====================================================
        // ✅ DELETE
        // =====================================================
        public async Task DeleteAIRepositoryItemAsync(int itemId)
        {
            var listId = _configuration["SharePoint:Lists:AIRepository"];

            await _graphClient
                .Sites[SiteId]   // ✅ FIXED
                .Lists[listId]
                .Items[itemId.ToString()]
                .DeleteAsync();
        }

        // =====================================================
        // ✅ AI UPDATE
        // =====================================================
        public async Task UpdateAIFieldsAsync(
            int itemId,
            AIResultDto aiResult,
            string promptName,
            CancellationToken cancellationToken = default)
        {
            var listId = _configuration["SharePoint:Lists:AIRepository"];

            var fields = new Dictionary<string, object>
            {
                { "Summary", aiResult.Summary ?? "" },
                { "HtmlReport", aiResult.Html ?? "" },
                { "Tags", promptName ?? "" },
                { "Score", aiResult.Score }
            };

            await _graphClient
                .Sites[SiteId]   // ✅ FIXED
                .Lists[listId]
                .Items[itemId.ToString()]
                .Fields
                .PatchAsync(
                    new FieldValueSet
                    {
                        AdditionalData = fields
                    },
                    null,
                    cancellationToken);
        }

        // =====================================================
        // ✅ COURSE EXISTS
        // =====================================================
        public async Task<bool> CourseExistsAsync(string courseName, string courseCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(courseName) ||
                    string.IsNullOrWhiteSpace(courseCode))
                    return false;

                var filter =
                    $"fields/Title eq '{courseName.Replace("'", "''")}' and " +
                    $"fields/CourseCode eq '{courseCode.Replace("'", "''")}'";

                var listId = _configuration["SharePoint:Lists:AIRepository"];

                var result = await _graphClient
                    .Sites[SiteId]   // ✅ FIXED
                    .Lists[listId]
                    .Items
                    .GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Filter = filter;
                        cfg.QueryParameters.Top = 1;

                        // ✅ FIX (important)
                        cfg.QueryParameters.Select = new[] { "id", "fields" };
                        cfg.QueryParameters.Expand = new[] { "fields" };
                    });

                return result?.Value?.Any() == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CourseExists error");
                return false;
            }
        }
    }
}