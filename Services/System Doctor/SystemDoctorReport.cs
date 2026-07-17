using System;
using System.Collections.Generic;

namespace MITANZ360Pro.Web.Services.SystemDoctor
{
    public sealed class SystemDoctorReport
    {
        public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public SystemDoctorSnapshot Snapshot { get; set; } = new();

        /// <summary>
        /// Health checks (Graph, Runtime, Security hints, etc.)
        /// </summary>
        public List<HealthCheckResult> Checks { get; set; } = new();

        /// <summary>
        /// Total time spent generating report (ms)
        /// </summary>
        public long ElapsedMs { get; set; }
    }
}