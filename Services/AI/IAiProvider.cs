namespace MITANZ360Pro.Web.Services.AI;

public interface IAiProvider
{
    AiProviderType ProviderType { get; }

    Task<AiWorkflowResult> ExecuteAsync(
        AiWorkflowRequest request,
        AiModelConfig modelConfig,
        CancellationToken cancellationToken = default);
}
