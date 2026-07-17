using System.Text.Json;
using MITANZ360Pro.Web.Models.Workflow;
using Microsoft.AspNetCore.Hosting;

namespace MITANZ360Pro.Web.Services.Workflow.Repository;

/// <summary>
/// Manages workflow JSON files.
/// </summary>
public sealed class WorkflowRepository
{
    private readonly string _workflowFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public WorkflowRepository(IWebHostEnvironment environment)
    {
        _workflowFolder = Path.Combine(
            environment.WebRootPath,
            "AI-Workflow",
            "Workflows");

        Directory.CreateDirectory(_workflowFolder);
    }

    public async Task SaveAsync(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var fileName = $"{workflow.Name}.json";
        var filePath = Path.Combine(_workflowFolder, fileName);

        var json = JsonSerializer.Serialize(workflow, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<WorkflowDefinition?> LoadAsync(string fileName)
    {
        var filePath = Path.Combine(_workflowFolder, fileName);

        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);

        return JsonSerializer.Deserialize<WorkflowDefinition>(json);
    }

    public Task<List<string>> GetFilesAsync()
    {
        var files = Directory
            .GetFiles(_workflowFolder, "*.json")
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .OrderBy(x => x)
            .ToList();

        return Task.FromResult(files);
    }

    public Task DeleteAsync(string fileName)
    {
        var filePath = Path.Combine(_workflowFolder, fileName);

        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }
}