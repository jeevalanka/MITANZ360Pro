using Microsoft.AspNetCore.Mvc;
using MITANZ360Pro.Web.Services.DocumentProcessing;

namespace MITANZ360Pro.Web.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FileController : ControllerBase
    {
        private readonly IFileStreamingService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IFileStreamingService fileService,
            ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        /// <summary>
        /// Streams file from SharePoint via Graph (NO direct SharePoint exposure)
        /// </summary>
        [HttpGet("{itemId}")]
        public async Task<IActionResult> GetFile(
            string itemId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _fileService.GetFileAsync(itemId, cancellationToken);

                // ✅ CRITICAL: Force inline rendering instead of download
                if (Response.Headers.ContainsKey("Content-Disposition"))
                {
                    Response.Headers.Remove("Content-Disposition");
                }

                Response.Headers.Add("Content-Disposition", "inline");

                // ✅ Optional performance headers (good practice)
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return result;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access for file {ItemId}", itemId);
                return StatusCode(403, "Access denied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file {ItemId}", itemId);
                return StatusCode(500, "File retrieval error");
            }
        }
    }
}
