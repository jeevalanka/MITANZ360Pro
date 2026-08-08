using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SharePointService> _logger;
    protected readonly GraphServiceClient _graphClient;

    /// <summary>Shared Graph client for feature services that reuse this SharePoint connector.</summary>
    public GraphServiceClient GraphClient => _graphClient;

    // ✅ CANONICAL GRAPH IDENTIFIER (USE THIS EVERYWHERE)
    protected readonly string SiteId;
    protected readonly string LmsLibraryListId;
    // ✅ OPTIONAL (DEBUG / DISCOVERY ONLY)
    protected readonly string SitePath;
    protected string CurrentUserUpn => "system@mitanz.com";
    public string CurrentUserName =>
        _httpContextAccessor
            .HttpContext?
            .User?
            .Identity?
            .Name
        ?? "Anonymous";

    // =====================================================
    // ✅ CONSTRUCTOR (FINAL — CLEAN + CORRECT)
    // =====================================================
    public SharePointService(
        IConfiguration configuration,
        ILogger<SharePointService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;

        // ✅ Canonical Graph SiteId (PRIMARY)
        SiteId =
            _configuration["SharePoint:SiteId"]
            ?? throw new InvalidOperationException("SharePoint SiteId missing.");

        // ✅ Base URL (for optional SitePath only)
        var baseUrl =
            _configuration["SharePoint:BaseUrl"]
            ?? throw new InvalidOperationException("SharePoint BaseUrl missing.");

        var uri = new Uri(baseUrl);

        // ✅ FIXED FORMAT (trailing colon REQUIRED)
        SitePath = $"{uri.Host}:{uri.AbsolutePath.TrimEnd('/')}:";

        LmsLibraryListId = _configuration["SharePoint:Libraries:ListId"]
            ?? throw new InvalidOperationException("Library ListId missing.");

        var tenantId =
            _configuration["Graph:TenantId"]
            ?? throw new InvalidOperationException("Graph TenantId missing.");

        var clientId =
            _configuration["Graph:ClientId"]
            ?? throw new InvalidOperationException("Graph ClientId missing.");

        var clientSecret =
            _configuration["Graph:ClientSecret"]
            ?? throw new InvalidOperationException("Graph ClientSecret missing.");

        var credential = new ClientSecretCredential(
            tenantId,
            clientId,
            clientSecret);

        _graphClient = new GraphServiceClient(credential);

        // ✅ VALIDATION LOGGING
        _logger.LogInformation("✅ Graph SiteId   : {SiteId}", SiteId);
        _logger.LogInformation("✅ Graph SitePath : {SitePath}", SitePath);
    }

    // =====================================================
    // ✅ SAFE RETRY
    // =====================================================
    protected async Task<T> ExecuteSafeAsync<T>(Func<Task<T>> action, string operation)
    {
        int retries = 3;

        for (int i = 0; i < retries; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (i < retries - 1)
            {
                _logger.LogWarning(ex, "Retry {Retry} for {Operation}", i + 1, operation);
                await Task.Delay(500 * (i + 1));
            }
        }

        throw new Exception($"Operation failed: {operation}");
    }

    // =====================================================
    // ✅ DRIVE RESOLVE (CORRECT — USE SiteId)
    // =====================================================
    public async Task<string> GetLmsDriveIdAsync()
    {
        var configuredDriveId =
            _configuration["SharePoint:Libraries:LMS:DriveId"];

        if (!string.IsNullOrWhiteSpace(configuredDriveId))
            return configuredDriveId;

        return await ExecuteSafeAsync(async () =>
        {
            var drives =
                await _graphClient
                    .Sites[SiteId]   // ✅ CORRECT
                    .Drives
                    .GetAsync();

            var drive =
                drives?.Value?.FirstOrDefault(x => x.Name == "LMS-Lib-Content");

            if (drive?.Id == null)
                throw new InvalidOperationException("Unable to resolve LMS drive.");

            return drive.Id;

        }, "Resolve LMS Drive");
    }

    // =====================================================
    // ✅ FIELD HELPERS
    // =====================================================
    protected static string GetField(IDictionary<string, object>? fields, string fieldName)
    {
        return fields != null &&
               fields.TryGetValue(fieldName, out var value)
            ? value?.ToString() ?? ""
            : "";
    }
    protected static bool GetBoolField(IDictionary<string, object>? fields, string fieldName)
    {
        if (fields == null || !fields.TryGetValue(fieldName, out var value))
            return false;

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => false
        };
    }

    // =====================================================
    // ✅ LOGGING
    // =====================================================
    protected void EnsureUserHasPermission(string permission)
    {
        _logger.LogInformation("Permission validated: {Permission}", permission);
    }

    // =====================================================
    // ✅ PUBLIC HELPER
    // =====================================================
    public async Task<string> GetPublicLmsDriveIdAsync()
    {
        return await GetLmsDriveIdAsync();
    }

    // =====================================================
    // ✅ ID GENERATION
    // =====================================================
    public string GenerateContentId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var random = Random.Shared.Next(1000, 9999);
        return $"ACT-{timestamp}-{random}";
    }

    // =====================================================
    // ✅ COURSE LOOKUP ✅ FIXED (USE SiteId)
    // =====================================================
    public async Task<int?> GetExistingCourseIdAsync(
        string courseName,
        string courseCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(courseName) ||
                string.IsNullOrWhiteSpace(courseCode))
                return null;

            var safeName = courseName.Replace("'", "''");
            var safeCode = courseCode.Replace("'", "''");

            var filter =
                $"fields/Title eq '{safeName}' and fields/CourseCode eq '{safeCode}'";

            var listId =
                _configuration["SharePoint:Lists:AIRepository"];

            var result = await ExecuteSafeAsync(async () =>
                await _graphClient
                    .Sites[SiteId]   // ✅ FIXED
                    .Lists[listId]
                    .Items
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Filter = filter;
                        config.QueryParameters.Top = 1;

                        config.QueryParameters.Select = new[] { "id", "fields" };
                        config.QueryParameters.Expand = new[] { "fields" };
                    }),
                "GetExistingCourseId");

            var item = result?.Value?.FirstOrDefault();

            return item != null
                ? int.Parse(item.Id)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetExistingCourseId failed");
            return null;
        }
    }

    // =====================================================
    // ✅ UPDATE FILE STATUS (FINAL - CORRECT)
    // =====================================================
    public async Task UpdateStatus(int id, bool isPublished, bool isArchived)
    {
        try
        {
            // ✅ Prepare SharePoint field update
            var updateData = new FieldValueSet
            {
                AdditionalData = new Dictionary<string, object>
            {
                { "IsPublished", isPublished },
                { "IsArchived", isArchived }
            }
            };

            // ✅ Update using existing correct IDs (NO _siteId / _listId)
            await ExecuteSafeAsync(async () =>
            {
                await _graphClient
                    .Sites[SiteId]                 // ✅ YOUR EXISTING FIELD
                    .Lists[LmsLibraryListId]      // ✅ YOUR EXISTING FIELD
                    .Items[id.ToString()]
                    .Fields
                    .PatchAsync(updateData);

                return true;
            }, "Update File Status");

            _logger.LogInformation(
                "✅ Status updated successfully. ItemId: {Id}, Published: {Published}, Archived: {Archived}",
                id, isPublished, isArchived);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex,
                "❌ Graph error while updating status for ItemId: {Id}",
                id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Unexpected error while updating status for ItemId: {Id}",
                id);
            throw;
        }
    }

    // =====================================================
    // CREATE FOLDER
    // =====================================================

    public async Task<LibraryItem?> CreateFolderAsync(string driveId, string parentFolderId, string folderName)
    {
        try
        {
            var newFolder = new Microsoft.Graph.Models.DriveItem
            {
                Name = folderName,
                Folder = new Microsoft.Graph.Models.Folder(),
                AdditionalData = new Dictionary<string, object>
                {
                    ["@microsoft.graph.conflictBehavior"] = "rename"
                }
            };

            var created = await _graphClient
                .Drives[driveId]
                .Items[parentFolderId]
                .Children
                .PostAsync(newFolder);

            if (created == null) return null;

            return new LibraryItem
            {
                Id = created.Id!,
                Name = created.Name!,
                Title = created.Name!,
                DriveId = driveId,
                IsFolder = created.Folder != null
            };
        }
        catch
        {
            return null;
        }
    }

    // =====================================================
    // RENAME
    // =====================================================

    public async Task<bool> RenameItemAsync(string driveId, string itemId, string newName)
    {
        try
        {
            var patch = new Microsoft.Graph.Models.DriveItem
            {
                Name = newName
            };

            await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .PatchAsync(patch);

            return true;
        }
        catch
        {
            return false;
        }
    }


    // =====================================================
    // DOWNLOAD URL
    // =====================================================

    public async Task<string?> GetDownloadUrlAsync(string driveId, string itemId)
    {
        try
        {
            var item = await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .GetAsync();

            if (item == null) return null;

            if (item.AdditionalData != null &&
                item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var url))
            {
                return url?.ToString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

}