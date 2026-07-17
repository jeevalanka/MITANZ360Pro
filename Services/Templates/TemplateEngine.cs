using MITANZ360Pro.Web.Services.AI;
using System.Text;
using System.Text.Json;

namespace MITANZ360Pro.Web.Services.Templates
{
    public sealed class TemplateEngine
    {
        public string? RenderAIReport(
            AiWorkflowResult result,
            IReadOnlyDictionary<string, string> metadata)
        {
            if (result == null || !result.Success)
                return null;

            string cleanContent = string.Empty;

            try
            {
                // ✅ Step 1: Read FULL result JSON
                var root = JsonDocument.Parse(
                    JsonSerializer.Serialize(result.Result));

                // ✅ Step 2: Navigate to choices[0].message.content
                var contentJson = root
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (!string.IsNullOrWhiteSpace(contentJson))
                {
                    // ✅ Step 3: PARSE inner JSON string
                    var parsedInner = JsonDocument.Parse(contentJson);

                    cleanContent = JsonSerializer.Serialize(
                        parsedInner.RootElement,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });
                }
                else
                {
                    cleanContent = "AI returned empty content.";
                }
            }
            catch
            {
                // ✅ fallback (safe)
                cleanContent = JsonSerializer.Serialize(
                    result.Result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
            }

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='utf-8' />");
            sb.AppendLine("<title>AI Result</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial; padding: 20px; background: #f5f5f5; }");
            sb.AppendLine("h1 { color: #333; }");
            sb.AppendLine("pre { background: #fff; padding: 15px; border-radius: 8px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("<h1>AI Parsed Result</h1>");

            sb.AppendLine("<pre>");
            sb.AppendLine(System.Net.WebUtility.HtmlEncode(cleanContent));
            sb.AppendLine("</pre>");

            // ✅ metadata block
            if (metadata != null && metadata.Count > 0)
            {
                sb.AppendLine("<h2>Metadata</h2>");
                sb.AppendLine("<ul>");

                foreach (var item in metadata)
                {
                    sb.AppendLine($"<li><b>{item.Key}:</b> {System.Net.WebUtility.HtmlEncode(item.Value)}</li>");
                }

                sb.AppendLine("</ul>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}