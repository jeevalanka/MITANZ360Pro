using MITANZ360Pro.Web.Services.Workflow.Plugins;

namespace MITANZ360Pro.Web.Services.Workflow.Engine;

/// <summary>
/// Manages workflow plugins.
/// </summary>
public sealed class WorkflowPluginManager
{
    private readonly Dictionary<string, IWorkflowPlugin> _plugins;

    public WorkflowPluginManager(IEnumerable<IWorkflowPlugin> plugins)
    {
        _plugins = plugins.ToDictionary(
            p => p.Type,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a plugin by type.
    /// </summary>
    public IWorkflowPlugin Get(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        if (_plugins.TryGetValue(type, out var plugin))
            return plugin;

        throw new InvalidOperationException(
            $"Workflow plugin '{type}' is not registered.");
    }

    /// <summary>
    /// Determines whether a plugin exists.
    /// </summary>
    public bool Exists(string type)
    {
        return _plugins.ContainsKey(type);
    }

    /// <summary>
    /// Returns all registered plugins.
    /// </summary>
    public IReadOnlyCollection<IWorkflowPlugin> GetAll()
    {
        return _plugins.Values.ToList().AsReadOnly();
    }
}