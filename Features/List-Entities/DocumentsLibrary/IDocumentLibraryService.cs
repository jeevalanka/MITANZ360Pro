using MITANZ360Pro.Web.Modules.Entities;

namespace MITANZ360Pro.Web.Modules.Entities.DocumentsLibrary;

/// <summary>
/// Entity Document Library service — SharePoint Documents library backed.
/// Metadata JSON remains the source of required documents; SharePoint remains the source of uploaded files.
/// </summary>
public interface IDocumentLibraryService
{
    Task<IReadOnlyList<DocumentCategoryDefinition>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentStatusDefinition>> GetStatusesAsync(CancellationToken cancellationToken = default);

    DocumentStatusDefinition? GetStatusDefinition(string code);

    DocumentCategoryDefinition? GetCategoryDefinition(string code);

    Task<DocumentResult> UploadFileAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default);

    Task<DocumentResult> ReplaceFileAsync(string existingDriveItemId, DocumentUploadRequest request, CancellationToken cancellationToken = default);

    Task<DocumentResult> DeleteFileAsync(string driveItemId, CancellationToken cancellationToken = default);

    Task<DocumentContentResult?> DownloadAsync(string driveItemId, CancellationToken cancellationToken = default);

    Task<DocumentContentResult?> PreviewAsync(string driveItemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentMetadata>> GetDocumentsByEntityAsync(
        string entityType,
        string entityNumber,
        bool latestOnly = true,
        CancellationToken cancellationToken = default);

    Task<DocumentMetadata?> GetDocumentAsync(string driveItemId, CancellationToken cancellationToken = default);

    Task<DocumentMetadata?> GetByDocumentCodeAsync(
        string entityType,
        string entityNumber,
        string documentCode,
        bool latestOnly = true,
        CancellationToken cancellationToken = default);

    Task<DocumentMetadata?> GetLatestVersionAsync(
        string entityType,
        string entityNumber,
        string documentCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentVersionInfo>> GetVersionHistoryAsync(
        string entityType,
        string entityNumber,
        string documentCode,
        CancellationToken cancellationToken = default);

    Task<DocumentResult> UpdateMetadataAsync(
        string driveItemId,
        DocumentMetadataUpdate update,
        CancellationToken cancellationToken = default);

    Task<DocumentResult> RenameAsync(
        string driveItemId,
        string newTitle,
        CancellationToken cancellationToken = default);

    Task<DocumentResult> VerifyDocumentAsync(
        string driveItemId,
        string verifiedBy,
        string? remarks = null,
        CancellationToken cancellationToken = default);

    Task<DocumentResult> RejectDocumentAsync(
        string driveItemId,
        string rejectedBy,
        string remarks,
        CancellationToken cancellationToken = default);

    Task<DocumentSearchResult> SearchAsync(DocumentFilter filter, CancellationToken cancellationToken = default);

    Task<DocumentResult> CreateVersionAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentResult> MarkLatestAsync(string driveItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Matches Entity Metadata RequiredDocuments[] with uploaded SharePoint files by DocumentCode.
    /// </summary>
    Task<IReadOnlyList<EntityDocumentViewModel>> GetEntityDocumentMatrixAsync(
        Entity entity,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> RequestDocumentAsync(
        Entity entity,
        string documentCode,
        bool required = true,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveRequirementAsync(
        Entity entity,
        string documentCode,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateRequirementStatusAsync(
        Entity entity,
        string documentCode,
        string status,
        CancellationToken cancellationToken = default);

    IReadOnlyList<RequiredDocumentSpec> ReadRequiredDocuments(Entity entity);

    IReadOnlyList<RequiredDocumentSpec> GetDefaultRequiredDocuments(string entityType);

    bool CanStudentUpload(EntityDocumentViewModel row);

    bool CanStudentReplace(EntityDocumentViewModel row);

    bool CanStudentDelete(EntityDocumentViewModel row);

    bool IsAllowedFile(string documentCode, string fileName, long sizeBytes, out string? error);
}
