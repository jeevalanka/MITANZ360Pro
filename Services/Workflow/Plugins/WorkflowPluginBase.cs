using Microsoft.Extensions.Logging;
using MITANZ360Pro.Web.Models.Workflow;
using System.Text.Json;

namespace MITANZ360Pro.Web.Services.Workflow.Plugins;

/// <summary>
/// Base class for workflow plugins.
/// </summary>
public abstract class WorkflowPluginBase : IWorkflowPlugin
{
    protected WorkflowPluginBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logger instance.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Plugin type.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// Execute the plugin.
    /// </summary>
    public abstract Task ExecuteAsync(
        WorkflowContext context,
        WorkflowStep step,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a setting value.
    /// </summary>
    protected T? GetSetting<T>(WorkflowStep step, string key)
    {
        if (!step.Settings.TryGetValue(key, out var value) || value is null)
        {
            return default;
        }

        try
        {
            if (value is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(
                    jsonElement.GetRawText());
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}
