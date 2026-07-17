using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class OpenRouterService : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterService> _logger;

    public AiProviderType ProviderType =>
        AiProviderType.OpenRouter;

    public OpenRouterService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiWorkflowResult> ExecuteAsync(
        AiWorkflowRequest request,
        AiModelConfig modelConfig,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var apiKey =
                _configuration["OpenRouter:ApiKey"];

            var endpoint =
                $"{_configuration["OpenRouter:BaseUrl"]}/chat/completions";

            var payload = new
            {
                model = modelConfig.Model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = request.SystemPrompt ??
                                  "Return ONLY valid JSON."
                    },
                    new
                    {
                        role = "user",
                        content = request.Prompt
                    }
                }
            };

            var json =
                JsonSerializer.Serialize(payload);

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    endpoint);

            message.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);

            message.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await _httpClient.SendAsync(
                    message,
                    cancellationToken);

            var rawResponse =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            stopwatch.Stop();

            return new AiWorkflowResult
            {
                Success = response.IsSuccessStatusCode,
                Provider = ProviderType,
                Model = modelConfig.Model,
                RawResponse = rawResponse,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "OpenRouter execution failed.");

            return new AiWorkflowResult
            {
                Success = false,
                Provider = ProviderType,
                Model = modelConfig.Model,
                Errors = new List<AiError>
                {
                    new AiError
                    {
                        Code = "OPENROUTER_ERROR",
                        Message = "some error"
                    }
                },
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
