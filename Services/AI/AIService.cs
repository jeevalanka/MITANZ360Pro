using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MITANZ360Pro.Web.Services.AI;

public class AIResultDto
{
    public string Html { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Tags { get; set; } = "";

    // ✅ Use correct naming (clean)
    public string Metadata { get; set; } = "";

    // ✅ KEEP these to avoid breaking other services
    public string UpdatedJson { get; set; } = "";
    public decimal Score { get; set; } = 0;
}


public partial class AIService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AIService(
        HttpClient http,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _http = http;
        _config = config;
        _env = env;
    }


    // ✅ AI Engine (http request)
    public async Task<string> GenerateTextAsync(string prompt)
    {
        var endpoint = _config["OpenAI:Endpoint"];
        var key = _config["OpenAI:Key"];
        var deployment = _config["OpenAI:Deployment"];

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(deployment))
        {
            return "⚠ Azure OpenAI configuration missing.";
        }

        var url = $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview";

        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are an AI course designer." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 1500
        };

        var json = JsonSerializer.Serialize(request);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("api-key", key);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"⚠ AI request failed: {response.StatusCode}\n{error}";
        }

        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    // =====================================================
    // ✅ 1: COURSE AI ENGINE (json) | COURSE AI ENGINE (json,promptName)
    public async Task<AIResultDto> GenerateCourseAsync(JsonObject json)
    {
        var prompt = BuildCoursePrompt(json);
        var raw = await GenerateTextAsync(prompt);
        return ParseAIResponse(raw, json);
    }
    public async Task<AIResultDto> GenerateContentAsync(JsonObject metadataJson, string promptName)
    {
        var prompt = await BuildPromptAsync(metadataJson,promptName);
        var raw = await GenerateTextAsync(prompt);
        return ParseAIResponse(raw, metadataJson);
    }
    // =====================================================


    // ✅ PROMPT BUILDER
    private string BuildCoursePrompt(JsonObject json)
    {
        return $@"
You are an expert academic course designer.

Generate output in STRICT JSON format:

{{
  ""summary"": ""short plain text summary"",
  ""html"": ""HTML formatted course report"",
  ""tags"": ""comma separated keywords"",
  ""score"": 0
}}

INPUT:
{json.ToJsonString()}
";
    }
    private async Task<string> BuildPromptAsync(JsonObject metadataJson,string promptName)
    {

        var basePath = _env.WebRootPath;   // ✅ FIXED

        var systemPrompt = await File.ReadAllTextAsync(
            Path.Combine(basePath, "Data", "UniversalSystemPrompt.txt"));

        var stylePrompt = await File.ReadAllTextAsync(
            Path.Combine(basePath, "Data", "Styles", "MicrosoftFluent-Style.txt"));

        var businessPrompt = await File.ReadAllTextAsync(
            Path.Combine(basePath, "Data", "Prompts", $"{promptName}.txt"));

        return $"""
                    {systemPrompt}

                    ================================================

                    {stylePrompt}

                    ================================================

                    {businessPrompt}

                    ================================================

                    COURSE METADATA JSON

                    {metadataJson.ToJsonString()}

                    ================================================
                    """;
    }

    // ✅ PARSER
    private AIResultDto ParseAIResponse(string response, JsonObject input)
    {
        try
        {
            // ✅ Clean AI response (remove markdown blocks if any)
            var clean = response?.Trim() ?? "";

            if (clean.StartsWith("```"))
            {
                clean = clean.Replace("```html", "")
                             .Replace("```", "")
                             .Trim();
            }

            // ✅ Return HTML directly (NO JSON parsing)
            return new AIResultDto
            {
                Summary = "AI-generated content",
                Html = clean,
                Tags = "",
                Score = 0,
                UpdatedJson = input.ToJsonString(),
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ AI RESPONSE ERROR:");
            Console.WriteLine(ex.Message);
            Console.WriteLine(response);

            return new AIResultDto
            {
                Summary = "⚠ AI processing failed",
                Html = "<p>Error processing AI output</p>",
                Tags = "",
                Score = 0,
                UpdatedJson = input.ToJsonString()
            };
        }
    }
}