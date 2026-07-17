using Microsoft.Graph.Models;
using System.Collections.Concurrent;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    private static readonly ConcurrentDictionary<string, CacheEntry> _choiceCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private sealed class CacheEntry
    {
        public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
        public DateTime CachedAtUtc { get; init; }
    }

    public async Task<IReadOnlyList<string>> GetChoiceValuesAsync(string listName, string columnName)
    {
        var cacheKey = $"{listName}|{columnName}".ToLowerInvariant();

        if (_choiceCache.TryGetValue(cacheKey, out var cached)
            && DateTime.UtcNow - cached.CachedAtUtc < CacheTtl)
        {
            return cached.Values;
        }

        try
        {
            var columns = await _graphClient
                .Sites[SitePath] // ✅ FIXED
                .Lists[listName]
                .Columns
                .GetAsync();

            var values = columns?.Value?
                .FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                ?.Choice?.Choices?
                .ToList()
                ?? new List<string>();

            _choiceCache[cacheKey] = new CacheEntry
            {
                Values = values,
                CachedAtUtc = DateTime.UtcNow
            };

            return values;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Choice load failed");

            return Array.Empty<string>();
        }
    }

    public void ClearChoiceCache()
    {
        _choiceCache.Clear();
    }
}
