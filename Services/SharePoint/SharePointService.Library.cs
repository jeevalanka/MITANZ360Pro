using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MITANZ360Pro.Web.Services;

// ============================================================
// ✅ LIBRARY ITEM
// ============================================================
public class LibraryItem
{
    public string Id { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string ParentFolderId { get; set; } = string.Empty;

    public string AllowedRoles { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }

    public long Size { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public string? ContentType { get; set; }

    public string WebUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string ContentTypeCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string CourseModelCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }

    public string MetadataJson { get; set; } = string.Empty;
    public string? AiFeed { get; set; }
    public string ACTMetaJson { get; set; } = string.Empty;
}

// ============================================================
// ✅ ACT METADATA
// ============================================================
public class ActMetadata
{
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public int PassingScore { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsMandatory { get; set; }
}

// ============================================================
// ✅ SHAREPOINT SERVICE
// ============================================================
public partial class SharePointService
{
    // ================= ROOT =================
    public async Task<List<LibraryItem>> GetLibraryRootAsync()
    {
        var driveId = await GetLmsDriveIdAsync();
        return await GetLibraryFolderChildrenAsync(driveId, "root");
    }

    // ================= FOLDER =================
    public async Task<List<LibraryItem>> GetLibraryFolderChildrenAsync(string driveId, string folderId)
    {
        var response =
            await _graphClient
                .Drives[driveId]
                .Items[folderId]
                .Children
                .GetAsync(req =>
                {
                    req.QueryParameters.Expand =
                    [
                        "listItem($expand=fields)"
                    ];
                });

        var results = new List<LibraryItem>();

        foreach (var item in response?.Value ?? new())
        {
            var fields = item.ListItem?.Fields?.AdditionalData;

            results.Add(new LibraryItem
            {
                Id = item.Id ?? "",
                DriveId = driveId,
                Name = item.Name ?? "",
                ParentFolderId = item.ParentReference?.Id ?? "",
                IsFolder = item.Folder != null,

                ContentType = item.File?.MimeType,
                Size = item.Size ?? 0,
                LastModified = item.LastModifiedDateTime,

                Title = GetField(fields, "Title"),
                Description = GetField(fields, "_ExtendedDescription"),
                IsPublished = GetBoolField(fields, "REL_IsPublished"),
                IsArchived = GetBoolField(fields, "ARC_IsArchived"),
                ACTMetaJson = GetField(fields, "ACT_Metadata")
            });
        }

        return results;
    }

    // ================= ✅ FIXED STATUS UPDATE =================
    public async Task UpdateStatus(string driveId, string itemId, bool isPublished, bool isArchived)
    {
        var listItem =
            await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .ListItem
                .GetAsync();

        if (listItem?.Id == null)
            throw new Exception("ListItem not found");

        var fieldValues = new FieldValueSet
        {
            AdditionalData = new Dictionary<string, object>
            {
                ["REL_IsPublished"] = isPublished,
                ["ARC_IsArchived"] = isArchived
            }
        };

        await _graphClient
            .Sites[SiteId]
            .Lists[LmsLibraryListId]
            .Items[listItem.Id]
            .Fields
            .PatchAsync(fieldValues);
    }

    // ================= UPLOAD =================
    public async Task<DriveItem> UploadFileAsync(string driveId, string folderId, string fileName, Stream stream)
    {
        return await _graphClient
            .Drives[driveId]
            .Items[folderId]
            .ItemWithPath(fileName)
            .Content
            .PutAsync(stream);
    }

    // ================= UPDATE FULL =================
    public async Task UpdateLibraryItemAsync(string driveId, string itemId, LibraryItem model)
    {
        var fieldValues = new FieldValueSet
        {
            AdditionalData = new Dictionary<string, object>
            {
                ["REL_IsPublished"] = model.IsPublished,
                ["ARC_IsArchived"] = model.IsArchived,
                ["Title"] = model.Title ?? ""
            }
        };

        var listItem =
            await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .ListItem
                .GetAsync();

        await _graphClient
            .Sites[SiteId]
            .Lists[LmsLibraryListId]
            .Items[listItem.Id]
            .Fields
            .PatchAsync(fieldValues);
    }
    public async Task UpdateLibraryItemFullAsync(string driveId, string itemId, LibraryItem model)
    {
        // ✅ GET list item linked to file
        var listItem = await _graphClient
            .Drives[driveId]
            .Items[itemId]
            .ListItem
            .GetAsync();

        if (listItem?.Id == null)
            throw new Exception("ListItem not found");

        // ✅ FULL FIELD UPDATE (MATCHES YOUR RAZOR PAGE EXACTLY)
        var fieldValues = new FieldValueSet
        {
            AdditionalData = new Dictionary<string, object>
            {
                // ✅ GENERAL
                ["Title"] = model.Title ?? "",
                ["COR_ContentId"] = model.ContentId ?? "",
                ["COR_CourseId"] = model.CourseId ?? "",
                ["_ExtendedDescription"] = model.Description ?? "",
                ["COR_CourseModel"] = model.CourseModelCode ?? "",

                // ✅ GOVERNANCE
                ["COR_ContentType"] = model.ContentTypeCode ?? "",
                ["GOV_Source"] = model.Source ?? "",
                ["REL_IsPublished"] = model.IsPublished,
                ["ARC_IsArchived"] = model.IsArchived,

                // ✅ EXTENDED
                ["EXT_Metadata"] = string.IsNullOrWhiteSpace(model.MetadataJson) ? "{}" : model.MetadataJson,

                // ✅ AI
                ["AI_Feed"] = model.AiFeed ?? "",

                // ✅ ACT
                ["ACT_Metadata"] = string.IsNullOrWhiteSpace(model.ACTMetaJson) ? "{}" : model.ACTMetaJson
            }
        };

        // ✅ PATCH TO SHAREPOINT LIST ITEM
        await _graphClient
            .Sites[SiteId]
            .Lists[LmsLibraryListId]
            .Items[listItem.Id]
            .Fields
            .PatchAsync(fieldValues);
    }

