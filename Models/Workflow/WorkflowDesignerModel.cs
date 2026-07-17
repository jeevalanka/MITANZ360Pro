namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Workflow designer model.
/// </summary>
public sealed class WorkflowDesignerModel
{
    public WorkflowDefinition Workflow { get; set; } = new();

    public WorkflowStep? SelectedStep { get; set; }

    public WorkflowAiDefinition? SelectedAiStep { get; set; }

    public WorkflowAction? SelectedAction { get; set; }

    public bool IsDirty { get; set; }

    public string FileName { get; set; } = string.Empty;
}