using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiGatewayService
{
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly AiRouterService _routerService;
    private readonly ILogger<AiGatewayService> _logger;

    public AiGatewayService(
        IEnumerable<IAiProvider> providers,
        AiRouterService routerService,
        ILogger<AiGatewayService> logger)
    {
        _providers = providers;
        _routerService = routerService;
        _logger = logger;
    }

    public async Task<AiWorkflowResult> ExecuteAsync(
        AiWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "AI Gateway execution started. TaskType: {TaskType}",
            request.TaskType);

        var modelConfig =
            _routerService.ResolveModel(
                request.TaskType);

        var provider =
            _providers.FirstOrDefault(x =>
                x.ProviderType == modelConfig.Provider);

        if (provider is null)
        {
            _logger.LogError(
                "No AI provider found for provider type: {ProviderType}",
                modelConfig.Provider);

            return new AiWorkflowResult
            {
                Success = false,
                TaskType = request.TaskType,
                Provider = modelConfig.Provider,
                Model = modelConfig.Model,
                Errors = new List<AiError>
                    {
                        new AiError
                        {
                            Code = "PROVIDER_NOT_FOUND",
                            Message = $"Provider not registered: {modelConfig.Provider}"
                        }
                    }
            };
        }

        var result =
            await provider.ExecuteAsync(
                request,
                modelConfig,
                cancellationToken);

        result.TaskType =
            request.TaskType;

        result.Provider =
            modelConfig.Provider;

        result.Model =
            modelConfig.Model;

        result.CorrelationId =
            request.CorrelationId;

        _logger.LogInformation(
            "AI Gateway execution completed. Success: {Success}",
            result.Success);

        return result;
    }
}
