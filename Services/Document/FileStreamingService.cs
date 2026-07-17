using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;

namespace MITANZ360Pro.Web.Services.DocumentProcessing
{
    public class FileStreamingService : IFileStreamingService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly IConfiguration _config;
        private readonly ILogger<FileStreamingService> _logger;

        public FileStreamingService(
            GraphServiceClient graphClient,
            IConfiguration config,
            ILogger<FileStreamingService> logger)
        {
            _graphClient = graphClient;
            _config = config;
            _logger = logger;
        }

        public async Task<FileStreamResult> GetFileAsync(
            string itemId,
            CancellationToken cancellationToken = default)
        {
            var driveId = _config["SharePoint:Libraries:LMS:DriveId"];

            if (string.IsNullOrWhiteSpace(driveId))
                throw new InvalidOperationException("DriveId not configured.");

            try
            {
                // ✅ STEP 1 — GET METADATA
                var item = await _graphClient
                    .Drives[driveId]
                    .Items[itemId]
                    .GetAsync(cancellationToken: cancellationToken);

                if (item == null)
                    throw new Exception("File not found");

                // ✅ STEP 2 — SECURITY CHECK
                if (item.AdditionalData != null &&
                    item.AdditionalData.ContainsKey("Published") &&
                    item.AdditionalData["Published"] is bool published &&
                    !published)
                {
                    throw new UnauthorizedAccessException("Content not published");
                }

                var mime = item.File?.MimeType?.ToLower() ?? "";

                // ✅ ✅ STEP 3 — OFFICE FILES → GRAPH PDF CONVERSION (REAL FIX)
                if (mime.Contains("word") ||
                    mime.Contains("presentation") ||
                    mime.Contains("excel"))
                {
                    var convertedStream = await _graphClient
                        .Drives[driveId]
                        .Items[itemId]
                        .Content
                        .GetAsync(requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Format = "pdf";
                        }, cancellationToken);

                    return new FileStreamResult(
                        convertedStream,
                        "application/pdf");

                }

                // ✅ STEP 4 — PDFs DIRECT
                if (mime.Contains("pdf"))
                {
                    var pdfStream = await _graphClient
                        .Drives[driveId]
                        .Items[itemId]
                        .Content
                        .GetAsync(cancellationToken: cancellationToken);

                    return new FileStreamResult(pdfStream, "application/pdf");
                }

                // ✅ STEP 5 — IMAGE
                if (mime.Contains("image"))
                {
                    var imgStream = await _graphClient
                        .Drives[driveId]
                        .Items[itemId]
                        .Content
                        .GetAsync(cancellationToken: cancellationToken);

                    return new FileStreamResult(imgStream, mime);
                }

                // ✅ STEP 6 — VIDEO
                if (mime.Contains("video"))
                {
                    var vidStream = await _graphClient
                        .Drives[driveId]
                        .Items[itemId]
                        .Content
                        .GetAsync(cancellationToken: cancellationToken);

                    return new FileStreamResult(vidStream, mime);
                }
                // ✅ STEP 7 — HTML
                if (mime.Contains("html"))
                {
                    var htmlStream = await _graphClient
                        .Drives[driveId]
                        .Items[itemId]
                        .Content
                        .GetAsync(cancellationToken: cancellationToken);

                    return new FileStreamResult(htmlStream, "text/html");
                }

                // ✅ STEP 8 — DEFAULT
                var rawStream = await _graphClient
                    .Drives[driveId]
                    .Items[itemId]
                    .Content
                    .GetAsync(cancellationToken: cancellationToken);

                return new FileStreamResult(rawStream, "application/octet-stream");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming file {ItemId}", itemId);
                throw;
            }
        }
    }
}