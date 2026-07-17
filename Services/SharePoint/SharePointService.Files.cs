using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    // =====================================================
    // UPLOAD Course Doc
    // =====================================================
            public async Task UploadCourseDocAsync(
                int itemId,
                byte[] fileBytes,
                string fileName)
            {
                var driveId = await GetLmsDriveIdAsync();

                using var stream = new MemoryStream(fileBytes);

                await _graphClient
                    .Drives[driveId]
                    .Root
                    .ItemWithPath($"AIRepository/{fileName}")
                    .Content
                    .PutAsync(stream);
            }

    // =====================================================
    // DOWNLOAD FILE BY DRIVE ITEM ID
    // =====================================================
    public async Task<Stream> DownloadFileAsync(
        string driveItemId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(driveItemId))
            throw new ArgumentNullException(nameof(driveItemId));

        var driveId = await GetLmsDriveIdAsync();

        _logger.LogInformation(
            "Downloading file | DriveId={DriveId} DriveItemId={DriveItemId}",
            driveId,
            driveItemId);

        return await ExecuteWithRetryAsync(
            async token =>
            {
                var stream =
                    await _graphClient
                        .Drives[driveId]
                        .Items[driveItemId]
                        .Content
                        .GetAsync(
                            requestConfig => { },
                            token);

                if (stream == null)
                    throw new InvalidOperationException(
                        "Downloaded file stream was null.");

                return stream;
            },
            "DownloadFileAsync",
            ct);
    }
    public async Task<Stream?> DownloadFileAsync(string driveId, string itemId)
    {
        try
        {
            var stream = await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .Content
                .GetAsync();

            return stream;
        }
        catch
        {
            return null;
        }
    }

    // =====================================================
    // DOWNLOAD FILE BY FILE NAME (LMS LIB ROOT)
    // =====================================================
    public async Task<Stream> DownloadFileByNameAsync(
        string fileName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var driveId = await GetLmsDriveIdAsync();

        _logger.LogInformation(
            "Resolving DriveItem by name | DriveId={DriveId} File={File}",
            driveId,
            fileName);

        var driveItem =
            await _graphClient
                .Drives[driveId]
                .Root
                .ItemWithPath(fileName)
                .GetAsync(
                    requestConfig => { },
                    ct);

        if (driveItem == null || string.IsNullOrWhiteSpace(driveItem.Id))
            throw new InvalidOperationException(
                $"Drive item not found for file '{fileName}'.");

        _logger.LogInformation(
            "Resolved DriveItemId={DriveItemId} for file={File}",
            driveItem.Id,
            fileName);

        return await DownloadFileAsync(driveItem.Id, ct);
    }

    // =====================================================
    // ✅ READ AI HTML REPORT (AI/LMS/{ContentId}/result.html)
    // =====================================================
    public async Task<string?> GetAIResultHtmlAsync(
        string workflowId,
        string executionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            return null;

        if (string.IsNullOrWhiteSpace(executionId))
            return null;

        var driveId =
            await GetLmsDriveIdAsync();

        var path = $"AI-Results/{workflowId}/{executionId}/result.html";

        _logger.LogInformation(
            "Reading AI HTML report | DriveId={DriveId} Path={Path}",
            driveId,
            path);

        return await ExecuteWithRetryAsync(
            async token =>
            {
                try
                {
                    using var stream =
                        await _graphClient
                            .Drives[driveId]
                            .Root
                            .ItemWithPath(path)
                            .Content
                            .GetAsync(
                                requestConfig => { },
                                token);

                    if (stream == null)
                        return null;

                    using var reader =
                        new StreamReader(
                            stream,
                            Encoding.UTF8);

                    return await reader.ReadToEndAsync();
                }
                catch (ServiceException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "AI HTML report not found | Workflow={WorkflowId} | Execution={ExecutionId}",
                        workflowId,
                        executionId);

                    return null;
                }
            },
            "GetAIResultHtmlAsync",
            ct);
    }
    // =====================================================
    // ✅ READ AI JSON RESULT (AI/LMS/{ContentId}/result.json)
    // =====================================================
    public async Task<string?> GetAIResultJsonAsync(
        string contentId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return null;

        var driveId = await GetLmsDriveIdAsync();
        var path = $"AI/LMS/{contentId}/result.json";

        _logger.LogInformation(
            "Reading AI JSON result | DriveId={DriveId} Path={Path}",
            driveId,
            path);

        return await ExecuteWithRetryAsync(
            async token =>
            {
                try
                {
                    using var stream =
                        await _graphClient
                            .Drives[driveId]
                            .Root
                            .ItemWithPath(path)
                            .Content
                            .GetAsync(
                                requestConfig => { },
                                token);

                    if (stream == null)
                        return null;

                    using var reader =
                        new StreamReader(stream, Encoding.UTF8);

                    return await reader.ReadToEndAsync();
                }
                catch (ServiceException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "AI JSON result not found | ContentId={ContentId}",
                        contentId);

                    return null;
                }
            },
            "GetAIResultJsonAsync",
            ct);
    }

    /// <summary>
    /// Extract raw text content from SharePoint file
    /// </summary>
    public async Task<string> ExtractDocumentContentAsync(
        string driveId,
        string itemId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(driveId) ||
                string.IsNullOrWhiteSpace(itemId))
            {
                return string.Empty;
            }

            var stream = await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .Content
                .GetAsync();

            if (stream == null)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            // ✅ never break UI
            return string.Empty;
        }
    }

    public async Task UploadFileToLibraryAsync(
        string fileName,
        byte[] content,
        CancellationToken ct = default)
    {
        var drive = await GetDefaultDriveAsync(ct);

        if (drive == null)
            throw new Exception("SharePoint drive not found");

        using var stream = new MemoryStream(content);

        var path = $"AIResults/{fileName}";

        await ExecuteWithRetryAsync(
            token => _graphClient
                .Drives[drive.Id]
                .Root
                .ItemWithPath(path)
                .Content
                .PutAsync(stream, cancellationToken: token),
            "UploadFile",
            ct);
    }
    private async Task<Drive?> GetDefaultDriveAsync(CancellationToken ct)
    {
        var drives = await _graphClient
            .Sites[SiteId]
            .Drives
            .GetAsync(cancellationToken: ct);

        return drives?.Value?.FirstOrDefault();
    }

    // =====================================================
    // ✅ GET FILES FROM FOLDER (COURSE BASED)
    // =====================================================
    public async Task<List<LibraryItem>> GetFolderFilesAsync(
        string folderId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            throw new ArgumentNullException(nameof(folderId));

        var driveId = await GetLmsDriveIdAsync();

        _logger.LogInformation(
            "Fetching folder files | DriveId={DriveId} FolderId={FolderId}",
            driveId,
            folderId);

        return await ExecuteWithRetryAsync(
            async token =>
            {
                var result = new List<LibraryItem>();

                var response = await _graphClient
                    .Drives[driveId]
                    .Items[folderId]
                    .Children
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Top = 100;
                        config.QueryParameters.Select = new[]
                        {
                        "id",
                        "name",
                        "size",
                        "file",
                        "folder",
                        "lastModifiedDateTime"
                        };
                    }, token);

                if (response?.Value == null)
                    return result;

                foreach (var item in response.Value)
                {
                    // ✅ Skip folders (only files)
                    if (item.File == null)
                        continue;

                    result.Add(new LibraryItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Size = item.Size ?? 0,
                        LastModified = item.LastModifiedDateTime,
                        ContentType = item.File.MimeType
                    });
                }

                return result;

            },
            "GetFolderFilesAsync",
            ct);
    }


    // =====================================================
    // ✅ CREATE COURSE FOLDER + ALL SUBFOLDERS
    // =====================================================
    public async Task<string> CreateCourseFolderStructureAsync(string courseCode)
    {
        if (string.IsNullOrWhiteSpace(courseCode))
            throw new ArgumentException("CourseCode is required");

        var driveId = await GetLmsDriveIdAsync();

        _logger.LogInformation("Creating full folder structure for {CourseCode}", courseCode);

        // =====================================================
        // ✅ STEP 1: GET ROOT
        // =====================================================
        var root = await _graphClient
            .Drives[driveId]
            .Root
            .GetAsync();

        if (root == null || string.IsNullOrWhiteSpace(root.Id))
            throw new Exception("Unable to resolve root");

        // =====================================================
        // ✅ STEP 2: ENSURE "Courses" BASE FOLDER EXISTS
        // =====================================================
        var coursesFolder = await EnsureFolderAsync(driveId, root.Id, "Courses");

        // =====================================================
        // ✅ STEP 3: CREATE MAIN COURSE FOLDER
        // =====================================================
        var courseFolder = await EnsureFolderAsync(driveId, coursesFolder.Id, courseCode);

        // =====================================================
        // ✅ STEP 4: CREATE SUBFOLDERS
        // =====================================================
        var subFolders = new[]
        {
            "01_Tutor",
            "02_Models",
            "03_Activities",
            "04_Share",
            "05_Assessments"
        };

        foreach (var sub in subFolders)
        {
            await EnsureFolderAsync(driveId, courseFolder.Id, sub);
        }

        _logger.LogInformation("Folder structure created for {CourseCode}", courseCode);

        return courseFolder.Id!;
    }

    // =====================================================
    // ✅ HELPER: ENSURE FOLDER EXISTS (CREATE IF NOT)
    // =====================================================
    private async Task<DriveItem> EnsureFolderAsync(
        string driveId,
        string parentId,
        string folderName)
    {
        try
        {
            // ✅ Try get existing folder
            var existing = await _graphClient
                .Drives[driveId]
                .Items[parentId]
                .ItemWithPath(folderName)
                .GetAsync();

            if (existing != null)
                return existing;
        }
        catch
        {
            // Not found → will create
        }

        // ✅ Create new folder
        var folder = new DriveItem
        {
            Name = folderName,
            Folder = new Folder(),
            AdditionalData = new Dictionary<string, object>
            {
                ["@microsoft.graph.conflictBehavior"] = "rename"
            }
        };

        var created = await _graphClient
            .Drives[driveId]
            .Items[parentId]
            .Children
            .PostAsync(folder);

        if (created == null)
            throw new Exception($"Failed to create folder: {folderName}");

        return created;
    }

    // =====================================================
    // ✅ GET COURSE CONTENT CATEGORIES (SUBFOLDERS)
    // =====================================================
    public async Task<List<LibraryItem>> GetCourseCategoriesAsync(string courseFolderId)
    {
        var driveId = await GetLmsDriveIdAsync();

        var items = await _graphClient
            .Drives[driveId]
            .Items[courseFolderId]
            .Children
            .GetAsync();

        if (items?.Value == null)
            return new();

        return items.Value
            .Where(x => x.Folder != null)   // ✅ ONLY FOLDERS
            .Select(x => new LibraryItem
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToList();
    }

    // =====================================================
    // ✅ GET FILES FROM COURSE CATEGORY FOLDER
    // =====================================================
    public async Task<List<LibraryItem>> GetCourseCategoryFilesAsync(
        string courseFolderId,
        string categoryFolderName)
    {
        if (string.IsNullOrWhiteSpace(courseFolderId))
            throw new ArgumentNullException(nameof(courseFolderId));

        if (string.IsNullOrWhiteSpace(categoryFolderName))
            throw new ArgumentNullException(nameof(categoryFolderName));

        var driveId = await GetLmsDriveIdAsync();

        // ✅ STEP 1: Get category folder (e.g., 02_Models)
        var categoryFolder = await _graphClient
            .Drives[driveId]
            .Items[courseFolderId]
            .ItemWithPath(categoryFolderName)
            .GetAsync();

        if (categoryFolder == null || string.IsNullOrWhiteSpace(categoryFolder.Id))
        {
            _logger.LogWarning(
                "Category folder not found: {Category}",
                categoryFolderName);

            return new();
        }

        // ✅ STEP 2: Get files inside that folder
        var items = await _graphClient
            .Drives[driveId]
            .Items[categoryFolder.Id]
            .Children
            .GetAsync();

        if (items?.Value == null)
            return new();

        var result = new List<LibraryItem>();

        foreach (var item in items.Value)
        {
            // ✅ ONLY FILES (skip subfolders)
            if (item.File == null)
                continue;

            result.Add(new LibraryItem
            {
                Id = item.Id,
                Name = item.Name,
                Size = item.Size ?? 0,
                ContentType = item.File.MimeType,
                LastModified = item.LastModifiedDateTime
            });
        }

        return result;
    }

    // =====================================================
    // ✅ GET FULL FOLDER CONTENT (FILES + SUBFOLDERS)
    // =====================================================
    public async Task<List<LibraryItem>> GetFolderContentAsync(string folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            throw new ArgumentNullException(nameof(folderId));

        var driveId = await GetLmsDriveIdAsync();

        var items = await _graphClient
            .Drives[driveId]
            .Items[folderId]
            .Children
            .GetAsync();

        var result = new List<LibraryItem>();

        if (items?.Value == null)
            return result;

        foreach (var item in items.Value)
        {
            result.Add(new LibraryItem
            {
                Id = item.Id,
                Name = item.Name,
                Size = item.Size ?? 0,
                LastModified = item.LastModifiedDateTime,
                ContentType = item.File?.MimeType,

                // ✅ KEY FLAG
                IsFolder = item.Folder != null
            });
        }

        return result;
    }

}
