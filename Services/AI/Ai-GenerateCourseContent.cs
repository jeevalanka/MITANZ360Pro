using Azure;
using Azure.AI.OpenAI;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenAI;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MITANZ360Pro.Web.Services.AI
{
    public partial class AIService
    {
        // ✅ MAIN ENTRY
        public async Task<AIResult> GenerateCourseContentAsync(string jsonPayload)
        {
            var prompt = BuildCoursePrompt(jsonPayload);

            var aiJson = await ExecuteAzureOpenAIAsync(jsonPayload, prompt);

            // ✅ NORMALIZE JSON (IMPORTANT FIX)
            var normalizedJson = JsonSerializer.Serialize(
                JsonNode.Parse(aiJson),
                new JsonSerializerOptions { WriteIndented = false }
            );

            // ✅ GENERATE HTML (RAW ONLY)
            var html = GenerateHtml(normalizedJson);

            // ✅ SUMMARY
            var summary = GenerateSummary(normalizedJson);

            return new AIResult
            {
                Success = true,
                Json = normalizedJson,
                HtmlContent = html,     // ✅ RAW HTML
                SummaryText = summary,
                FileName = $"Course_{DateTime.UtcNow:yyyyMMddHHmmss}.docx"
            };
        }
        // ✅ PROMPT
        private string BuildCoursePrompt(string json)
        {
            return $@"
                    You are a Senior Curriculum Architect, NZQA Programme Developer,
                    Microsoft Learning Designer, AQF Compliance Specialist,
                    and Enterprise Education Consultant.

                    Your responsibility is to enhance and complete the supplied course JSON.

                    ==================================================
                    COURSE DESIGN STANDARDS
                    ==================================================

                    Follow:

                    - NZQA Programme Design Principles
                    - AQF Alignment Principles
                    - ANZ Education Standards
                    - Bloom's Taxonomy
                    - Adult Learning Theory
                    - Competency Based Learning
                    - Industry Aligned Learning Outcomes

                    ==================================================
                    JSON INPUT
                    ==================================================

                    {json}

                    ==================================================
                    OBJECTIVES
                    ==================================================

                    1. Analyse all supplied fields.

                    2. Complete missing information.

                    3. Improve weak content.

                    4. Create professional course descriptions.

                    5. Generate measurable learning outcomes.

                    6. Generate graduate profile outcomes.

                    7. Improve module descriptions.

                    8. Create assessment recommendations.

                    9. Create delivery recommendations.

                    10. Create industry relevance statements.

                    11. Create career pathway statements.

                    12. Improve resource recommendations.

                    13. Ensure all generated content aligns with
                        the supplied qualification level.

                    14. Ensure all content remains realistic and
                        educationally appropriate.

                    ==================================================
                    RULES
                    ==================================================

                    - NEVER remove existing JSON properties.
                    - NEVER rename JSON properties.
                    - NEVER add unexpected JSON structures.
                    - Keep JSON schema EXACT.
                    - Populate empty fields only.
                    - Improve existing content where appropriate.
                    - Use professional academic language.
                    - Use ANZ education terminology.
                    - Generate detailed content.
                    - Maintain consistency across all sections.

                    ==================================================
                    OUTPUT
                    ==================================================

                    Return VALID JSON ONLY.

                    Do NOT return markdown.

                    Do NOT return explanations.

                    Do NOT wrap JSON in code blocks.

                    Return only the final JSON object.
                    ";
                            }
        // ✅ MOCK AI/Real
        private async Task<string> ExecuteAzureOpenAIAsync(
            string jsonPayload,
            string prompt)
        {
            var endpoint = _config["OpenAI:Endpoint"];
            var key = _config["OpenAI:Key"];
            var deployment = _config["OpenAI:Deployment"];

            var client = new AzureOpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(key));

            var chatClient = client.GetChatClient(deployment);

            var response = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(
            "Return ONLY valid JSON. No markdown."
        ),
        new UserChatMessage(prompt)
            ]);

            var content = string.Join(
                "",
                response.Value.Content.Select(x => x.Text));

            content = content
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            JsonNode.Parse(content); // ✅ validate

            return content;
        }

        // ✅ HTML GENERATION (SharePoint Rich Field)
        private string GenerateHtml(string json)
        {
            var root = JsonNode.Parse(json)?.AsObject();

            if (root == null)
                return "<p>No content</p>";

            var sb = new StringBuilder();

            sb.Append("<div style='font-family:Segoe UI;'>");

            foreach (var section in root)
            {
                sb.Append($"<h2 style='color:#0078d4'>{section.Key}</h2>");

                if (section.Value is JsonObject obj)
                {
                    foreach (var field in obj)
                    {
                        sb.Append($"<p><b>{field.Key}:</b> {field.Value}</p>");
                    }
                }
            }

            sb.Append("</div>");

            return sb.ToString();   // ✅ NO ENCODING
        }
        // ✅ TEXT SUMMARY (SharePoint TEXT FIELD)
        private string GenerateSummary(string json)
        {
            var node = JsonNode.Parse(json);

            return node?["Summary"]?.ToString()
                   ?? "AI-generated course summary.";
        }

        // Helper Method 1-9

        //Run Test From GenerateCourseContentAsync
        public async Task TestAzureOpenAIAsync()
        {
            var endpoint = _config["OpenAI:Endpoint"];
            var key = _config["OpenAI:Key"];
            var deployment = _config["OpenAI:Deployment"];

            var client = new AzureOpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(key));

            var chatClient = client.GetChatClient(deployment);

            var response = await chatClient.CompleteChatAsync(
            [
                new UserChatMessage("Reply ONLY with: HELLO MITANZ")
            ]);

            var content = string.Join("",
                response.Value.Content.Select(x => x.Text));

            Console.WriteLine($"TEST RESPONSE: {content}");
        }
    }

    // ✅ RESULT MODEL (EXTENDED)
    public partial class AIService
    {
        public class AIResult
        {
            public bool Success { get; set; }

            public string Json { get; set; } = string.Empty;

            public string SummaryText { get; set; } = string.Empty;

            public string HtmlContent { get; set; } = string.Empty;

            public string FileName { get; set; } = string.Empty;

            public string? Error { get; set; }

            // NEW
            public string ExecutiveSummary { get; set; } = string.Empty;

            public string StudentGuide { get; set; } = string.Empty;

            public string TutorGuide { get; set; } = string.Empty;

            public string ComplianceReport { get; set; } = string.Empty;
        }
    }
}