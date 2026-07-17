using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    // =====================================================
    // ✅ SITE BUILDER (NEW – CLEAN ACCESS)
    // =====================================================
    protected Microsoft.Graph.Sites.Item.SiteItemRequestBuilder Site()
        => _graphClient.Sites[SiteId];

    // =====================================================
    // 🔐 AUTH
    // =====================================================
    protected bool IsAuthenticated()
        => !string.IsNullOrWhiteSpace(CurrentUserUpn);

    protected string GetUserDisplayName()
        => CurrentUserUpn;

    // =====================================================
    // 🔐 ROLE HELPERS
    // =====================================================
    private bool IsInRole(params string[] roles)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
            return false;

        return roles.Any(r => user.IsInRole(r));
    }

    protected bool HasRequiredRole(params string[] allowed)
        => IsInRole(allowed);

    // =====================================================
    // 🔐 RBAC
    // =====================================================
    protected void EnforceAdmin()
    {
        if (!IsAuthenticated() || !IsInRole("Admin", "SysAdmin"))
        {
            _logger.LogWarning("Unauthorized: Admin required. User={User}", CurrentUserName);
            throw new UnauthorizedAccessException("Admin access required.");
        }
    }

    protected void EnforceAdminOrTrainer()
    {
        if (!IsAuthenticated() || !IsInRole("Admin", "SysAdmin", "Trainer"))
        {
            _logger.LogWarning("Unauthorized: Admin/Trainer required. User={User}", CurrentUserName);
            throw new UnauthorizedAccessException("Admin or Trainer access required.");
        }
    }

    protected void EnforceAcademicAuthority()
    {
        if (!IsAuthenticated() || !IsInRole("Academic", "Admin", "SysAdmin"))
        {
            _logger.LogWarning("Unauthorized: Academic required. User={User}", CurrentUserName);
            throw new UnauthorizedAccessException("Academic authority required.");
        }
    }

    protected void EnforceFinance()
    {
        if (!IsAuthenticated() || !IsInRole("Finance", "Admin", "SysAdmin"))
        {
            _logger.LogWarning("Unauthorized: Finance required. User={User}", CurrentUserName);
            throw new UnauthorizedAccessException("Finance authority required.");
        }
    }

    // =====================================================
    // 📚 LIST CONFIG
    // =====================================================
    protected string CoursesListName =>
        _configuration["SharePoint:Lists:Courses"]
        ?? throw new InvalidOperationException("Missing config: SharePoint:Lists:Courses");

    // =====================================================
    // 🧾 AUDIT
    // =====================================================
    protected Task WriteAuditAsync(string message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AUDIT | {Message} | User={User} | At={AtUtc}",
            message,
            CurrentUserName,
            DateTime.UtcNow);

        return Task.CompletedTask;
    }

    protected Task WriteAuditAsync(string action, string entityId, CancellationToken ct = default)
        => WriteAuditAsync($"{action} | EntityId={entityId}", ct);

    protected Task WriteAuditAsync(string action, string entityType, string entityId, CancellationToken ct = default)
        => WriteAuditAsync($"{action} | {entityType} | EntityId={entityId}", ct);

    // =====================================================
    // 🧼 FIELD NORMALIZATION
    // =====================================================
    protected Dictionary<string, object?> NormalizeFields(Dictionary<string, object?> fields)
        => fields.Where(x => x.Value != null)
                 .ToDictionary(x => x.Key, x => x.Value);

    // =====================================================
    // 🔎 SAFE FIELD READERS
    // =====================================================
    protected string GetString(FieldValueSet fields, string key)
        => fields?.AdditionalData != null &&
           fields.AdditionalData.TryGetValue(key, out var v)
            ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
            : "";

    protected int? GetInt(FieldValueSet fields, string key)
        => fields?.AdditionalData != null &&
           fields.AdditionalData.TryGetValue(key, out var v) &&
           int.TryParse(Convert.ToString(v), out var i)
            ? i
            : null;

    protected bool GetBool(FieldValueSet fields, string key)
        => fields?.AdditionalData != null &&
           fields.AdditionalData.TryGetValue(key, out var v) &&
           bool.TryParse(Convert.ToString(v), out var b)
            ? b
            : false;

    protected DateTime? GetDateTime(FieldValueSet fields, string key)
        => fields?.AdditionalData != null &&
           fields.AdditionalData.TryGetValue(key, out var v) &&
           DateTime.TryParse(Convert.ToString(v), out var d)
            ? d
            : null;

    protected string? GetStringNullable(FieldValueSet fields, string key)
        => fields?.AdditionalData != null &&
           fields.AdditionalData.TryGetValue(key, out var v)
            ? Convert.ToString(v)
            : null;

    protected DateTime? GetDate(FieldValueSet fields, string key)
        => GetDateTime(fields, key);

    // =====================================================
    // 🔐 ODATA SAFETY
    // =====================================================
    protected string EscapeODataString(string value)
        => value?.Replace("'", "''") ?? "";

    // =====================================================
    // 🔁 GRAPH RETRY
    // =====================================================
    protected async Task<T?> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> exec,
        string operation,
        CancellationToken ct)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                return await exec(ct);
            }
            catch (ServiceException ex) when (i < 2)
            {
                _logger.LogWarning(ex, "Retry {Retry} - {Operation}", i + 1, operation);
                await Task.Delay(1000 * (i + 1), ct);
            }
        }

        _logger.LogError("Graph operation failed: {Operation}", operation);
        throw new Exception($"Graph operation failed: {operation}");
    }

    // =====================================================
    // 📚 LIST RESOLVER ✅ FINAL FIXED
    // =====================================================
    protected async Task<string> GetListIdByTitleAsync(
        string listName,
        CancellationToken ct = default)
    {
        var configValue = _configuration[$"SharePoint:Lists:{listName}"];

        if (!string.IsNullOrWhiteSpace(configValue))
            return configValue;

        _logger.LogWarning("Resolving list via Graph: {ListName}", listName);

        var lists = await ExecuteWithRetryAsync(
            async token =>
                await Site()                       // ✅ FIXED HERE
                    .Lists
                    .GetAsync(cancellationToken: token),
            "ResolveListId",
            ct);

        var match = lists?.Value?.FirstOrDefault(l => l.DisplayName == listName);

        if (match?.Id == null)
            throw new InvalidOperationException($"List not found: {listName}");

        return match.Id;
    }

    // =====================================================
    // 🧱 FILE SANITIZER
    // =====================================================
    protected string SanitizePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        var s = input.Replace('\\', '/').Trim();
        s = s.Replace("../", "", StringComparison.Ordinal);

        s = Regex.Replace(s,
            @"[<>:""\|\?\*\u0000-\u001F]",
            "_");

        return s.Trim('/');
    }

    // =====================================================
    // 🧱 ENROLLMENT HELPERS
    // =====================================================
    protected string BuildNote(string actor, string message)
    {
        var stamp = DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture);
        return $"{stamp} [{actor}] {message}";
    }

    protected async Task<string> GenerateEnrollmentCodeAsync()
    {
        await Task.Yield();
        return $"ENR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }

    // =====================================================
    // ✅ PUBLIC AUDIT WRAPPER (REQUIRED FOR AUTOMATION)
    // =====================================================
    public async Task WriteAuditPublicAsync(
        string action,
        string entityType,
        string entityId,
        CancellationToken ct = default)
    {
        await WriteAuditAsync($"{action} | {entityType} | EntityId={entityId}", ct);
    }

}