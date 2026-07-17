namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Defines an AI processing step.
/// </summary>
public sealed class WorkflowAiDefinition
{
    /// <summary>
    /// AI step name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// AI provider (AzureOpenAI, OpenAI, Ollama...).
    /// </summary>
    public string Provider { get; set; } = "AzureOpenAI";

    /// <summary>
    /// Model name.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// AI role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// System prompt.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// User prompt.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Additional instructions.
    /// </summary>
    public List<string> Instructions { get; set; } = [];

    /// <summary>
    /// Business rules.
    /// </summary>
    public List<string> BusinessRules { get; set; } = [];

    /// <summary>
    /// Input variables from previous steps.
    /// </summary>
    public List<string> Inputs { get; set; } = [];

    /// <summary>
    /// Output variable.
    /// </summary>
    public string Output { get; set; } = "aiResult";

    /// <summary>
    /// Output format (Json, Html, Markdown, Text...).
    /// </summary>
    public string OutputFormat { get; set; } = "Json";

    /// <summary>
    /// Optional template name.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Temperature.
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum tokens.
    /// </summary>
    public int MaxTokens { get; set; } = 4000;
}