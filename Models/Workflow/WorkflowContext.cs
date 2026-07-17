namespace MITANZ360Pro.Web.Models.Workflow;

/// <summary>
/// Runtime workflow context shared between all plugins.
/// </summary>
public sealed class WorkflowContext
{
    private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all workflow variables.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Variables => _variables;

    /// <summary>
    /// Sets or updates a workflow variable.
    /// </summary>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _variables[name] = value;
    }

    /// <summary>
    /// Gets a workflow variable.
    /// </summary>
    public T? Get<T>(string name)
    {
        if (_variables.TryGetValue(name, out var value) && value is T result)
            return result;

        return default;
    }

    /// <summary>
    /// Checks whether a variable exists.
    /// </summary>
    public bool Contains(string name)
    {
        return _variables.ContainsKey(name);
    }

    /// <summary>
    /// Removes a variable.
    /// </summary>
    public bool Remove(string name)
    {
        return _variables.Remove(name);
    }

    /// <summary>
    /// Clears the workflow context.
    /// </summary>
    public void Clear()
    {
        _variables.Clear();
    }
}