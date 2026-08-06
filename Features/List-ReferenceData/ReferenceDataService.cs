using MITANZ360Pro.Web.Modules.Entities;

namespace MITANZ360Pro.Web.Modules.ReferenceData;

public interface IReferenceDataService
{
    Task<ReferenceDataPagedResult> GetPagedAsync(
        ReferenceDataFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<ReferenceDataItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string category,
        string code,
        int? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ReferenceDataItem>> CreateAsync(
        ReferenceDataItem item,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ReferenceDataItem>> UpdateAsync(
        ReferenceDataItem item,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<ReferenceDataImportResult>> ImportDefaultsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ReferenceDataService : IReferenceDataService
{
    private readonly IReferenceDataRepository _repository;
    private readonly ILogger<ReferenceDataService> _logger;

    public ReferenceDataService(
        IReferenceDataRepository repository,
        ILogger<ReferenceDataService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ReferenceDataPagedResult> GetPagedAsync(
        ReferenceDataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(x =>
                x.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(x =>
                x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = ApplySort(query, filter.OrderBy);

        var list = query.ToList();
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var pageNumber = Math.Max(1, filter.PageNumber);
        var skip = (pageNumber - 1) * pageSize;

        return new ReferenceDataPagedResult
        {
            Items = list.Skip(skip).Take(pageSize).ToList(),
            TotalCount = list.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all
            .Select(x => x.Category)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public Task<ReferenceDataItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public async Task<bool> ExistsAsync(
        string category,
        string code,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Any(x =>
            (!excludeId.HasValue || x.Id != excludeId.Value) &&
            x.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
            x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ServiceResult<ReferenceDataItem>> CreateAsync(
        ReferenceDataItem item,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(item);
        if (!validation.IsSuccess)
        {
            return ServiceResult<ReferenceDataItem>.Failure(validation.ErrorMessage!);
        }

        try
        {
            if (await ExistsAsync(item.Category, item.Code, cancellationToken: cancellationToken))
            {
                return ServiceResult<ReferenceDataItem>.Failure(
                    $"An item with Category '{item.Category}' and Code '{item.Code}' already exists.");
            }

            Normalize(item);
            var created = await _repository.CreateAsync(item, cancellationToken);
            _logger.LogInformation(
                "Reference data created Id={Id} Category={Category} Code={Code}",
                created.Id,
                created.Category,
                created.Code);

            return ServiceResult<ReferenceDataItem>.Success(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create reference data failed");
            return ServiceResult<ReferenceDataItem>.Failure(FriendlyError(ex));
        }
    }

    public async Task<ServiceResult<ReferenceDataItem>> UpdateAsync(
        ReferenceDataItem item,
        CancellationToken cancellationToken = default)
    {
        if (item.Id <= 0)
        {
            return ServiceResult<ReferenceDataItem>.Failure("Invalid reference item Id.");
        }

        var validation = Validate(item);
        if (!validation.IsSuccess)
        {
            return ServiceResult<ReferenceDataItem>.Failure(validation.ErrorMessage!);
        }

        try
        {
            var existing = await _repository.GetByIdAsync(item.Id, cancellationToken);
            if (existing == null)
            {
                return ServiceResult<ReferenceDataItem>.Failure("Reference item not found.");
            }

            if (await ExistsAsync(item.Category, item.Code, item.Id, cancellationToken))
            {
                return ServiceResult<ReferenceDataItem>.Failure(
                    $"An item with Category '{item.Category}' and Code '{item.Code}' already exists.");
            }

            Normalize(item);
            var updated = await _repository.UpdateAsync(item, cancellationToken);
            _logger.LogInformation("Reference data updated Id={Id}", updated.Id);
            return ServiceResult<ReferenceDataItem>.Success(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update reference data failed for {Id}", item.Id);
            return ServiceResult<ReferenceDataItem>.Failure(FriendlyError(ex));
        }
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ServiceResult.Failure("Invalid reference item Id.");
        }

        try
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return ServiceResult.Failure("Reference item not found.");
            }

            await _repository.DeleteAsync(id, cancellationToken);
            _logger.LogInformation("Reference data deleted Id={Id}", id);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete reference data failed for {Id}", id);
            return ServiceResult.Failure(FriendlyError(ex));
        }
    }

    public async Task<ServiceResult<ReferenceDataImportResult>> ImportDefaultsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new ReferenceDataImportResult();

        try
        {
            var existing = await _repository.GetAllAsync(cancellationToken);
            var existingKeys = existing
                .Select(x => Key(x.Category, x.Code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sortByCategory = existing
                .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(x => x.SortOrder),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var (category, code, title) in ReferenceDataDefaults.GetSeedRows())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = Key(category, code);
                if (existingKeys.Contains(key))
                {
                    result.Skipped++;
                    continue;
                }

                sortByCategory.TryGetValue(category, out var maxSort);
                var nextSort = maxSort + 1;
                sortByCategory[category] = nextSort;

                var item = new ReferenceDataItem
                {
                    Title = title,
                    Code = code,
                    Category = category,
                    Description = title,
                    Icon = "",
                    Color = "",
                    SortOrder = nextSort,
                    IsActive = true,
                    IsDefault = false
                };

                try
                {
                    await _repository.CreateAsync(item, cancellationToken);
                    existingKeys.Add(key);
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"{category}/{code}: {FriendlyError(ex)}");
                    _logger.LogWarning(ex, "Import default failed for {Category}/{Code}", category, code);
                }
            }

            _logger.LogInformation(
                "Reference defaults import complete Imported={Imported} Skipped={Skipped} Errors={Errors}",
                result.Imported,
                result.Skipped,
                result.Errors);

            return ServiceResult<ReferenceDataImportResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import defaults failed");
            return ServiceResult<ReferenceDataImportResult>.Failure(FriendlyError(ex));
        }
    }

    private static IEnumerable<ReferenceDataItem> ApplySort(
        IEnumerable<ReferenceDataItem> query,
        string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title);
        }

        // Radzen may send "Title asc" / "Code desc"
        var parts = orderBy.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var property = parts[0];
        var desc = parts.Length > 1 &&
                   parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<ReferenceDataItem, object?> keySelector = property.ToLowerInvariant() switch
        {
            "title" => x => x.Title,
            "code" => x => x.Code,
            "category" => x => x.Category,
            "description" => x => x.Description,
            "icon" => x => x.Icon,
            "color" => x => x.Color,
            "sortorder" => x => x.SortOrder,
            "isactive" => x => x.IsActive,
            "isdefault" => x => x.IsDefault,
            _ => x => x.Category
        };

        return desc
            ? query.OrderByDescending(keySelector)
            : query.OrderBy(keySelector);
    }

    private static ServiceResult Validate(ReferenceDataItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return ServiceResult.Failure("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Code))
        {
            return ServiceResult.Failure("Code is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Category))
        {
            return ServiceResult.Failure("Category is required.");
        }

        if (item.SortOrder < 0)
        {
            return ServiceResult.Failure("Sort Order must be zero or greater.");
        }

        return ServiceResult.Success();
    }

    private static void Normalize(ReferenceDataItem item)
    {
        item.Title = item.Title.Trim();
        item.Code = item.Code.Trim();
        item.Category = item.Category.Trim();
        item.Description = string.IsNullOrWhiteSpace(item.Description)
            ? item.Title
            : item.Description.Trim();
        item.Icon = item.Icon?.Trim() ?? "";
        item.Color = item.Color?.Trim() ?? "";
    }

    private static string Key(string category, string code)
        => $"{category.Trim()}::{code.Trim()}";

    private static string FriendlyError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("Field '", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("accessDenied", StringComparison.OrdinalIgnoreCase))
        {
            return "SharePoint rejected the request. Check list permissions and field mapping.";
        }

        return string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message;
    }
}
