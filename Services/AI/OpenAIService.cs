using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class OpenAIService : IAiProvider
{
    private readonly HttpClient _httpClient;

    private readonly IConfiguration _configuration;

    private readonly ILogger<OpenAIService> _logger;

    public AiProviderType ProviderType =>
        AiProviderType.OpenAI;

    public OpenAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIService> logger)
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
        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "OpenAI execution started.");

            var apiKey =
                _configuration["OpenAI:Key"];

            var endpoint =
                _configuration["OpenAI:Endpoint"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key not configured.");
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException(
                    "OpenAI endpoint not configured.");
            }

            var payload = new
            {
                model = modelConfig.Model,

                messages = new object[]
                {
                    new
                    {
                        role = "system",

                        content =
                            request.SystemPrompt ??
                            GetSystemPrompt(
                                request.OutputMode)
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

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "OpenAI request failed. StatusCode: {StatusCode}",
                    response.StatusCode);

                return new AiWorkflowResult
                {
                    Success = false,

                    Provider = ProviderType,

                    Model = modelConfig.Model,

                    RawResponse = rawResponse,

                    Errors = new List<AiError>
                    {
                        new AiError
                        {
                            Code = "OPENAI_ERROR",
                            Message = "OpenAI request failed"
                        }
                    },
                    DurationMs =
                        stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation(
                "OpenAI execution completed successfully.");

            return new AiWorkflowResult
            {
                Success = true,

                Provider = ProviderType,

                Model = modelConfig.Model,

                RawResponse = rawResponse,

                DurationMs =
                    stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "OpenAI execution failed.");

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

                DurationMs =
                    stopwatch.ElapsedMilliseconds
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