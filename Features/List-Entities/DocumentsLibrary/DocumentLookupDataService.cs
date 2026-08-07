using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MITANZ360Pro.Web.Modules.Entities.DocumentsLibrary;

/// <summary>Loads DocumentCategories.json and DocumentStatus.json from Features/Shared.</summary>
public sealed class DocumentLookupDataService
{
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocumentLookupDataService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string CategoriesCacheKey = "doc-categories";
    private const string StatusesCacheKey = "doc-statuses";

    public DocumentLookupDataService(
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<DocumentLookupDataService> logger)
    {
        _env = env;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentCategoryDefinition>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CategoriesCacheKey, out IReadOnlyList<DocumentCategoryDefinition>? cached) && cached != null)
            return cached;

        var path = ResolveSharedPath("DocumentCategories.json");
        var items = await LoadAsync<DocumentCategoryDefinition>(path, cancellationToken);
        var active = items.Where(x => x.Active).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToList();
        _cache.Set(CategoriesCacheKey, (IReadOnlyList<DocumentCategoryDefinition>)active, TimeSpan.FromMinutes(10));
        return active;
    }

    public async Task<IReadOnlyList<DocumentStatusDefinition>> GetStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(StatusesCacheKey, out IReadOnlyList<DocumentStatusDefinition>? cached) && cached != null)
            return cached;

        var path = ResolveSharedPath("DocumentStatus.json");
        var items = await LoadAsync<DocumentStatusDefinition>(path, cancellationToken);
        var active = items.Where(x => x.Active).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToList();
        _cache.Set(StatusesCacheKey, (IReadOnlyList<DocumentStatusDefinition>)active, TimeSpan.FromMinutes(10));
        return active;
    }

    private string ResolveSharedPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(_env.ContentRootPath, "Features", "Shared", fileName),
            Path.Combine(AppContext.BaseDirectory, "Features", "Shared", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Features", "Shared", fileName)
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException($"Shared document lookup file not found: {fileName}");
    }

    private async Task<List<T>> LoadAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken);
            return items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load document lookup file {Path}", path);
            throw;
        }
    }
}
