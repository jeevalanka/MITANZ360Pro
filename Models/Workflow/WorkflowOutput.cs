namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Defines the workflow output.
/// </summary>
public sealed class WorkflowOutput
{
    /// <summary>
    /// Output variable name.
    /// </summary>
    public string Name { get; set; } = "result";

    /// <summary>
    /// Output format.
    /// Examples: Json, Html, Markdown, Text, Pdf, Word.
    /// </summary>
    public string Format { get; set; } = "Json";

    /// <summary>
    /// Optional template name.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Save output to file.
    /// </summary>
    public bool SaveToFile { get; set; }

    /// <summary>
    /// Output file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Output folder.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Return result to UI.
    /// </summary>
    public bool ReturnToClient { get; set; } = true;
}