using System.Text.Json;

namespace MITANZ360Pro.Web.Services;

public partial class JsonLookupDataService
{
    protected readonly IWebHostEnvironment
        _environment;

    protected readonly ILogger<
        JsonLookupDataService>
        _logger;

    public JsonLookupDataService(
        IWebHostEnvironment environment,
        ILogger<JsonLookupDataService> logger)
    {
        _environment = environment;

        _logger = logger;
    }

    // =====================================================
    // GENERIC JSON LOADER
    // =====================================================

    protected async Task<List<T>>
        LoadAsync<T>(
            string fileName)
    {
        try
        {
            var path =
                Path.Combine(
                    _environment.WebRootPath,
                    "data",
                    fileName);

            if (!File.Exists(path))
            {
                _logger.LogWarning(
                    "JSON lookup file missing: {File}",
                    fileName);

                return [];
            }

            var json =
                await File.ReadAllTextAsync(path);

            return JsonSerializer.Deserialize<
                List<T>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
                ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "JSON lookup load failed.");

            return [];
        }
    }
}