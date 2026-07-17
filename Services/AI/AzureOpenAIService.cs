using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AzureOpenAIService : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AiProviderType ProviderType =>
        AiProviderType.AzureOpenAI;

    public AzureOpenAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AzureOpenAIService> logger)
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
            _logger.LogInformation(
                "Azure OpenAI execution started. OutputMode: {OutputMode}",
                request.OutputMode);

            var endpoint =
                _configuration["OpenAI:Endpoint"];

            var deployment =
                _configuration["OpenAI:Deployment"];

            var key =
                _configuration["OpenAI:Key"];

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI endpoint not configured.");
            }

            if (string.IsNullOrWhiteSpace(deployment))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI deployment not configured.");
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI API key not configured.");
            }

            var url =
                $"{endpoint}openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview";

            var payload = new
            {
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content =
                            request.SystemPrompt ??
                            GetSystemPrompt(request.OutputMode)
                    },

                    new
                    {
                        role = "user",
                        content = request.Prompt
                    }
                },

                temperature = request.Temperature,

                max_tokens = request.MaxTokens
            };

            _logger.LogWarning(
                "================================================");

            _logger.LogWarning(
                "SYSTEM PROMPT:\n{SystemPrompt}",
                request.SystemPrompt ??
                GetSystemPrompt(request.OutputMode));

            _logger.LogWarning(
                "USER PROMPT:\n{UserPrompt}",
                request.Prompt);

            _logger.LogWarning(
                "OUTPUT MODE: {OutputMode}",
                request.OutputMode);

            _logger.LogWarning(
                "TEMPERATURE: {Temperature}",
                request.Temperature);

            _logger.LogWarning(
                "MAX TOKENS: {MaxTokens}",
                request.MaxTokens);

            _logger.LogWarning(
                "PROMPT LENGTH: {Length}",
                request.Prompt?.Length ?? 0);

            _logger.LogWarning(
                "================================================");

            var json =
                JsonSerializer.Serialize(payload);

            _logger.LogWarning(
                "REQUEST JSON:\n{Json}",
                json);

            using var requestMessage =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    url);

            requestMessage.Headers.Add(
                "api-key",
                key);

            requestMessage.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await _httpClient.SendAsync(
                    requestMessage,
                    cancellationToken);

            var rawResponse =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            _logger.LogWarning(
                "OPENAI STATUS: {Status}",
                response.StatusCode);

            _logger.LogWarning(
                "OPENAI RESPONSE: {Response}",
                rawResponse);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Azure OpenAI request failed. StatusCode: {StatusCode}",
                    response.StatusCode);

                return new AiWorkflowResult
                {
                    Success = false,
                    Provider = ProviderType,
                    Model = deployment,
                    RawResponse = rawResponse,
                    Errors = new List<AiError>
                    {
                        new AiError
                        {
                            Code = "OPENAI_ERROR",
                            Message = rawResponse
                        }
                    },
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation(
                "Azure OpenAI execution completed successfully.");

            return new AiWorkflowResult
            {
                Success = true,
                Provider = ProviderType,
                Model = deployment,
                RawResponse = rawResponse,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Azure OpenAI execution failed.");

            return new AiWorkflowResult
            {
                Success = false,
                Provider = ProviderType,
                Model = modelConfig.Model,
                Errors = new List<AiError>
                {
                    new AiError
                    {
                        Code = "EXCEPTION",
                        Message = ex.ToString()
                    }
                },
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static string GetSystemPrompt(AiOutputMode outputMode)
    {
        return """
    You are a professional AI assistant for MITANZ360Pro.

    Core Rules:
    - Follow the user's instructions exactly.
    - Respect the user's requested output format.
    - Respect the user's requested layout, design, styling, structure, and presentation requirements.
    - Do not add explanations outside the requested output.
    - If no format is specified, return clean professional plain text.
    - Be accurate, professional, and complete.
    """;
    }
}