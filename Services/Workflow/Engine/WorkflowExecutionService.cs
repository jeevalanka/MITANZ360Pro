using MITANZ360Pro.Web.Models.Workflow;
using MITANZ360Pro.Web.Services.Workflow.Repository;

namespace MITANZ360Pro.Web.Services.Workflow.Engine;

/// <summary>
/// Workflow execution service.
/// </summary>
public sealed class WorkflowExecutionService
{
    private readonly WorkflowRepository _repository;
    private readonly WorkflowEngine _engine;

    public WorkflowExecutionService(
        WorkflowRepository repository,
        WorkflowEngine engine)
    {
        _repository = repository;
        _engine = engine;
    }

    public async Task<List<string>> GetWorkflowsAsync()
    {
        return await _repository.GetFilesAsync();
    }

    public async Task<WorkflowDefinition?> LoadAsync(string fileName)
    {
        return await _repository.LoadAsync(fileName);
    }

    public async Task SaveAsync(WorkflowDefinition workflow)
    {
        await _repository.SaveAsync(workflow);
    }

    public async Task DeleteAsync(string fileName)
    {
        await _repository.DeleteAsync(fileName);
    }

    public async Task<WorkflowContext> ExecuteAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        return await _engine.ExecuteAsync(
            workflow,
            cancellationToken);
    }
}