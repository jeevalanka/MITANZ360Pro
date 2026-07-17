using System.Text;
using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiStorageService
{
    private readonly ILogger<AiStorageService> _logger;

    public AiStorageService(
        ILogger<AiStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveJsonAsync(
        string filePath,
        string json,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory =
                Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                filePath,
                json,
                Encoding.UTF8,
                cancellationToken);

            _logger.LogInformation(
                "JSON file saved successfully: {FilePath}",
                filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save JSON file.");

            throw;
        }
    }

    public async Task<string> SaveHtmlAsync(
        string filePath,
        string html,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory =
                Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                filePath,
                html,
                Encoding.UTF8,
                cancellationToken);

            _logger.LogInformation(
                "HTML file saved successfully: {FilePath}",
                filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save HTML file.");

            throw;
        }
    }
}
