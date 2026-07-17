using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json.Nodes;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiHtmlTemplateService
{
    private readonly ILogger<AiHtmlTemplateService> _logger;

    public AiHtmlTemplateService(
        ILogger<AiHtmlTemplateService> logger)
    {
        _logger = logger;
    }

    // =====================================================
    // ✅ MAIN GENERATOR
    // =====================================================
    public string GenerateHtml(string title, object? content)
    {
        try
        {
            var json = content?.ToString();

            if (string.IsNullOrWhiteSpace(json))
                return "<p>No content available</p>";

            var node = JsonNode.Parse(json);

            var sb = new StringBuilder();

            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='utf-8' />");

            // ✅ GOOD LOOKING UI (NOT DULL)
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Segoe UI; margin:20px; background:#f4f6f9; }");
            sb.AppendLine("h1 { color:#003366; margin-bottom:20px; }");
            sb.AppendLine("h2 { color:#0078d4; margin-top:20px; }");
            sb.AppendLine(".card { background:#fff; padding:15px; border-radius:10px; margin-bottom:15px; box-shadow:0 2px 6px rgba(0,0,0,0.1);} ");
            sb.AppendLine("ul { padding-left:20px; }");
            sb.AppendLine("p { margin:4px 0; }");
            sb.AppendLine("</style>");

            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine($"<h1>{title}</h1>");

            // ✅ GENERIC JSON RENDER
            RenderNode(node, sb);

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            _logger.LogInformation("✅ Generic HTML generated successfully");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ HTML generation failed");
            throw;
        }
    }

    // =====================================================
    // ✅ GENERIC RENDER ENGINE (THIS FIXES YOUR PROBLEM)
    // =====================================================
    private void RenderNode(JsonNode? node, StringBuilder sb)
    {
        if (node == null)
            return;

        // ✅ OBJECT → Section
        if (node is JsonObject obj)
        {
            foreach (var prop in obj)
            {
                sb.AppendLine("<div class='card'>");

                sb.AppendLine($"<h2>{FormatTitle(prop.Key)}</h2>");

                RenderNode(prop.Value, sb);

                sb.AppendLine("</div>");
            }
        }

        // ✅ ARRAY → List
        else if (node is JsonArray arr)
        {
            sb.AppendLine("<ul>");

            foreach (var item in arr)
            {
                sb.AppendLine("<li>");
                RenderNode(item, sb);
                sb.AppendLine("</li>");
            }

            sb.AppendLine("</ul>");
        }

        // ✅ VALUE → Paragraph
        else
        {
            sb.AppendLine($"<p>{node.ToString()}</p>");
        }
    }

    // =====================================================
    // ✅ CLEAN TITLE FORMAT
    // =====================================================
    private string FormatTitle(string text)
    {
        return System.Text.RegularExpressions.Regex
            .Replace(text, "([a-z])([A-Z])", "$1 $2");
    }
}