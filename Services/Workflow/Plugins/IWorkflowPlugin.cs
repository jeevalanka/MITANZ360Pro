using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// Base interface for all workflow plugins.
/// </summary>
public interface IWorkflowPlugin
{
    /// <summary>
    /// Plugin type.
    /// Example:
    /// scrape
    /// ai
    /// sql
    /// http
    /// sharepoint
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Executes the workflow step.
    /// </summary>
    Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default);
}