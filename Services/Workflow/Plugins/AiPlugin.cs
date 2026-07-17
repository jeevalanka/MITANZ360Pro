using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;
using MITANZ360Pro.Web.Services.AI;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// AI workflow plugin.
/// </summary>
public sealed class AiPlugin : WorkflowPluginBase
{
    private readonly IConfiguration _configuration;
    private readonly AiWorkflowEngine _aiWorkflowEngine;
    public AiPlugin(
    IConfiguration configuration,
    AiWorkflowEngine aiWorkflowEngine,
    ILogger<AiPlugin> logger)
    : base(logger)
    {
        _configuration = configuration;
        _aiWorkflowEngine = aiWorkflowEngine;
    }

    public override string Type => "ai";

    public override async Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);

        var provider = GetSetting<string>(step, "provider") ?? "AzureOpenAI";
        var model = GetSetting<string>(step, "model") ?? string.Empty;
        var prompt = GetSetting<string>(step, "prompt") ?? string.Empty;

        // TODO:
        // Read Azure OpenAI settings from appsettings.json
        // Build AI request
        // Submit prompt
        // Store response

        foreach (var variable in context.Variables)
        {
            prompt = prompt.Replace(
                $"{{{{{variable.Key}}}}}",
                variable.Value?.ToString() ?? string.Empty);
        }

        var request = new AiWorkflowRequest
        {
            TaskType = AiTaskType.ReportGeneration,
            Prompt = prompt,
            OutputMode = AiOutputMode.Text,
            StrictJsonResponse = false,
            Temperature = 0.2,
            MaxTokens = 4000
        };

        var result =await _aiWorkflowEngine.ExecuteAsync(request,cancellationToken);

        if (!result.Success)
        {
            var errors =string.Join(Environment.NewLine,result.Errors.Select(x => x.Message));
            throw new InvalidOperationException($"AI execution failed: {errors}");
        }
        context.Set( step.Output, ExtractContent(result.RawResponse));
    }
    private static string ExtractContent(string response)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(response);

            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?? response;
        }
        catch
        {
            return response;
        }
    }
}