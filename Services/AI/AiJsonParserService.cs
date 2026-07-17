using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiJsonParserService
{
    private readonly ILogger<AiJsonParserService> _logger;

    public AiJsonParserService(
        ILogger<AiJsonParserService> logger)
    {
        _logger = logger;
    }

    public AiJsonParseResult TryParse(
        string rawResponse)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return new AiJsonParseResult
                {
                    Success = false,
                    Errors =
                    [
                        "AI response is empty."
                    ]
                };
            }

            using var document =
                JsonDocument.Parse(rawResponse);

            var json =
                JsonSerializer.Deserialize<object>(
                    rawResponse);

            return new AiJsonParseResult
            {
                Success = true,
                Result = json
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "JSON parsing failed.");

            return new AiJsonParseResult
            {
                Success = false,
                Errors =
                [
                    ex.Message
                ]
            };
        }
    }
}
