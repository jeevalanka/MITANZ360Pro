using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Kiota.Abstractions;

namespace MITANZ360Pro.Web.Services;

// ============================================================
// ✅ DOCUMENT VIEWER SUPPORT
//
// Renders library files in-browser instead of forcing a download.
//   - PDF / Image / HTML / Text  -> streamed as-is from Graph
//   - Word / Excel / PowerPoint / Visio -> converted to PDF via
//     Microsoft Graph's built-in "?format=pdf" content conversion,
//     then cached to disk so we never convert the same version twice.
//
// Server-side permission check happens here (not in the UI):
//   - Students may only view Published, non-Archived items.
//   - Tutor / Trainer / Admin / SysAdmin may view everything.
//
// NOTE (integration point for the Enrollment phase): once
// EnrollmentAccessService exists, call
//   enrollmentAccess.EnsureCanViewCourseAsync(user, courseCode)
// inside EnsureViewPermission below, before the Published check.
// Today this method only enforces the publish-state rule.
// ============================================================
public partial class SharePointService
{
    private static readonly HashSet<string> DirectRenderExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".htm", ".html", ".txt"
    };

    private static readonly HashSet<string> ConvertibleToPdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".rtf", ".odt",
        ".xls", ".xlsx", ".ods",
        ".ppt", ".pptx", ".odp",
        ".vsd", ".vsdx"
    };

    // one lock per item so two concurrent viewers of the same file
    // don't trigger two Graph conversions at once
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _conversionLocks = new();

    /// <summary>
    /// Resolves the correct in-browser representation for a library item
    /// (original stream for directly-renderable types, or a cached /
    /// freshly-converted PDF for Office documents), after validating the
    /// caller is allowed to see it.
    /// </summary>
    public async Task<RenderableDocument> GetRenderableDocumentAsync(
        string itemId,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("itemId is required", nameof(itemId));

        var driveId = await GetLmsDriveIdAsync();

        var libraryItem = await GetItemAsync(driveId, itemId)
            ?? throw new FileNotFoundException($"Library item '{itemId}' was not found.");

        EnsureViewPermission(libraryItem, user);

        var ext = Path.GetExtension(libraryItem.Name);

        if (DirectRenderExtensions.Contains(ext))
        {
            var stream = await _graphClient
                .Drives[driveId]
                .Items[itemId]
                .Content
                .GetAsync(cancellationToken: ct);

            return new RenderableDocument(
                stream ?? throw new FileNotFoundException("File content unavailable."),
                GetContentTypeForExtension(ext),
                libraryItem.Name,
                IsConverted: false);
        }

        if (ConvertibleToPdfExtensions.Contains(ext))
        {
            var pdfStream = await GetOrCreateCachedPdfAsync(driveId, itemId, libraryItem.Name, ct);

            return new RenderableDocument(
                pdfStream,
                "application/pdf",
                Path.ChangeExtension(libraryItem.Name, ".pdf"),
                IsConverted: true);
        }

        throw new NotSupportedException(
            $"'{ext}' files cannot be previewed in-browser. Supported: Word, Excel, PowerPoint, Visio, PDF, images, HTML, and text.");
    }

    // ================= PERMISSION CHECK (SERVER-SIDE, NEVER TRUST CLIENT) =================
    private static void EnsureViewPermission(LibraryItem item, ClaimsPrincipal user)
    {
        if (item.IsArchived)
            throw new UnauthorizedAccessException("This item has been archived.");

        var roles = user.Claims
            .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isStaff = roles.Overlaps(new[] { "Tutor", "Trainer", "Admin", "SysAdmin" });

        if (isStaff)
            return;

        // Everyone else (Student, or no recognized role) can only see Published content
        if (!item.IsPublished)
            throw new UnauthorizedAccessException("This item is not published.");
    }

    // ================= PDF CONVERSION + DISK CACHE =================
    private async Task<Stream> GetOrCreateCachedPdfAsync(
        string driveId, string itemId, string originalName, CancellationToken ct)
    {
        // Refresh eTag so the cache key changes whenever the source file changes
        var meta = await _graphClient.Drives[driveId].Items[itemId]
            .GetAsync(req => req.QueryParameters.Select = new[] { "id", "eTag", "lastModifiedDateTime" }, cancellationToken: ct);

        var cacheKey = BuildCacheKey(itemId, meta?.ETag);
        var cachePath = Path.Combine(GetCacheRootPath(), $"{cacheKey}.pdf");

        if (File.Exists(cachePath))
        {
            return File.OpenRead(cachePath);
        }

        var gate = _conversionLocks.GetOrAdd(itemId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);

        try
        {
            // Another request may have finished the conversion while we waited
            if (File.Exists(cachePath))
                return File.OpenRead(cachePath);

            _logger.LogInformation("Converting {ItemId} ({Name}) to PDF via Graph", itemId, originalName);

            using var pdfStream = await RequestPdfConversionAsync(driveId, itemId, ct);

            EvictStaleCacheEntries(itemId, cacheKey);

            await using (var fileStream = File.Create(cachePath))
            {
                await pdfStream.CopyToAsync(fileStream, ct);
            }

            return File.OpenRead(cachePath);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Requests a PDF rendition of an Office file from Microsoft Graph
    /// (GET /drives/{drive-id}/items/{item-id}/content?format=pdf).
    /// The Content endpoint is a raw-stream endpoint, so the "format"
    /// query option is added manually via RequestInformation rather than
    /// a generated QueryParameters type.
    /// </summary>
    private async Task<Stream> RequestPdfConversionAsync(string driveId, string itemId, CancellationToken ct)
    {
        var requestInfo = _graphClient.Drives[driveId].Items[itemId].Content.ToGetRequestInformation();
        requestInfo.QueryParameters["format"] = "pdf";

        var stream = await _graphClient.RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo, cancellationToken: ct);

        return stream ?? throw new InvalidOperationException(
            $"Graph returned no content converting item '{itemId}' to PDF.");
    }

    private static string BuildCacheKey(string itemId, string? eTag)
    {
        var safeTag = string.IsNullOrWhiteSpace(eTag)
            ? "noetag"
            : new string(eTag.Where(char.IsLetterOrDigit).ToArray());

        return $"{itemId}_{safeTag}";
    }

    private static void EvictStaleCacheEntries(string itemId, string currentCacheKey)
    {
        var root = GetCacheRootPath();

        foreach (var file in Directory.EnumerateFiles(root, $"{itemId}_*.pdf"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name != currentCacheKey)
            {
                try { File.Delete(file); } catch { /* best effort */ }
            }
        }
    }

    private static string GetCacheRootPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "mitanz360-doc-cache");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetContentTypeForExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".htm" or ".html" => "text/html",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}

/// <summary>
/// Stream + metadata returned to the /api/documents/render endpoint.
/// Caller is responsible for disposing Stream.
/// </summary>
public sealed record RenderableDocument(Stream Stream, string ContentType, string FileName, bool IsConverted);
