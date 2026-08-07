using System.Text.Json;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using MITANZ360Pro.Web.Services;

namespace MITANZ360Pro.Web.Modules.Entities.DocumentsLibrary;

/// <summary>
/// Document library business service. Uses SharePointService Graph client patterns
/// against the SharePoint Document Library named "Documents" (no folders).
/// </summary>
public sealed class DocumentLibraryService : IDocumentLibraryService
{
    public const string RequiredDocumentsMetaKey = "RequiredDocuments";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".txt"
    };

    private readonly SharePointService _sharePoint;
    private readonly DocumentLookupDataService _lookups;
    private readonly IEntityService _entityService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentLibraryService> _logger;
    private readonly GraphServiceClient _graph;

    private string? _documentsDriveId;
    private string? _documentsListId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DocumentLibraryService(
        SharePointService sharePoint,
        DocumentLookupDataService lookups,
        IEntityService entityService,
        IConfiguration configuration,
        ILogger<DocumentLibraryService> logger)
    {
        _sharePoint = sharePoint;
        _lookups = lookups;
        _entityService = entityService;
        _configuration = configuration;
        _logger = logger;
        _graph = sharePoint.GraphClient;
    }

    public Task<IReadOnlyList<DocumentCategoryDefinition>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => _lookups.GetCategoriesAsync(cancellationToken);

    public Task<IReadOnlyList<DocumentStatusDefinition>> GetStatusesAsync(CancellationToken cancellationToken = default)
        => _lookups.GetStatusesAsync(cancellationToken);

    public DocumentStatusDefinition? GetStatusDefinition(string code)
    {
        var statuses = _lookups.GetStatusesAsync().GetAwaiter().GetResult();
        return statuses.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public DocumentCategoryDefinition? GetCategoryDefinition(string code)
    {
        var cats = _lookups.GetCategoriesAsync().GetAwaiter().GetResult();
        return cats.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DocumentResult> UploadFileAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateUploadRequest(request);

            if (!IsAllowedFile(request.DocumentCode, request.FileName, request.SizeBytes ?? 0, out var fileError))
                return DocumentResult.Fail(fileError ?? "Invalid file.");

            var existing = await GetLatestVersionAsync(
                request.EntityType,
                request.EntityNumber,
                request.DocumentCode,
                cancellationToken);

            if (existing != null && !request.ReplaceExisting)
            {
                return DocumentResult.Fail(
                    $"A document for code '{request.DocumentCode}' already exists (v{existing.DocumentVersion}). Use Replace to create a new version.");
            }

            if (existing != null && request.ReplaceExisting)
                return await ReplaceFileAsync(existing.DriveItemId, request, cancellationToken);

            return await CreateVersionInternalAsync(request, version: 1, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadFileAsync failed for {Entity}/{Code}", request.EntityNumber, request.DocumentCode);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public async Task<DocumentResult> ReplaceFileAsync(
        string existingDriveItemId,
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateUploadRequest(request);

            if (!IsAllowedFile(request.DocumentCode, request.FileName, request.SizeBytes ?? 0, out var fileError))
                return DocumentResult.Fail(fileError ?? "Invalid file.");

            var current = await GetDocumentAsync(existingDriveItemId, cancellationToken)
                          ?? await GetLatestVersionAsync(request.EntityType, request.EntityNumber, request.DocumentCode, cancellationToken);

            var nextVersion = (current?.DocumentVersion ?? 0) + 1;

            if (current != null)
                await MarkIsLatestAsync(current.DriveItemId, false, cancellationToken);

            request.ReplaceExisting = true;
            return await CreateVersionInternalAsync(request, nextVersion, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReplaceFileAsync failed for {ItemId}", existingDriveItemId);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public Task<DocumentResult> CreateVersionAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default)
    {
        request.ReplaceExisting = true;
        return UploadFileAsync(request, cancellationToken);
    }

    public async Task<DocumentResult> DeleteFileAsync(string driveItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await GetDocumentAsync(driveItemId, cancellationToken);
            if (doc == null)
                return DocumentResult.Fail("Document not found.");

            if (string.Equals(doc.Status, "Verified", StringComparison.OrdinalIgnoreCase))
                return DocumentResult.Fail("Cannot delete a verified document. Reject or replace it first.");

            var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
            await _graph.Drives[driveId].Items[driveItemId].DeleteAsync(cancellationToken: cancellationToken);

            if (doc.IsLatest)
            {
                var history = await GetVersionHistoryAsync(doc.EntityType, doc.EntityNumber, doc.DocumentCode, cancellationToken);
                var next = history.Where(h => h.DriveItemId != driveItemId).OrderByDescending(h => h.DocumentVersion).FirstOrDefault();
                if (next != null)
                    await MarkIsLatestAsync(next.DriveItemId, true, cancellationToken);
            }

            return DocumentResult.Ok(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteFileAsync failed for {ItemId}", driveItemId);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public async Task<DocumentContentResult?> DownloadAsync(string driveItemId, CancellationToken cancellationToken = default)
    {
        var meta = await GetDocumentAsync(driveItemId, cancellationToken);
        if (meta == null)
            return null;

        var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
        var stream = await _graph.Drives[driveId].Items[driveItemId].Content.GetAsync(cancellationToken: cancellationToken);
        if (stream == null)
            return null;

        return new DocumentContentResult
        {
            Stream = stream,
            FileName = meta.FileName,
            ContentType = GuessContentType(meta.FileName, meta.ContentType)
        };
    }

    public Task<DocumentContentResult?> PreviewAsync(string driveItemId, CancellationToken cancellationToken = default)
        => DownloadAsync(driveItemId, cancellationToken);

    public async Task<IReadOnlyList<DocumentMetadata>> GetDocumentsByEntityAsync(
        string entityType,
        string entityNumber,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var filter = new DocumentFilter
        {
            EntityType = entityType,
            EntityNumber = entityNumber,
            IsLatestOnly = latestOnly,
            Active = null,
            PageNumber = 1,
            PageSize = 500
        };

        var result = await SearchAsync(filter, cancellationToken);
        return result.Items;
    }

    public async Task<DocumentMetadata?> GetDocumentAsync(string driveItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driveItemId))
            return null;

        try
        {
            var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
            var item = await _graph.Drives[driveId].Items[driveItemId].GetAsync(cfg =>
            {
                cfg.QueryParameters.Expand = ["listItem($expand=fields)"];
            }, cancellationToken);

            return item == null ? null : MapDriveItem(item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetDocumentAsync failed for {ItemId}", driveItemId);
            return null;
        }
    }

    public async Task<DocumentMetadata?> GetByDocumentCodeAsync(
        string entityType,
        string entityNumber,
        string documentCode,
        bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        var docs = await GetDocumentsByEntityAsync(entityType, entityNumber, latestOnly, cancellationToken);
        return docs.FirstOrDefault(d =>
            string.Equals(d.DocumentCode, documentCode, StringComparison.OrdinalIgnoreCase));
    }

    public Task<DocumentMetadata?> GetLatestVersionAsync(
        string entityType,
        string entityNumber,
        string documentCode,
        CancellationToken cancellationToken = default)
        => GetByDocumentCodeAsync(entityType, entityNumber, documentCode, latestOnly: true, cancellationToken);

    public async Task<IReadOnlyList<DocumentVersionInfo>> GetVersionHistoryAsync(
        string entityType,
        string entityNumber,
        string documentCode,
        CancellationToken cancellationToken = default)
    {
        var filter = new DocumentFilter
        {
            EntityType = entityType,
            EntityNumber = entityNumber,
            DocumentCode = documentCode,
            IsLatestOnly = false,
            Active = null,
            PageNumber = 1,
            PageSize = 200
        };

        var result = await SearchAsync(filter, cancellationToken);
        return result.Items
            .OrderByDescending(x => x.DocumentVersion)
            .Select(x => new DocumentVersionInfo
            {
                DriveItemId = x.DriveItemId,
                FileName = x.FileName,
                DocumentVersion = x.DocumentVersion,
                IsLatest = x.IsLatest,
                Status = x.Status,
                UploadedBy = x.UploadedBy,
                UploadedDate = x.UploadedDate,
                SizeBytes = x.SizeBytes,
                Remarks = x.Remarks
            })
            .ToList();
    }

    public async Task<DocumentResult> UpdateMetadataAsync(
        string driveItemId,
        DocumentMetadataUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var current = await GetDocumentAsync(driveItemId, cancellationToken);
            if (current == null)
                return DocumentResult.Fail("Document not found.");

            var fields = new Dictionary<string, object>
            {
                ["Title"] = update.Title ?? current.Title,
                ["Description"] = update.Description ?? current.Description ?? "",
                ["Status"] = update.Status ?? current.Status,
                ["Remarks"] = update.Remarks ?? current.Remarks ?? "",
                ["Active"] = update.Active ?? current.Active
            };

            if (update.ExpiryDate.HasValue)
                fields["ExpiryDate"] = update.ExpiryDate.Value.ToUniversalTime().ToString("o");
            else if (current.ExpiryDate.HasValue)
                fields["ExpiryDate"] = current.ExpiryDate.Value.ToUniversalTime().ToString("o");

            await PatchFieldsAsync(driveItemId, fields, cancellationToken);
            var refreshed = await GetDocumentAsync(driveItemId, cancellationToken);
            return refreshed == null
                ? DocumentResult.Fail("Updated but failed to reload document.")
                : DocumentResult.Ok(refreshed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateMetadataAsync failed for {ItemId}", driveItemId);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public Task<DocumentResult> RenameAsync(string driveItemId, string newTitle, CancellationToken cancellationToken = default)
        => UpdateMetadataAsync(driveItemId, new DocumentMetadataUpdate { Title = newTitle }, cancellationToken);

    public async Task<DocumentResult> VerifyDocumentAsync(
        string driveItemId,
        string verifiedBy,
        string? remarks = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = new Dictionary<string, object>
            {
                ["Status"] = "Verified",
                ["VerifiedBy"] = verifiedBy,
                ["VerifiedDate"] = DateTime.UtcNow.ToString("o"),
                ["Remarks"] = remarks ?? ""
            };

            await PatchFieldsAsync(driveItemId, fields, cancellationToken);
            var refreshed = await GetDocumentAsync(driveItemId, cancellationToken);
            return refreshed == null
                ? DocumentResult.Fail("Verified but failed to reload document.")
                : DocumentResult.Ok(refreshed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyDocumentAsync failed for {ItemId}", driveItemId);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public async Task<DocumentResult> RejectDocumentAsync(
        string driveItemId,
        string rejectedBy,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remarks))
            return DocumentResult.Fail("Rejection remarks are required.");

        try
        {
            var fields = new Dictionary<string, object>
            {
                ["Status"] = "Rejected",
                ["VerifiedBy"] = rejectedBy,
                ["VerifiedDate"] = DateTime.UtcNow.ToString("o"),
                ["Remarks"] = remarks
            };

            await PatchFieldsAsync(driveItemId, fields, cancellationToken);
            var refreshed = await GetDocumentAsync(driveItemId, cancellationToken);
            return refreshed == null
                ? DocumentResult.Fail("Rejected but failed to reload document.")
                : DocumentResult.Ok(refreshed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RejectDocumentAsync failed for {ItemId}", driveItemId);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public async Task<DocumentSearchResult> SearchAsync(DocumentFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new DocumentFilter();
        var all = await LoadAllDocumentsAsync(cancellationToken);
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(x => string.Equals(x.EntityType, filter.EntityType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.EntityNumber))
            query = query.Where(x => string.Equals(x.EntityNumber, filter.EntityNumber, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.DocumentCode))
            query = query.Where(x => string.Equals(x.DocumentCode, filter.DocumentCode, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => string.Equals(x.Status, filter.Status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(x => string.Equals(x.Category, filter.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.UploadedBy))
            query = query.Where(x =>
                (x.UploadedBy ?? "").Contains(filter.UploadedBy, StringComparison.OrdinalIgnoreCase)
                || (x.UploadedByRole ?? "").Contains(filter.UploadedBy, StringComparison.OrdinalIgnoreCase));

        if (filter.Verified == true)
            query = query.Where(x => string.Equals(x.Status, "Verified", StringComparison.OrdinalIgnoreCase));
        else if (filter.Verified == false)
            query = query.Where(x => !string.Equals(x.Status, "Verified", StringComparison.OrdinalIgnoreCase));

        if (filter.Expired == true)
            query = query.Where(x =>
                (x.ExpiryDate.HasValue && x.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                || string.Equals(x.Status, "Expired", StringComparison.OrdinalIgnoreCase));
        else if (filter.Expired == false)
            query = query.Where(x => !x.ExpiryDate.HasValue || x.ExpiryDate.Value.Date >= DateTime.UtcNow.Date);

        if (filter.IsLatestOnly == true)
            query = query.Where(x => x.IsLatest);

        if (filter.Active == true)
            query = query.Where(x => x.Active);
        else if (filter.Active == false)
            query = query.Where(x => !x.Active);

        if (filter.UploadedFrom.HasValue)
            query = query.Where(x => x.UploadedDate >= filter.UploadedFrom.Value);

        if (filter.UploadedTo.HasValue)
            query = query.Where(x => x.UploadedDate <= filter.UploadedTo.Value);

        if (filter.ExpiryFrom.HasValue)
            query = query.Where(x => x.ExpiryDate >= filter.ExpiryFrom.Value);

        if (filter.ExpiryTo.HasValue)
            query = query.Where(x => x.ExpiryDate <= filter.ExpiryTo.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var q = filter.SearchText.Trim();
            query = query.Where(x =>
                (x.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.FileName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.EntityNumber ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.DocumentCode ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.EntityType ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.Remarks ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var matched = query
            .OrderByDescending(x => x.UploadedDate)
            .ThenBy(x => x.Title)
            .ToList();

        var page = Math.Max(1, filter.PageNumber);
        var size = Math.Clamp(filter.PageSize, 1, 500);
        var items = matched.Skip((page - 1) * size).Take(size).ToList();

        return new DocumentSearchResult
        {
            Items = items,
            TotalCount = matched.Count,
            PageNumber = page,
            PageSize = size
        };
    }

    public async Task<DocumentResult> MarkLatestAsync(string driveItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await GetDocumentAsync(driveItemId, cancellationToken);
            if (doc == null)
                return DocumentResult.Fail("Document not found.");

            var siblings = await GetVersionHistoryAsync(doc.EntityType, doc.EntityNumber, doc.DocumentCode, cancellationToken);
            foreach (var sibling in siblings)
            {
                await MarkIsLatestAsync(sibling.DriveItemId, sibling.DriveItemId == driveItemId, cancellationToken);
            }

            var refreshed = await GetDocumentAsync(driveItemId, cancellationToken);
            return refreshed == null
                ? DocumentResult.Fail("Marked latest but failed to reload.")
                : DocumentResult.Ok(refreshed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkLatestAsync failed for {ItemId}", driveItemId);
            return DocumentResult.Fail(ex.Message);
        }
    }

    public async Task<IReadOnlyList<EntityDocumentViewModel>> GetEntityDocumentMatrixAsync(
        Entity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var required = ReadRequiredDocuments(entity);
        if (required.Count == 0 && !string.IsNullOrWhiteSpace(entity.EntityType))
            required = GetDefaultRequiredDocuments(entity.EntityType).ToList();

        var categories = await GetCategoriesAsync(cancellationToken);
        var statuses = await GetStatusesAsync(cancellationToken);
        var uploaded = string.IsNullOrWhiteSpace(entity.EntityId)
            ? []
            : await GetDocumentsByEntityAsync(entity.EntityType, entity.EntityId, latestOnly: true, cancellationToken);

        var uploadedByCode = uploaded
            .GroupBy(x => x.DocumentCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DocumentVersion).First(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<EntityDocumentViewModel>();

        foreach (var req in required)
        {
            uploadedByCode.TryGetValue(req.Code, out var file);
            var cat = categories.FirstOrDefault(c => string.Equals(c.Code, req.Code, StringComparison.OrdinalIgnoreCase));
            var displayStatus = ResolveDisplayStatus(req, file);
            var statusDef = statuses.FirstOrDefault(s => string.Equals(s.Code, displayStatus, StringComparison.OrdinalIgnoreCase));

            rows.Add(new EntityDocumentViewModel
            {
                DocumentCode = req.Code,
                Title = cat?.Title ?? req.Code,
                Category = cat?.Category ?? "",
                Required = req.Required,
                RequirementStatus = req.Status,
                DisplayStatus = displayStatus,
                StatusColor = statusDef?.Color ?? "#64748B",
                LatestDocument = file
            });
        }

        // Include orphan uploads not in RequiredDocuments
        foreach (var orphan in uploaded.Where(u =>
                     !rows.Any(r => string.Equals(r.DocumentCode, u.DocumentCode, StringComparison.OrdinalIgnoreCase))))
        {
            var cat = categories.FirstOrDefault(c => string.Equals(c.Code, orphan.DocumentCode, StringComparison.OrdinalIgnoreCase));
            var statusDef = statuses.FirstOrDefault(s => string.Equals(s.Code, orphan.Status, StringComparison.OrdinalIgnoreCase));
            rows.Add(new EntityDocumentViewModel
            {
                DocumentCode = orphan.DocumentCode,
                Title = cat?.Title ?? orphan.Title,
                Category = cat?.Category ?? orphan.Category ?? "",
                Required = false,
                RequirementStatus = "NotRequired",
                DisplayStatus = orphan.Status,
                StatusColor = statusDef?.Color ?? "#64748B",
                LatestDocument = orphan
            });
        }

        return rows.OrderBy(r => r.Title).ToList();
    }

    public async Task<ServiceResult> RequestDocumentAsync(
        Entity entity,
        string documentCode,
        bool required = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentCode))
            return ServiceResult.Failure("Document code is required.");

        var list = ReadRequiredDocuments(entity).ToList();
        var existing = list.FirstOrDefault(x => string.Equals(x.Code, documentCode, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Required = required;
            existing.Status = "Requested";
        }
        else
        {
            list.Add(new RequiredDocumentSpec
            {
                Code = documentCode,
                Required = required,
                Status = "Requested"
            });
        }

        WriteRequiredDocuments(entity, list);
        var result = await _entityService.UpdateAsync(entity, "Document requirement requested", cancellationToken);
        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.ErrorMessage ?? "Failed to update entity.");
    }

    public async Task<ServiceResult> RemoveRequirementAsync(
        Entity entity,
        string documentCode,
        CancellationToken cancellationToken = default)
    {
        var list = ReadRequiredDocuments(entity)
            .Where(x => !string.Equals(x.Code, documentCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        WriteRequiredDocuments(entity, list);
        var result = await _entityService.UpdateAsync(entity, "Document requirement removed", cancellationToken);
        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.ErrorMessage ?? "Failed to update entity.");
    }

    public async Task<ServiceResult> UpdateRequirementStatusAsync(
        Entity entity,
        string documentCode,
        string status,
        CancellationToken cancellationToken = default)
    {
        var list = ReadRequiredDocuments(entity).ToList();
        var item = list.FirstOrDefault(x => string.Equals(x.Code, documentCode, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return ServiceResult.Failure("Requirement not found.");

        item.Status = status;
        WriteRequiredDocuments(entity, list);
        var result = await _entityService.UpdateAsync(entity, $"Document requirement status → {status}", cancellationToken);
        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.ErrorMessage ?? "Failed to update entity.");
    }

    public IReadOnlyList<RequiredDocumentSpec> ReadRequiredDocuments(Entity entity)
    {
        if (entity.Metadata == null || !entity.Metadata.TryGetValue(RequiredDocumentsMetaKey, out var raw) || raw == null)
            return [];

        try
        {
            if (raw is JsonElement je)
            {
                return JsonSerializer.Deserialize<List<RequiredDocumentSpec>>(je.GetRawText(), JsonOptions) ?? [];
            }

            if (raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                return JsonSerializer.Deserialize<List<RequiredDocumentSpec>>(s, JsonOptions) ?? [];
            }

            if (raw is List<RequiredDocumentSpec> typed)
                return typed;

            var json = JsonSerializer.Serialize(raw, JsonOptions);
            return JsonSerializer.Deserialize<List<RequiredDocumentSpec>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse RequiredDocuments for {EntityId}", entity.EntityId);
            return [];
        }
    }

    public IReadOnlyList<RequiredDocumentSpec> GetDefaultRequiredDocuments(string entityType)
    {
        if (string.Equals(entityType, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new RequiredDocumentSpec { Code = "PASSPORT", Required = true, Status = "Requested" },
                new RequiredDocumentSpec { Code = "DEGREE", Required = true, Status = "Requested" },
                new RequiredDocumentSpec { Code = "IELTS", Required = false, Status = "NotRequired" },
                new RequiredDocumentSpec { Code = "CV", Required = true, Status = "Requested" },
                new RequiredDocumentSpec { Code = "PHOTO", Required = true, Status = "Requested" }
            ];
        }

        if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entityType, "Person", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new RequiredDocumentSpec { Code = "NIC", Required = true, Status = "Requested" },
                new RequiredDocumentSpec { Code = "CV", Required = true, Status = "Requested" },
                new RequiredDocumentSpec { Code = "PHOTO", Required = false, Status = "NotRequired" }
            ];
        }

        if (string.Equals(entityType, "Agent", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new RequiredDocumentSpec { Code = "NIC", Required = true, Status = "Requested" },
                new RequiredDocumentSpec { Code = "OTHER", Required = true, Status = "Requested" }
            ];
        }

        return [];
    }

    public bool CanStudentUpload(EntityDocumentViewModel row)
        => row.Required
           && !row.HasFile
           && !string.Equals(row.RequirementStatus, "NotRequired", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(row.DisplayStatus, "Verified", StringComparison.OrdinalIgnoreCase);

    public bool CanStudentReplace(EntityDocumentViewModel row)
        => row.HasFile
           && !row.IsVerified
           && (string.Equals(row.DisplayStatus, "Uploaded", StringComparison.OrdinalIgnoreCase)
               || string.Equals(row.DisplayStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
               || string.Equals(row.DisplayStatus, "ReplaceRequired", StringComparison.OrdinalIgnoreCase)
               || string.Equals(row.DisplayStatus, "Pending", StringComparison.OrdinalIgnoreCase));

    public bool CanStudentDelete(EntityDocumentViewModel row)
        => false; // Students cannot delete; especially not verified files

    public bool IsAllowedFile(string documentCode, string fileName, long sizeBytes, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "File name is required.";
            return false;
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            error = $"File type '{ext}' is not allowed. Allowed: PDF, Word, Excel, Images.";
            return false;
        }

        var cat = GetCategoryDefinition(documentCode);
        var maxMb = cat?.MaxSizeMb > 0 ? cat.MaxSizeMb : 20;
        if (sizeBytes > maxMb * 1024L * 1024L)
        {
            error = $"File exceeds maximum size of {maxMb} MB.";
            return false;
        }

        if (cat?.AllowedExtensions is { Count: > 0 }
            && !cat.AllowedExtensions.Any(a => string.Equals(a, ext, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"File type '{ext}' is not allowed for {cat.Title}. Allowed: {string.Join(", ", cat.AllowedExtensions)}";
            return false;
        }

        return true;
    }

    // ----------------- internals -----------------

    private void WriteRequiredDocuments(Entity entity, List<RequiredDocumentSpec> list)
    {
        entity.Metadata[RequiredDocumentsMetaKey] = list;
    }

    private static string ResolveDisplayStatus(RequiredDocumentSpec req, DocumentMetadata? file)
    {
        if (!req.Required || string.Equals(req.Status, "NotRequired", StringComparison.OrdinalIgnoreCase))
            return "NotRequired";

        if (file == null)
            return string.IsNullOrWhiteSpace(req.Status) || req.Status == "Requested" ? "Requested" : req.Status;

        if (file.ExpiryDate.HasValue && file.ExpiryDate.Value.Date < DateTime.UtcNow.Date
            && !string.Equals(file.Status, "Verified", StringComparison.OrdinalIgnoreCase))
            return "Expired";

        return string.IsNullOrWhiteSpace(file.Status) ? "Uploaded" : file.Status;
    }

    private void ValidateUploadRequest(DocumentUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.EntityType))
            throw new ArgumentException("EntityType is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.EntityNumber))
            throw new ArgumentException("EntityNumber is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DocumentCode))
            throw new ArgumentException("DocumentCode is required.", nameof(request));
        if (request.Content == Stream.Null || request.Content == null)
            throw new ArgumentException("File content is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("FileName is required.", nameof(request));
    }

    private async Task<DocumentResult> CreateVersionInternalAsync(
        DocumentUploadRequest request,
        int version,
        CancellationToken cancellationToken)
    {
        var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
        var safeName = BuildStorageFileName(request, version);
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(request.FileName)
            : request.Title;

        // Flat library root — no folders
        DriveItem? uploaded;
        if (request.Content.CanSeek)
            request.Content.Position = 0;

        uploaded = await _graph.Drives[driveId]
            .Root
            .ItemWithPath(safeName)
            .Content
            .PutAsync(request.Content, cancellationToken: cancellationToken);

        if (uploaded?.Id == null)
            return DocumentResult.Fail("Upload failed — no drive item returned.");

        var cat = GetCategoryDefinition(request.DocumentCode);
        var fields = new Dictionary<string, object>
        {
            ["Title"] = title,
            ["Description"] = request.Description ?? "",
            ["EntityType"] = request.EntityType,
            ["EntityNumber"] = request.EntityNumber,
            ["DocumentCode"] = request.DocumentCode,
            ["Status"] = "Uploaded",
            ["Remarks"] = request.Remarks ?? "",
            ["Active"] = true,
            ["DocumentVersion"] = version,
            ["IsLatest"] = true,
            ["UploadedByRole"] = request.UploadedByRole ?? "Admin"
        };

        if (request.ExpiryDate.HasValue)
            fields["ExpiryDate"] = request.ExpiryDate.Value.ToUniversalTime().ToString("o");

        await PatchFieldsAsync(uploaded.Id, fields, cancellationToken);

        // Also try to set UploadedBy if column exists (best-effort)
        try
        {
            if (!string.IsNullOrWhiteSpace(request.UploadedBy))
            {
                await PatchFieldsAsync(uploaded.Id, new Dictionary<string, object>
                {
                    ["UploadedBy"] = request.UploadedBy
                }, cancellationToken);
            }
        }
        catch
        {
            // optional column
        }

        var meta = await GetDocumentAsync(uploaded.Id, cancellationToken);
        if (meta == null)
        {
            meta = new DocumentMetadata
            {
                DriveItemId = uploaded.Id,
                FileName = safeName,
                Title = title,
                EntityType = request.EntityType,
                EntityNumber = request.EntityNumber,
                DocumentCode = request.DocumentCode,
                Status = "Uploaded",
                DocumentVersion = version,
                IsLatest = true,
                Active = true,
                UploadedBy = request.UploadedBy,
                UploadedByRole = request.UploadedByRole,
                UploadedDate = DateTime.UtcNow,
                SizeBytes = request.SizeBytes ?? uploaded.Size,
                Category = cat?.Category,
                WebUrl = uploaded.WebUrl
            };
        }

        _logger.LogInformation(
            "Uploaded entity document {Code} v{Version} for {EntityType}/{EntityNumber} → {ItemId}",
            request.DocumentCode, version, request.EntityType, request.EntityNumber, uploaded.Id);

        return DocumentResult.Ok(meta);
    }

    private static string BuildStorageFileName(DocumentUploadRequest request, int version)
    {
        var ext = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".bin";

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var guid = Guid.NewGuid().ToString("N")[..8];
        var entity = Sanitize(request.EntityNumber);
        var code = Sanitize(request.DocumentCode);
        return $"{Sanitize(request.EntityType)}_{entity}_{code}_v{version}_{stamp}_{guid}{ext.ToLowerInvariant()}";
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "X";
        var chars = value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray();
        return chars.Length == 0 ? "X" : new string(chars);
    }

    private async Task MarkIsLatestAsync(string driveItemId, bool isLatest, CancellationToken cancellationToken)
    {
        await PatchFieldsAsync(driveItemId, new Dictionary<string, object>
        {
            ["IsLatest"] = isLatest
        }, cancellationToken);
    }

    private async Task PatchFieldsAsync(
        string driveItemId,
        Dictionary<string, object> fields,
        CancellationToken cancellationToken)
    {
        var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
        var fieldSet = new FieldValueSet { AdditionalData = fields };

        var listItem = await _graph.Drives[driveId]
            .Items[driveItemId]
            .ListItem
            .GetAsync(cancellationToken: cancellationToken);

        if (listItem?.Id == null)
            throw new InvalidOperationException("Unable to resolve list item for drive item.");

        var listId = await GetDocumentsListIdAsync(cancellationToken);
        var siteId = _configuration["SharePoint:SiteId"]
                     ?? throw new InvalidOperationException("SharePoint SiteId missing.");

        await _graph.Sites[siteId]
            .Lists[listId]
            .Items[listItem.Id]
            .Fields
            .PatchAsync(fieldSet, cancellationToken: cancellationToken);
    }

    private async Task<List<DocumentMetadata>> LoadAllDocumentsAsync(CancellationToken cancellationToken)
    {
        var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
        var results = new List<DocumentMetadata>();

        // Prefer list query when list id available (custom columns)
        try
        {
            var listId = await GetDocumentsListIdAsync(cancellationToken);
            var siteId = _configuration["SharePoint:SiteId"]!;
            string? nextLink = null;

            do
            {
                ListItemCollectionResponse? page;
                if (nextLink != null)
                {
                    var requestInfo = new RequestInformation
                    {
                        HttpMethod = Method.GET,
                        URI = new Uri(nextLink)
                    };
                    page = await _graph.RequestAdapter.SendAsync(
                        requestInfo,
                        ListItemCollectionResponse.CreateFromDiscriminatorValue,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    page = await _graph.Sites[siteId].Lists[listId].Items.GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Expand = ["fields", "driveItem"];
                        cfg.QueryParameters.Top = 100;
                    }, cancellationToken);
                }

                if (page?.Value != null)
                {
                    foreach (var item in page.Value)
                    {
                        var mapped = MapListItem(item);
                        if (mapped != null && !string.IsNullOrWhiteSpace(mapped.DocumentCode))
                            results.Add(mapped);
                    }
                }

                nextLink = page?.OdataNextLink;
            } while (!string.IsNullOrWhiteSpace(nextLink));

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "List-based document load failed; falling back to drive children.");
        }

        // Fallback: drive root children (flat — no folders)
        string? driveNext = null;
        do
        {
            DriveItemCollectionResponse? children;
            if (driveNext != null)
            {
                var requestInfo = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    URI = new Uri(driveNext)
                };
                children = await _graph.RequestAdapter.SendAsync(
                    requestInfo,
                    DriveItemCollectionResponse.CreateFromDiscriminatorValue,
                    cancellationToken: cancellationToken);
            }
            else
            {
                children = await _graph.Drives[driveId].Items["root"].Children.GetAsync(cfg =>
                {
                    cfg.QueryParameters.Top = 100;
                    cfg.QueryParameters.Expand = ["listItem($expand=fields)"];
                }, cancellationToken);
            }

            if (children?.Value != null)
            {
                foreach (var item in children.Value.Where(i => i.File != null))
                {
                    var mapped = MapDriveItem(item);
                    if (!string.IsNullOrWhiteSpace(mapped.DocumentCode))
                        results.Add(mapped);
                }
            }

            driveNext = children?.OdataNextLink;
        } while (!string.IsNullOrWhiteSpace(driveNext));

        return results;
    }

    private DocumentMetadata? MapListItem(ListItem item)
    {
        var fields = item.Fields?.AdditionalData;
        if (fields == null)
            return null;

        var driveItemId = item.DriveItem?.Id
                          ?? GetField(fields, "id")
                          ?? item.Id
                          ?? "";

        var fileName = item.DriveItem?.Name
                       ?? GetField(fields, "FileLeafRef")
                       ?? GetField(fields, "LinkFilename")
                       ?? GetField(fields, "Title")
                       ?? "";

        var code = GetField(fields, "DocumentCode");
        var cat = string.IsNullOrWhiteSpace(code) ? null : GetCategoryDefinition(code);

        return new DocumentMetadata
        {
            DriveItemId = driveItemId,
            ListItemId = item.Id,
            FileName = fileName,
            Title = GetField(fields, "Title") ?? fileName,
            Description = GetField(fields, "Description"),
            EntityType = GetField(fields, "EntityType") ?? "",
            EntityNumber = GetField(fields, "EntityNumber") ?? "",
            DocumentCode = code ?? "",
            Status = GetField(fields, "Status") ?? "Uploaded",
            VerifiedBy = GetField(fields, "VerifiedBy"),
            VerifiedDate = GetDateField(fields, "VerifiedDate"),
            ExpiryDate = GetDateField(fields, "ExpiryDate"),
            Remarks = GetField(fields, "Remarks"),
            Active = GetBoolField(fields, "Active", defaultValue: true),
            DocumentVersion = GetIntField(fields, "DocumentVersion", 1),
            IsLatest = GetBoolField(fields, "IsLatest", defaultValue: true),
            UploadedByRole = GetField(fields, "UploadedByRole"),
            UploadedBy = GetField(fields, "UploadedBy") ?? item.CreatedBy?.User?.DisplayName,
            UploadedDate = item.CreatedDateTime?.UtcDateTime ?? GetDateField(fields, "Created"),
            SizeBytes = item.DriveItem?.Size,
            ContentType = item.DriveItem?.File?.MimeType,
            WebUrl = item.WebUrl ?? item.DriveItem?.WebUrl,
            Category = cat?.Category
        };
    }

    private DocumentMetadata MapDriveItem(DriveItem item)
    {
        var fields = item.ListItem?.Fields?.AdditionalData;
        var code = GetField(fields, "DocumentCode") ?? "";
        var cat = string.IsNullOrWhiteSpace(code) ? null : GetCategoryDefinition(code);

        return new DocumentMetadata
        {
            DriveItemId = item.Id ?? "",
            ListItemId = item.ListItem?.Id,
            FileName = item.Name ?? "",
            Title = GetField(fields, "Title") ?? item.Name ?? "",
            Description = GetField(fields, "Description"),
            EntityType = GetField(fields, "EntityType") ?? "",
            EntityNumber = GetField(fields, "EntityNumber") ?? "",
            DocumentCode = code,
            Status = GetField(fields, "Status") ?? "Uploaded",
            VerifiedBy = GetField(fields, "VerifiedBy"),
            VerifiedDate = GetDateField(fields, "VerifiedDate"),
            ExpiryDate = GetDateField(fields, "ExpiryDate"),
            Remarks = GetField(fields, "Remarks"),
            Active = GetBoolField(fields, "Active", defaultValue: true),
            DocumentVersion = GetIntField(fields, "DocumentVersion", 1),
            IsLatest = GetBoolField(fields, "IsLatest", defaultValue: true),
            UploadedByRole = GetField(fields, "UploadedByRole"),
            UploadedBy = GetField(fields, "UploadedBy") ?? item.CreatedBy?.User?.DisplayName,
            UploadedDate = item.CreatedDateTime?.UtcDateTime,
            SizeBytes = item.Size,
            ContentType = item.File?.MimeType,
            WebUrl = item.WebUrl,
            Category = cat?.Category
        };
    }

    private async Task<string> GetDocumentsDriveIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_documentsDriveId))
            return _documentsDriveId;

        var configured = _configuration["SharePoint:Libraries:Documents"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            // Config may be drive id OR we still verify by listing drives
            _documentsDriveId = configured;
            // If it looks like a list GUID shared incorrectly, resolve by name
            try
            {
                var probe = await _graph.Drives[_documentsDriveId].GetAsync(cancellationToken: cancellationToken);
                if (probe?.Id != null)
                    return _documentsDriveId;
            }
            catch
            {
                _documentsDriveId = null;
            }
        }

        var siteId = _configuration["SharePoint:SiteId"]
                     ?? throw new InvalidOperationException("SharePoint SiteId missing.");

        var drives = await _graph.Sites[siteId].Drives.GetAsync(cancellationToken: cancellationToken);
        var drive = drives?.Value?.FirstOrDefault(d =>
            string.Equals(d.Name, "Documents", StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Name, "Shared Documents", StringComparison.OrdinalIgnoreCase));

        if (drive?.Id == null)
            throw new InvalidOperationException(
                "Unable to resolve SharePoint Document Library 'Documents'. Set SharePoint:Libraries:Documents to the drive id.");

        _documentsDriveId = drive.Id;
        return _documentsDriveId;
    }

    private async Task<string> GetDocumentsListIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_documentsListId))
            return _documentsListId;

        var configured = _configuration["SharePoint:Lists:Documents"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _documentsListId = configured;
            return _documentsListId;
        }

        // Discover from drive
        var driveId = await GetDocumentsDriveIdAsync(cancellationToken);
        var drive = await _graph.Drives[driveId].GetAsync(cancellationToken: cancellationToken);
        // list id often available via sharepoint ids — fallback: lists named Documents
        var siteId = _configuration["SharePoint:SiteId"]!;
        var lists = await _graph.Sites[siteId].Lists.GetAsync(cancellationToken: cancellationToken);
        var list = lists?.Value?.FirstOrDefault(l =>
            string.Equals(l.Name, "Documents", StringComparison.OrdinalIgnoreCase)
            || string.Equals(l.DisplayName, "Documents", StringComparison.OrdinalIgnoreCase));

        if (list?.Id == null)
            throw new InvalidOperationException(
                "Unable to resolve Documents library list id. Set SharePoint:Lists:Documents.");

        _documentsListId = list.Id;
        return _documentsListId;
    }

    private static string? GetField(IDictionary<string, object>? fields, string name)
    {
        if (fields == null || !fields.TryGetValue(name, out var value) || value == null)
            return null;

        return value switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            JsonElement je => je.ToString(),
            _ => value.ToString()
        };
    }

    private static bool GetBoolField(IDictionary<string, object>? fields, string name, bool defaultValue = false)
    {
        if (fields == null || !fields.TryGetValue(name, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            bool b => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static int GetIntField(IDictionary<string, object>? fields, string name, int defaultValue = 0)
    {
        if (fields == null || !fields.TryGetValue(name, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n) => n,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static DateTime? GetDateField(IDictionary<string, object>? fields, string name)
    {
        var raw = GetField(fields, name);
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return DateTime.TryParse(raw, out var dt) ? dt.ToUniversalTime() : null;
    }

    private static string GuessContentType(string fileName, string? fallback)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => string.IsNullOrWhiteSpace(fallback) ? "application/octet-stream" : fallback
        };
    }
}
