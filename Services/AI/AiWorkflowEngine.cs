using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiWorkflowEngine
{
    private readonly AiGatewayService _gatewayService;
    private readonly AiJsonParserService _jsonParserService;
    private readonly ILogger<AiWorkflowEngine> _logger;

    public AiWorkflowEngine(
        AiGatewayService gatewayService,
        AiJsonParserService jsonParserService,
        ILogger<AiWorkflowEngine> logger)
    {
        _gatewayService = gatewayService;
        _jsonParserService = jsonParserService;
        _logger = logger;
    }

    public async Task<AiWorkflowResult> ExecuteAsync(AiWorkflowRequest request,CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation( "AI Workflow Engine started. TaskType: {TaskType}",
            request.TaskType);

        var result = await _gatewayService.ExecuteAsync( request, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "AI workflow execution failed.");

            return result;
        }

        if (request.StrictJsonResponse)
        {
            var parseResult =
                _jsonParserService.TryParse(
                    result.RawResponse);

            if (!parseResult.Success)
            {
                _logger.LogWarning(
                    "AI response JSON parsing failed.");

                result.Success = false;

                result.Errors.AddRange(
                    parseResult.Errors.Select(e => new AiError
                    {
                        Code = "JSON_PARSE_ERROR",
                        Message = e
                    })
                );
                return result;
            }

            result.Result =
                parseResult.Result;
        }

        _logger.LogInformation("AI Workflow Engine completed successfully.");

        return result;
    }
}
