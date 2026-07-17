using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Application.Services
{
    public partial class AutomationService
    {
        private readonly ILogger<AutomationService> _logger;

        public AutomationService(ILogger<AutomationService> logger)
        {
            _logger = logger;
        }

        public ValidationResult ValidateContentRelevance(
            Dictionary<string, string> metadata,
            string content)
        {
            var result = new ValidationResult();

            try
            {
                // ✅ STEP 1: Extract with fallback support
                var title = GetMetadataValue(metadata, "CourseTitle", "Title", "CourseName");
                var description = GetMetadataValue(metadata, "CourseDescription", "Description", "CourseOverview");
                var learningOutcomes = GetMetadataValue(metadata, "CourseLearningOutcomes", "LearningOutcomes", "LearningOutcome");

                // ✅ STEP 2: Debug logging (CRITICAL)
                _logger.LogInformation("VALIDATION INPUT:");
                _logger.LogInformation("Title: {Title}", title);
                _logger.LogInformation("Description Length: {Len}", description?.Length ?? 0);
                _logger.LogInformation("LearningOutcomes Length: {Len}", learningOutcomes?.Length ?? 0);

                // ✅ STEP 3: Combine text
                var courseText = $"{title} {description} {learningOutcomes}".Trim();

                if (string.IsNullOrWhiteSpace(courseText))
                {
                    result.IsRelevant = false;
                    result.Score = 0;
                    result.Reason = "Course metadata is invalid.";
                    return result;
                }

                // ✅ STEP 4: AI relevance calculation (placeholder)
                result.IsRelevant = true;
                result.Score = CalculateScore(courseText, content);
                result.Reason = "Validation completed";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation failed");

                return new ValidationResult
                {
                    IsRelevant = false,
                    Score = 0,
                    Reason = "Validation exception occurred"
                };
            }
        }

        // ✅ REUSABLE METADATA RESOLVER
        private string GetMetadataValue(
            Dictionary<string, string> metadata,
            params string[] keys)
        {
            foreach (var key in keys)
            {
                if (metadata.TryGetValue(key, out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    _logger.LogInformation("Metadata matched key: {Key}", key);
                    return value;
                }
            }

            return string.Empty;
        }

        private int CalculateScore(string courseText, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            // Simple scoring baseline
            var score = content.Contains(courseText, StringComparison.OrdinalIgnoreCase)
                ? 90
                : 65;

            return score;
        }
    }

    public class ValidationResult
    {
        public bool IsRelevant { get; set; }
        public int Score { get; set; }
        public string Reason { get; set; }
    }
}