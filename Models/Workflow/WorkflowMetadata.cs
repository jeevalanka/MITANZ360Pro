namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Stores workflow metadata.
/// </summary>
public sealed class WorkflowMetadata
{
    /// <summary>
    /// Workflow author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Department or business area.
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Workflow category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Workflow tags.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Date created.
    /// </summary>
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date.
    /// </summary>
    public DateTime ModifiedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether this workflow is active.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}