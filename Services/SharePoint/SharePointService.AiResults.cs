using Microsoft.Graph;
using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Services.AI;
using System.Text;
using System.Text.Json;

namespace MITANZ360Pro.Web.Services
{
    public partial class SharePointService
    {
        private const string AIList = "AIResalt";

        // =====================================================
        // ✅ SAVE AI RESULT (FILES → DRIVE ✅ OK to use DriveId)
        // =====================================================
        public async Task SaveAIResultAsync(
            string workflowId,
            string targetEntityId,
            AiWorkflowResult result,
            string? renderedHtml,
            string? summaryText,
            CancellationToken cancellationToken)
        {
            var executionId = targetEntityId;
            var folderPath = $"AI-Results/{workflowId}/{executionId}";

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await UploadTextFileAsync($"{folderPath}/result.json", json, cancellationToken);

            if (!string.IsNullOrWhiteSpace(renderedHtml))
            {
                await UploadTextFileAsync($"{folderPath}/result.html", renderedHtml, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(summaryText))
            {
                await UploadTextFileAsync($"{folderPath}/summary.txt", summaryText, cancellationToken);
            }
        }

        // =====================================================
        // ✅ CREATE DRAFT (FIXED → SitePath)
        // =====================================================
        public async Task<string> CreateAIDraftAsync(string entityType, string metadataJson)
        {
            var fields = new Dictionary<string, object>
            {
                { "Metadata", metadataJson },
                { "Status", "Draft" }
            };

            var item = await _graphClient
                .Sites[SitePath] // ✅ FIXED
                .Lists[AIList]
                .Items
                .PostAsync(new ListItem
                {
                    Fields = new FieldValueSet
                    {
                        AdditionalData = fields
                    }
                });

            return item?.Id ?? "";
        }

        // =====================================================
        // ✅ UPDATE METADATA (FIXED)
        // =====================================================
        public async Task UpdateMetadataAsync(string itemId, string instruction)
        {
            var item = await _graphClient
                .Sites[SitePath] // ✅ FIXED
                .Lists[AIList]
                .Items[itemId]
                .Fields
                .GetAsync();

            var metadata = item?.AdditionalData?["Metadata"]?.ToString() ?? "{}";

            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(metadata)
                      ?? new Dictionary<string, object>();

            obj["Instruction"] = instruction;

            var updated = JsonSerializer.Serialize(obj);

            await _graphClient
                .Sites[SitePath] // ✅ FIXED
                .Lists[AIList]
                .Items[itemId]
                .Fields
                .PatchAsync(new FieldValueSet
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "Metadata", updated }
                    }
                });
        }

        // =====================================================
        // ✅ GET METADATA
        // =====================================================
        public async Task<string> GetMetadataAsync(string itemId)
        {
            var item = await _graphClient
                .Sites[SitePath] // ✅ FIXED
                .Lists[AIList]
                .Items[itemId]
                .Fields
                .GetAsync();

            return item?.AdditionalData?["Metadata"]?.ToString() ?? "{}";
        }

        // =====================================================
        // ✅ UPDATE AI RESULT FIELDS
        // =====================================================
        public async Task UpdateAIResultFieldsAsync(
            string itemId,
            int score,
            string status,
            string html,
            string summary,
            string tags)
        {
            var fields = new Dictionary<string, object>
            {
                { "Score", score },
                { "Status", status },
                { "HtmlReport", html },
                { "Summary", summary },
                { "Tags", tags }
            };

            await _graphClient
                .Sites[SitePath] // ✅ FIXED
                .Lists[AIList]
                .Items[itemId]
                .Fields
                .PatchAsync(new FieldValueSet
                {
                    AdditionalData = fields
                });
        }

        // =====================================================
        // ✅ FILE UPLOAD (DRIVE → KEEP AS IS ✅)
        // =====================================================
        private async Task UploadTextFileAsync(
            string path,
            string content,
            CancellationToken cancellationToken)
        {
            var driveId = _configuration["SharePoint:Libraries:LMS:DriveId"]
                ?? throw new InvalidOperationException("SharePoint LMS DriveId is missing.");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            await _graphClient
                .Drives[driveId] // ✅ CORRECT (Drive uses DriveId)
                .Root
                .ItemWithPath(path)
                .Content
                .PutAsync(stream, cancellationToken: cancellationToken);
        }
    }
}