using System.Text.Json.Serialization;

namespace MITANZ360Pro.Web.Modules.Entities.DocumentsLibrary;

/// <summary>Document category definition loaded from Features/Shared/DocumentCategories.json.</summary>
public sealed class DocumentCategoryDefinition
{
    [JsonPropertyName("Code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("Title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("Category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("AllowedExtensions")]
    public List<string> AllowedExtensions { get; set; } = [];

    [JsonPropertyName("MaxSizeMb")]
    public int MaxSizeMb { get; set; } = 10;

    [JsonPropertyName("SortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; } = true;
}

/// <summary>Document status definition loaded from Features/Shared/DocumentStatus.json.</summary>
public sealed class DocumentStatusDefinition
{
    [JsonPropertyName("Code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("Title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("Color")]
    public string Color { get; set; } = "#64748B";

    [JsonPropertyName("SortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; } = true;
}

/// <summary>Required document entry stored in Entity Metadata JSON (RequiredDocuments[]).</summary>
public sealed class RequiredDocumentSpec
{
    [JsonPropertyName("Code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("Required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = "Requested";
}

/// <summary>SharePoint Document Library item metadata for entity documents.</summary>
public sealed class DocumentMetadata
{
    public string DriveItemId { get; set; } = "";

    public string? ListItemId { get; set; }

    public string FileName { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public string EntityType { get; set; } = "";

    public string EntityNumber { get; set; } = "";

    public string DocumentCode { get; set; } = "";

    public string Status { get; set; } = "Uploaded";

    public string? VerifiedBy { get; set; }

    public DateTime? VerifiedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Remarks { get; set; }

    public bool Active { get; set; } = true;

    public int DocumentVersion { get; set; } = 1;

    public bool IsLatest { get; set; } = true;

    public string? UploadedByRole { get; set; }

    public string? UploadedBy { get; set; }

    public DateTime? UploadedDate { get; set; }

    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? WebUrl { get; set; }

    public string? Category { get; set; }
}

/// <summary>Result of a document operation.</summary>
public sealed class DocumentResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public DocumentMetadata? Document { get; set; }

    public static DocumentResult Ok(DocumentMetadata document)
        => new() { Success = true, Document = document };

    public static DocumentResult Fail(string message)
        => new() { Success = false, ErrorMessage = message };
}

/// <summary>Upload / replace request payload.</summary>
public sealed class DocumentUploadRequest
{
    public string EntityType { get; set; } = "";

    public string EntityNumber { get; set; } = "";

    public string DocumentCode { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public string FileName { get; set; } = "";

    public Stream Content { get; set; } = Stream.Null;

    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? UploadedBy { get; set; }

    public string? UploadedByRole { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Remarks { get; set; }

    public bool ReplaceExisting { get; set; }
}

/// <summary>Version history entry.</summary>
public sealed class DocumentVersionInfo
{
    public string DriveItemId { get; set; } = "";

    public string FileName { get; set; } = "";

    public int DocumentVersion { get; set; }

    public bool IsLatest { get; set; }

    public string Status { get; set; } = "";

    public string? UploadedBy { get; set; }

    public DateTime? UploadedDate { get; set; }

    public long? SizeBytes { get; set; }

    public string? Remarks { get; set; }
}

/// <summary>Search / filter criteria for the Documents library.</summary>
public sealed class DocumentFilter
{
    public string? SearchText { get; set; }

    public string? EntityType { get; set; }

    public string? EntityNumber { get; set; }

    public string? DocumentCode { get; set; }

    public string? Status { get; set; }

    public string? Category { get; set; }

    public string? UploadedBy { get; set; }

    public bool? Verified { get; set; }

    public bool? Expired { get; set; }

    public bool? IsLatestOnly { get; set; } = true;

    public bool? Active { get; set; } = true;

    public DateTime? UploadedFrom { get; set; }

    public DateTime? UploadedTo { get; set; }

    public DateTime? ExpiryFrom { get; set; }

    public DateTime? ExpiryTo { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>Paged search result.</summary>
public sealed class DocumentSearchResult
{
    public IReadOnlyList<DocumentMetadata> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }
}

/// <summary>Entity document row combining RequiredDocuments metadata with uploaded files.</summary>
public sealed class EntityDocumentViewModel
{
    public string DocumentCode { get; set; } = "";

    public string Title { get; set; } = "";

    public string Category { get; set; } = "";

    public bool Required { get; set; }

    public string RequirementStatus { get; set; } = "Requested";

    public string DisplayStatus { get; set; } = "Missing";

    public string StatusColor { get; set; } = "#64748B";

    public DocumentMetadata? LatestDocument { get; set; }

    public bool HasFile => LatestDocument != null;

    public bool IsMissing => Required && LatestDocument == null;

    public bool IsVerified =>
        string.Equals(LatestDocument?.Status, "Verified", StringComparison.OrdinalIgnoreCase)
        || string.Equals(DisplayStatus, "Verified", StringComparison.OrdinalIgnoreCase);

    public bool IsRejected =>
        string.Equals(LatestDocument?.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
        || string.Equals(DisplayStatus, "Rejected", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Metadata patch for UpdateMetadataAsync.</summary>
public sealed class DocumentMetadataUpdate
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Remarks { get; set; }

    public bool? Active { get; set; }
}

/// <summary>Download / preview payload.</summary>
public sealed class DocumentContentResult
{
    public Stream Stream { get; set; } = Stream.Null;

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "application/octet-stream";
}
