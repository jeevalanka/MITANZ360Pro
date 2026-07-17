using Microsoft.AspNetCore.Mvc;

namespace MITANZ360Pro.Web.Services.DocumentProcessing
{
    public interface IFileStreamingService
    {
        Task<FileStreamResult> GetFileAsync(
            string itemId,
            CancellationToken cancellationToken = default);
    }
}
