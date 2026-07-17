using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Drawing;
using System.Text;
using Tesseract;

namespace MITANZ360Pro.Web.Services; // ✅ MUST MATCH CORE SERVICE

public partial class SharePointService
{
    // =====================================================
    // PUBLIC ENTRY POINT
    // =====================================================
    public async Task<string> ExtractTextAsync(
        string fileName,
        Stream fileStream,
        CancellationToken ct = default)
    {
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        var ext = System.IO.Path
            .GetExtension(fileName)
            .ToLowerInvariant();

        return ext switch
        {
            ".pdf" => ExtractPdf(fileStream),
            ".docx" => ExtractDocx(fileStream),
            ".pptx" => ExtractPptx(fileStream),
            ".txt" => await ExtractTxtAsync(fileStream, ct),
            ".png" or ".jpg" or ".jpeg"
                => ExtractImageWithOcr(fileStream),

            _ => throw new NotSupportedException(
                $"File type '{ext}' is not supported for AI analysis.")
        };
    }

    // =====================================================
    // PDF
    // =====================================================
    private static string ExtractPdf(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var sb = new StringBuilder();

        using var pdf = PdfDocument.Open(stream);
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    // =====================================================
    // DOCX
    // =====================================================
    private static string ExtractDocx(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        return doc.MainDocumentPart?.Document?.Body?.InnerText
               ?? string.Empty;
    }

    // =====================================================
    // PPTX
    // =====================================================
    private static string ExtractPptx(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var sb = new StringBuilder();

        using var ppt = PresentationDocument.Open(stream, false);
        var slides = ppt.PresentationPart?.SlideParts ?? [];

        foreach (var slide in slides)
        {
            var texts = slide.Slide
                .Descendants<DocumentFormat.OpenXml.Drawing.Text>();

            foreach (var t in texts)
                sb.AppendLine(t.Text);
        }

        return sb.ToString();
    }

    // =====================================================
    // TXT
    // =====================================================
    private static async Task<string> ExtractTxtAsync(
        Stream stream,
        CancellationToken ct)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return await reader.ReadToEndAsync(ct);
    }

    // =====================================================
    // IMAGE (OCR)
    // =====================================================
    private static readonly Lazy<TesseractEngine> _ocrEngine =
        new(() =>
        {
            var basePath = AppContext.BaseDirectory;
            var tessPath = System.IO.Path.Combine(basePath, "tessdata");

            return new TesseractEngine(
                tessPath,
                "eng",
                EngineMode.Default);
        });

    private static string ExtractImageWithOcr(Stream stream)
    {
        try
        {
            if (stream.CanSeek)
                stream.Position = 0;

            using var img = Pix.LoadFromMemory(ReadAllBytes(stream));
            using var page = _ocrEngine.Value.Process(img);

            return page.GetText();
        }
        catch (TesseractException)
        {
            // OCR failed – return empty text
            // AI can still run on metadata or skip image content
            return string.Empty;
        }
    }
    private static byte[] ReadAllBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}