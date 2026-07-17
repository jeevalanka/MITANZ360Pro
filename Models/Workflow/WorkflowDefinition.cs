using System.Text.Json.Serialization;

namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Root workflow definition.
/// </summary>
public sealed class WorkflowDefinition
{
    /// <summary>
    /// Workflow schema version.
    /// </summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "1.0";

    /// <summary>
    /// Workflow identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Workflow name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Metadata.
    /// </summary>
    public WorkflowMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Data collection steps.
    /// </summary>
    public List<WorkflowStep> DataSources { get; set; } = [];

    /// <summary>
    /// AI processing steps.
    /// </summary>
    public List<WorkflowAiDefinition> AiSteps { get; set; } = [];

    /// <summary>
    /// Workflow output.
    /// </summary>
    public WorkflowOutput Output { get; set; } = new();

    /// <summary>
    /// Post execution actions.
    /// </summary>
    public List<WorkflowAction> Actions { get; set; } = [];
}