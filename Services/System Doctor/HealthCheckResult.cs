using System;

namespace MITANZ360Pro.Web.Services.SystemDoctor
{
    public enum HealthStatus
    {
        Healthy = 1,
        Warning = 2,
        Critical = 3
    }

    public sealed class HealthCheckResult
    {
        public string Name { get; set; } = string.Empty;
        public HealthStatus Status { get; set; } = HealthStatus.Healthy;

        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>
        /// Optional numeric value for charting (ms, MB, %, etc.)
        /// </summary>
        public double? Value { get; set; }

        public string? Unit { get; set; }

        /// <summary>
        /// Optional category (Runtime / Graph / Security / Storage / Configuration)
        /// </summary>
        public string Category { get; set; } = "Runtime";

        public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}