    // ================= GET ITEM =================
    public async Task<LibraryItem?> GetItemAsync(string driveId, string itemId)
    {
        var item = await _graphClient
            .Drives[driveId]
            .Items[itemId]
            .GetAsync(req =>
            {
                req.QueryParameters.Expand =
                [
                    "listItem($expand=fields)"
                ];
            });

        if (item == null) return null;

        var fields = item.ListItem?.Fields?.AdditionalData;

        return new LibraryItem
        {
            // ✅ SYSTEM
            Id = item.Id ?? "",
            DriveId = driveId,
            Name = item.Name ?? "",

            // ✅ FILE INFO (USED IN UI)
            ContentType = item.File?.MimeType,
            Size = item.Size ?? 0,
            LastModified = item.LastModifiedDateTime,
            WebUrl = item.WebUrl ?? "",

            // ✅ GENERAL
            Title = GetField(fields, "Title"),
            ContentId = GetField(fields, "COR_ContentId"),
            CourseId = GetField(fields, "COR_CourseId"),
            Description = GetField(fields, "_ExtendedDescription"),
            CourseModelCode = GetField(fields, "COR_CourseModel"),

            // ✅ METADATA / GOVERNANCE
            ContentTypeCode = GetField(fields, "COR_ContentType"),
            Source = GetField(fields, "GOV_Source"),

            // ✅ FLAGS
            IsPublished = GetBoolField(fields, "REL_IsPublished"),
            IsArchived = GetBoolField(fields, "ARC_IsArchived"),

            // ✅ EXTENDED
            MetadataJson = string.IsNullOrWhiteSpace(GetField(fields, "EXT_Metadata"))
                ? "{}"
                : GetField(fields, "EXT_Metadata"),

            // ✅ AI
            AiFeed = GetField(fields, "AI_Feed"),

            // ✅ ACT
            ACTMetaJson = string.IsNullOrWhiteSpace(GetField(fields, "ACT_Metadata"))
                ? "{}"
                : GetField(fields, "ACT_Metadata")
        };
    }

    // ================= DELETE =================
    public async Task<bool> DeleteItemAsync(string driveId, string itemId)
    {
        try
        {
            await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .DeleteAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for DriveId:{driveId} ItemId:{itemId}", driveId, itemId);
            return false;
        }
    }

    // ================= RECURSIVE FIND =================
    public async Task<LibraryItem?> GetLibraryItemByIdAsync(string itemId)
    {
        var root = await GetLibraryRootAsync();

        foreach (var item in root)
        {
            if (item.Id == itemId) return item;
        }

        return null;
    }

    // ================= CONTEXT =================
    public class LibraryRootContext
    {
        public string DriveId { get; set; } = "";
        public string FolderId { get; set; } = "root";
        public List<LibraryItem> Items { get; set; } = new();
    }

    public async Task<LibraryRootContext> GetRootContextAsync()
    {
        var driveId = await GetLmsDriveIdAsync();
        var items = await GetLibraryFolderChildrenAsync(driveId, "root");

        return new LibraryRootContext
        {
            DriveId = driveId,
            Items = items
        };
    }
    /// <summary>
    /// Returns an LMS library item using the configured LMS Drive.
    /// UI components should use this overload.
    /// </summary>
    public async Task<LibraryItem?> GetItemAsync(string itemId)
    {
        var driveId = await GetLmsDriveIdAsync();

        return await GetItemAsync(driveId, itemId);
    }

    /// <summary>
    /// Staff-only: lists course folders directly under the "Courses" root
    /// of the LMS library (used by CourseSelector for Tutor/Trainer/Admin,
    /// who aren't restricted to their own enrollments).
    /// </summary>
    public async Task<List<LibraryItem>> GetCourseFoldersAsync()
    {
        var root = await GetRootContextAsync();

        var coursesFolder = root.Items.FirstOrDefault(i =>
            i.IsFolder && string.Equals(
                string.IsNullOrWhiteSpace(i.Title) ? i.Name : i.Title,
                "Courses",
                StringComparison.OrdinalIgnoreCase));

        if (coursesFolder == null)
            return new();

        var children = await GetLibraryFolderChildrenAsync(root.DriveId, coursesFolder.Id);

        return children.Where(c => c.IsFolder).ToList();
    }
}