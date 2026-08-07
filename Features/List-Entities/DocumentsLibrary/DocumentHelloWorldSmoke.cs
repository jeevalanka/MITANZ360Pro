#if false
// disabled
#endif
namespace MITANZ360Pro.Web.Modules.Entities.DocumentsLibrary;

/// <summary>
/// One-shot hello-world exercised from CLI via DocumentLibrarySmokeRunner.
/// </summary>
public static class DocumentLibrarySmokeHelpers
{
    public static async Task<string> RunAsync(
        IDocumentLibraryService docs,
        IEntityService entities,
        CancellationToken ct = default)
    {
        var categories = await docs.GetCategoriesAsync(ct);
        var statuses = await docs.GetStatusesAsync(ct);
        if (categories.Count == 0 || statuses.Count == 0)
            return "FAIL: lookups empty";

        // Find any Student entity
        var page = await entities.GetPagedAsync(new EntityFilter
        {
            EntityType = "Student",
            PageNumber = 1,
            PageSize = 5
        }, ct);

        var entity = page.Items.FirstOrDefault();
        if (entity == null)
            return "FAIL: no Student entity to attach documents";

        var before = await docs.GetEntityDocumentMatrixAsync(entity, ct);
        var requestCode = "OTHER";
        if (before.Any(r => string.Equals(r.DocumentCode, requestCode, StringComparison.OrdinalIgnoreCase)))
            requestCode = "BANK";

        var req = await docs.RequestDocumentAsync(entity, requestCode, required: true, ct);
        if (!req.IsSuccess)
            return $"FAIL: RequestDocumentAsync: {req.ErrorMessage}";

        // Reload entity
        entity = await entities.GetByEntityIdAsync(entity.EntityId, ct) ?? entity;
        var after = await docs.GetEntityDocumentMatrixAsync(entity, ct);
        var row = after.FirstOrDefault(r => string.Equals(r.DocumentCode, requestCode, StringComparison.OrdinalIgnoreCase));
        if (row == null)
            return "FAIL: requested document not in matrix";

        // Search library (may be empty if no uploads yet — still must not throw)
        var search = await docs.SearchAsync(new DocumentFilter
        {
            EntityNumber = entity.EntityId,
            PageSize = 10
        }, ct);

        // Upload a tiny hello-world PDF-like text file for the requested code
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "%PDF-1.1\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\nHello Document Management\n"));
        var upload = await docs.UploadFileAsync(new DocumentUploadRequest
        {
            EntityType = entity.EntityType,
            EntityNumber = entity.EntityId,
            DocumentCode = requestCode,
            Title = $"Hello {requestCode}",
            FileName = $"hello-{requestCode.ToLowerInvariant()}.pdf",
            Content = content,
            ContentType = "application/pdf",
            SizeBytes = content.Length,
            UploadedBy = "doc-smoke",
            UploadedByRole = "Admin",
            ReplaceExisting = true
        }, ct);

        if (!upload.Success)
            return $"PARTIAL request-ok upload-fail: {upload.ErrorMessage} matrix={after.Count}";

        var afterUpload = await docs.GetEntityDocumentMatrixAsync(entity, ct);
        var uploadedRow = afterUpload.FirstOrDefault(r =>
            string.Equals(r.DocumentCode, requestCode, StringComparison.OrdinalIgnoreCase));

        return $"OK entity={entity.EntityId} requested={requestCode} matrix={afterUpload.Count} " +
               $"uploadId={upload.Document?.DriveItemId} status={uploadedRow?.DisplayStatus} " +
               $"file={upload.Document?.FileName} v={upload.Document?.DocumentVersion}";
    }
}
