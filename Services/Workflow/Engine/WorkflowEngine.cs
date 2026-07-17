using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Engine;

/// <summary>
/// Executes workflow definitions.
/// </summary>
public sealed class WorkflowEngine
{
    private readonly WorkflowPluginManager _pluginManager;

    public WorkflowEngine(WorkflowPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public async Task<WorkflowContext> ExecuteAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var context = new WorkflowContext();

        foreach (var step in workflow.DataSources
                     .Where(x => x.Enabled)
                     .OrderBy(x => x.Order))
        {
            var plugin = _pluginManager.Get(step.Type);

            await plugin.ExecuteAsync(
                context,
                step,
                cancellationToken);
        }

        return context;
    }
}