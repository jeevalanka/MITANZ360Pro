namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Defines an action executed after the workflow completes.
/// </summary>
public sealed class WorkflowAction
{
    /// <summary>
    /// Action name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin type.
    /// Examples:
    /// sharepoint
    /// email
    /// sql
    /// file
    /// webhook
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Enable or disable this action.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Action settings.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];

    /// <summary>
    /// Input variable names.
    /// </summary>
    public List<string> Inputs { get; set; } = [];

    /// <summary>
    /// Output variable.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Continue workflow if this action fails.
    /// </summary>
    public bool ContinueOnError { get; set; }
}