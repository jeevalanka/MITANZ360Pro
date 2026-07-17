
using System.Text.Json;
using MITANZ360Pro.Web.Models;

namespace MITANZ360Pro.Web.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SystemSettingsService> _logger;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    public SystemSettingsService(
        IWebHostEnvironment environment,
        ILogger<SystemSettingsService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    private string GetFilePath()
    {
        return Path.Combine(
            _environment.ContentRootPath,
            "system-settings.json");
    }

    public async Task<SystemSettings> GetSettingsAsync()
    {
        try
        {
            var filePath = GetFilePath();

            if (!File.Exists(filePath))
            {
                _logger.LogWarning(
                    "System settings file not found: {FilePath}",
                    filePath);

                return new SystemSettings();
            }

            var json = await File.ReadAllTextAsync(filePath);

            var settings =
                JsonSerializer.Deserialize<SystemSettings>(
                    json,
                    _jsonOptions);

            return settings ?? new SystemSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load system settings");

            return new SystemSettings();
        }
    }

    public async Task SaveSettingsAsync(SystemSettings settings)
    {
        try
        {
            var filePath = GetFilePath();

            var json =
                JsonSerializer.Serialize(
                    settings,
                    _jsonOptions);

            await File.WriteAllTextAsync(
                filePath,
                json);

            _logger.LogInformation(
                "System settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save system settings");

            throw;
        }
    }
}
public interface ISystemSettingsService
{
    Task<SystemSettings> GetSettingsAsync();

    Task SaveSettingsAsync(SystemSettings settings);
}

