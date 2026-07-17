using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MITANZ360Pro.Web.Services.DocumentProcessing
{
    /// <summary>
    /// DocumentProcessingService
    /// -------------------------
    /// Deterministic document extraction and preparation service.
    ///
    /// RESPONSIBILITIES:
    /// - Extract text from provided document streams
    /// - Normalize and segment large documents into logical chunks
    /// - Perform spelling / language quality analysis
    /// - Detect coverage gaps (missing or weak sections)
    ///
    /// DOES NOT:
    /// - Access SharePoint or Graph
    /// - Perform AI reasoning
    /// - Apply templates
    /// - Persist data
    ///
    /// This service is critical to AI correctness.
    /// </summary>
    public sealed class DocumentProcessingService
    {
        // ======================================================
        // PUBLIC ENTRY POINT
        // ======================================================

        /// <summary>
        /// Processes extracted document text into AI-ready components.
        /// The caller is responsible for providing already-loaded text.
        /// </summary>
        public Task<DocumentProcessingResult> ProcessAsync(
            string rawDocumentText)
        {
            if (string.IsNullOrWhiteSpace(rawDocumentText))
            {
                return Task.FromResult(DocumentProcessingResult.Empty());
            }

            // 1️⃣ Normalize text
            var normalizedText = NormalizeText(rawDocumentText);

            // 2️⃣ Chunk document
            var chunks = ChunkDocument(normalizedText);

            // 3️⃣ Spelling / language analysis
            var spelling = AnalyzeSpelling(normalizedText);

            // 4️⃣ Coverage analysis
            var coverage = AnalyzeCoverage(chunks);

            return Task.FromResult(new DocumentProcessingResult
            {
                Chunks = chunks,
                Spelling = spelling,
                Coverage = coverage
            });
        }

        // ======================================================
        // TEXT NORMALIZATION
        // ======================================================

        private static string NormalizeText(string input)
        {
            // Normalize line endings and whitespace
            var text = input.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }

        // ======================================================
        // DOCUMENT CHUNKING
        // ======================================================

        private static IReadOnlyList<DocumentChunkResult> ChunkDocument(
            string normalizedText)
        {
            var chunks = new List<DocumentChunkResult>();

            var paragraphs = normalizedText
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

            var currentBuilder = new StringBuilder();
            var chunkIndex = 1;

            foreach (var paragraph in paragraphs)
            {
                // Heuristic: treat short uppercase lines as section headers
                if (IsLikelyHeader(paragraph) && currentBuilder.Length > 0)
                {
                    chunks.Add(CreateChunk(chunkIndex++, currentBuilder.ToString()));
                    currentBuilder.Clear();
                }

                currentBuilder.AppendLine(paragraph);
                currentBuilder.AppendLine();
            }

            if (currentBuilder.Length > 0)
            {
                chunks.Add(CreateChunk(chunkIndex, currentBuilder.ToString()));
            }

            return chunks;
        }

        private static bool IsLikelyHeader(string text)
        {
            return text.Length < 80 &&
                   text.ToUpperInvariant() == text &&
                   Regex.IsMatch(text, @"^[A-Z0-9 \-]+$");
        }

        private static DocumentChunkResult CreateChunk(
            int index,
            string content)
        {
            return new DocumentChunkResult
            {
                ChunkId = $"Section-{index}",
                Purpose = "content",
                Content = content.Trim()
            };
        }

        // ======================================================
        // SPELLING & LANGUAGE ANALYSIS
        // ======================================================

        private static SpellingAnalysisResult AnalyzeSpelling(string text)
        {
            var words = Regex.Matches(text, @"\b[a-zA-Z]+\b")
                .Select(m => m.Value)
                .ToList();

            // Very simple heuristic spell check placeholder
            // (Replace later with Hunspell / LanguageTool if needed)
            var misspelled = words
                .Where(w => w.Length > 20) // unrealistic long words as proxy
                .Distinct()
                .Take(10)
                .ToList();

            var totalWords = words.Count;
            var misspelledCount = misspelled.Count;

            return new SpellingAnalysisResult
            {
                TotalWords = totalWords,
                MisspelledWords = misspelledCount,
                ErrorRate = totalWords == 0
                    ? 0
                    : Math.Round((double)misspelledCount / totalWords, 4),
                SampleErrors = misspelled
            };
        }

        // ======================================================
        // COVERAGE ANALYSIS
        // ======================================================

        private static CoverageAnalysisResult AnalyzeCoverage(
            IReadOnlyList<DocumentChunkResult> chunks)
        {
            var sections = new Dictionary<string, string>();

            foreach (var chunk in chunks)
            {
                var status = chunk.Content.Length < 200
                    ? "Weak"
                    : "Present";

                sections[chunk.ChunkId] = status;
            }

            if (sections.Count == 0)
            {
                sections["Document"] = "Missing";
            }

            return new CoverageAnalysisResult
            {
                Sections = sections
            };
        }
    }

    // ======================================================
    // RESULT MODELS (AI-SAFE)
    // ======================================================

    public sealed class DocumentProcessingResult
    {
        public IReadOnlyList<DocumentChunkResult> Chunks { get; init; }
            = Array.Empty<DocumentChunkResult>();

        public SpellingAnalysisResult Spelling { get; init; } = new();

        public CoverageAnalysisResult Coverage { get; init; } = new();

        public static DocumentProcessingResult Empty()
            => new DocumentProcessingResult();
    }

    public sealed class DocumentChunkResult
    {
        public string ChunkId { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    public sealed class SpellingAnalysisResult
    {
        public int TotalWords { get; init; }
        public int MisspelledWords { get; init; }
        public double ErrorRate { get; init; }
        public IReadOnlyList<string> SampleErrors { get; init; }
            = Array.Empty<string>();
    }

    public sealed class CoverageAnalysisResult
    {
        public IReadOnlyDictionary<string, string> Sections { get; init; }
            = new Dictionary<string, string>();
    }
}