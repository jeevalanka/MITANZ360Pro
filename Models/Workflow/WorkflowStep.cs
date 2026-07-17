namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Represents a workflow step.
/// </summary>
public sealed class WorkflowStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin type (scrape, ai, sql, http, sharepoint, file, email...)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int Order { get; set; }

    public bool ContinueOnError { get; set; }

    public List<string> Inputs { get; set; } = [];

    public string Output { get; set; } = string.Empty;

    public Dictionary<string, object?> Settings { get; set; } = [];
}