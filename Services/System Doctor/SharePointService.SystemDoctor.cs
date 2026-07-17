using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using MITANZ360Pro.Web.Services.SystemDoctor;

namespace MITANZ360Pro.Web.Services
{
    public partial class SharePointService
    {
        // ✅ Allow BOTH roles
        private static readonly HashSet<string> AllowedAdminRoles =
            new(StringComparer.OrdinalIgnoreCase) { "Admin", "SysAdmin" };

        /// <summary>
        /// Generates a full System Doctor report using REAL runtime data + REAL Graph probes.
        /// RBAC: Admin OR SysAdmin (enforced here).
        /// </summary>
        public async Task<SystemDoctorReport> GetSystemDoctorReportAsync(
            ClaimsPrincipal user,
            bool deepGraphChecks,
            CancellationToken cancellationToken = default)
        {
            EnsureAdminOrSysAdmin(user);

            var sw = Stopwatch.StartNew();
            var report = new SystemDoctorReport();

            report.Snapshot = CaptureRuntimeSnapshot();
            report.Checks.AddRange(BuildRuntimeChecks(report.Snapshot));

            // Graph / SharePoint checks (real calls, lightweight first)
            report.Checks.AddRange(await BuildGraphChecksAsync(deepGraphChecks, cancellationToken).ConfigureAwait(false));

            // Security hints (real policy hints)
            report.Checks.AddRange(BuildSecurityHints());

            // Sort: Critical -> Warning -> Healthy, then Category, then Name
            report.Checks = report.Checks
                .OrderByDescending(c => c.Status)
                .ThenBy(c => c.Category)
                .ThenBy(c => c.Name)
                .ToList();

            sw.Stop();
            report.ElapsedMs = sw.ElapsedMilliseconds;
            report.GeneratedAtUtc = DateTimeOffset.UtcNow;

            return report;
        }

        /// <summary>
        /// Returns a lightweight snapshot for real-time live monitoring (no Graph calls).
        /// RBAC: Admin OR SysAdmin (enforced here).
        /// </summary>
        public Task<SystemDoctorSnapshot> GetSystemDoctorSnapshotAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
        {
            EnsureAdminOrSysAdmin(user);
            return Task.FromResult(CaptureRuntimeSnapshot());
        }

        // ========================= RBAC =========================

        private static void EnsureAdminOrSysAdmin(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                throw new UnauthorizedAccessException("Authentication required.");

            // ✅ Correct role check: role-by-role (no comma string)
            foreach (var role in AllowedAdminRoles)
            {
                if (user.IsInRole(role))
                    return;
            }

            // ✅ Also check explicit role claims (some setups rely on claims)
            var roleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
            foreach (var role in roleClaims)
            {
                if (AllowedAdminRoles.Contains(role))
                    return;
            }

            throw new UnauthorizedAccessException("Admin/SysAdmin access required for System Doctor.");
        }

        // ========================= SNAPSHOT =========================

        private SystemDoctorSnapshot CaptureRuntimeSnapshot()
        {
            using var p = Process.GetCurrentProcess();

            var workingSetMb = p.WorkingSet64 / (1024d * 1024d);
            var privateMb = p.PrivateMemorySize64 / (1024d * 1024d);

            var gcBytes = GC.GetTotalMemory(forceFullCollection: false);
            var gcHeapMb = gcBytes / (1024d * 1024d);

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds;

            ThreadPool.GetAvailableThreads(out var workerAvail, out var ioAvail);
            ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);

            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version =
                asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "Unknown";

            return new SystemDoctorSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,

                ApplicationName = asm.GetName().Name ?? "MITANZ360Pro",
                EnvironmentName = TryGetHostEnvironmentName(),
                ContentRootPath = TryGetContentRootPath(),
                Version = version,

                WorkingSetMb = workingSetMb,
                PrivateMemoryMb = privateMb,
                GcHeapMb = gcHeapMb,

                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),

                ThreadCount = p.Threads.Count,
                HandleCount = SafeGetHandleCount(p),

                TotalProcessorSeconds = p.TotalProcessorTime.TotalSeconds,
                ProcessorCount = Environment.ProcessorCount,

                UptimeSeconds = uptime,

                ThreadPoolWorkerAvailable = workerAvail,
                ThreadPoolWorkerMax = workerMax,
                ThreadPoolIoAvailable = ioAvail,
                ThreadPoolIoMax = ioMax
            };
        }

        private static int SafeGetHandleCount(Process p)
        {
            try { return p.HandleCount; }
            catch { return -1; }
        }

        private string TryGetHostEnvironmentName()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                return string.IsNullOrWhiteSpace(env) ? "Unknown" : env;
            }
            catch { return "Unknown"; }
        }

        private string TryGetContentRootPath()
        {
            try { return AppContext.BaseDirectory; }
            catch { return string.Empty; }
        }

        // ========================= CHECKS =========================

        private IEnumerable<HealthCheckResult> BuildRuntimeChecks(SystemDoctorSnapshot s)
        {
            var now = DateTimeOffset.UtcNow;

            yield return new HealthCheckResult
            {
                Category = "Runtime",
                Name = "Working Set Memory",
                Status = s.WorkingSetMb > 1200 ? HealthStatus.Warning : HealthStatus.Healthy,
                Message = $"{s.WorkingSetMb:F0} MB working set",
                Recommendation = s.WorkingSetMb > 1200
                    ? "Investigate allocations, large caching, image processing, or runaway requests."
                    : "OK",
                Value = s.WorkingSetMb,
                Unit = "MB",
                CheckedAt = now
            };

            yield return new HealthCheckResult
            {
                Category = "Runtime",
                Name = "GC Heap Size",
                Status = s.GcHeapMb > 800 ? HealthStatus.Warning : HealthStatus.Healthy,
                Message = $"{s.GcHeapMb:F0} MB managed heap",
                Recommendation = s.GcHeapMb > 800
                    ? "Check for LOH growth and unbounded in-memory collections."
                    : "OK",
                Value = s.GcHeapMb,
                Unit = "MB",
                CheckedAt = now
            };

            yield return new HealthCheckResult
            {
                Category = "Runtime",
                Name = "Thread Count",
                Status = s.ThreadCount > 350 ? HealthStatus.Warning : HealthStatus.Healthy,
                Message = $"{s.ThreadCount} threads",
                Recommendation = s.ThreadCount > 350
                    ? "Look for blocking calls, thread leaks, or thread pool starvation."
                    : "OK",
                Value = s.ThreadCount,
                Unit = "count",
                CheckedAt = now
            };

            if (s.HandleCount >= 0)
            {
                yield return new HealthCheckResult
                {
                    Category = "Runtime",
                    Name = "Handle Count",
                    Status = s.HandleCount > 10000 ? HealthStatus.Warning : HealthStatus.Healthy,
                    Message = $"{s.HandleCount} handles",
                    Recommendation = s.HandleCount > 10000
                        ? "Investigate undisposed streams/sockets/file handles/timers."
                        : "OK",
                    Value = s.HandleCount,
                    Unit = "count",
                    CheckedAt = now
                };
            }

            yield return new HealthCheckResult
            {
                Category = "Runtime",
                Name = "ThreadPool Availability",
                Status = (s.ThreadPoolWorkerAvailable < (s.ThreadPoolWorkerMax * 0.15))
                    ? HealthStatus.Warning
                    : HealthStatus.Healthy,
                Message = $"Worker: {s.ThreadPoolWorkerAvailable}/{s.ThreadPoolWorkerMax}, IO: {s.ThreadPoolIoAvailable}/{s.ThreadPoolIoMax}",
                Recommendation = (s.ThreadPoolWorkerAvailable < (s.ThreadPoolWorkerMax * 0.15))
                    ? "Possible thread pool starvation. Remove .Result/.Wait(), reduce blocking IO, use async all the way."
                    : "OK",
                CheckedAt = now
            };

            yield return new HealthCheckResult
            {
                Category = "Runtime",
                Name = "Uptime",
                Status = s.UptimeSeconds < 1800 ? HealthStatus.Warning : HealthStatus.Healthy,
                Message = $"{TimeSpan.FromSeconds(s.UptimeSeconds):g}",
                Recommendation = s.UptimeSeconds < 1800
                    ? "If restarts are frequent, check crashes, IIS recycling, container restarts, or deployment loops."
                    : "OK",
                Value = s.UptimeSeconds / 3600d,
                Unit = "hours",
                CheckedAt = now
            };
        }

        private async Task<List<HealthCheckResult>> BuildGraphChecksAsync(bool deep, CancellationToken ct)
        {
            var checks = new List<HealthCheckResult>();
            var now = DateTimeOffset.UtcNow;

            var orgPing = await PingGraphOrganizationAsync(ct).ConfigureAwait(false);
            checks.Add(ToCheck("Graph", "Graph Token & Organization", orgPing,
                okRec: "OK",
                failRec: "Check Entra ID app registration, admin consent, and Graph permissions.", now));

            var sitePing = await PingGraphRootSiteAsync(ct).ConfigureAwait(false);
            checks.Add(ToCheck("Graph", "SharePoint Root Site", sitePing,
                okRec: sitePing.Success && sitePing.ElapsedMs > 2500
                    ? "Latency high. Consider caching IDs and reducing payload with $select."
                    : "OK",
                failRec: "Verify SharePoint permissions (Sites.Read.All / Sites.Selected) and tenant access.", now,
                warnIfMsOver: 2500));

            if (!deep)
                return checks;

            var listsPing = await PingGraphListsLightAsync(ct).ConfigureAwait(false);
            checks.Add(ToCheck("Graph", "Lists Probe (top=1)", listsPing,
                okRec: "OK",
                failRec: "If using Sites.Selected, ensure the site has been granted permissions. Check throttling (429).", now,
                warnIfMsOver: 3000));

            var drivePing = await PingGraphDriveRootAsync(ct).ConfigureAwait(false);
            checks.Add(ToCheck("Graph", "Drive Probe (Root)", drivePing,
                okRec: "OK",
                failRec: "Verify Files.Read.All / Sites.Read.All permissions and SharePoint drive availability.", now,
                warnIfMsOver: 3000));

            return checks;
        }

        private static HealthCheckResult ToCheck(
            string category,
            string name,
            ProbeResult pr,
            string okRec,
            string failRec,
            DateTimeOffset now,
            long? warnIfMsOver = null)
        {
            var status =
                pr.Success
                    ? (warnIfMsOver.HasValue && pr.ElapsedMs > warnIfMsOver.Value ? HealthStatus.Warning : HealthStatus.Healthy)
                    : HealthStatus.Critical;

            return new HealthCheckResult
            {
                Category = category,
                Name = name,
                Status = status,
                Message = pr.Success ? $"OK in {pr.ElapsedMs} ms" : $"FAILED: {pr.ErrorMessage}",
                Recommendation = pr.Success ? okRec : failRec,
                Value = pr.ElapsedMs,
                Unit = "ms",
                CheckedAt = now
            };
        }

        private IEnumerable<HealthCheckResult> BuildSecurityHints()
        {
            var now = DateTimeOffset.UtcNow;

            yield return new HealthCheckResult
            {
                Category = "Security",
                Name = "Sensitive Logging Risk",
                Status = HealthStatus.Warning,
                Message = "Review logs to ensure no tokens/PII are written.",
                Recommendation = "Mask Authorization headers, tokens, emails; disable sensitive EF logging in Production.",
                CheckedAt = now
            };
        }

        // ========================= GRAPH PROBES (REAL) =========================

        private sealed class ProbeResult
        {
            public bool Success { get; set; }
            public long ElapsedMs { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private async Task<ProbeResult> PingGraphOrganizationAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var graph = ResolveGraphClient();

                // ✅ REAL Graph call (lightweight)
                await graph.Organization.GetAsync(r =>
                {
                    r.QueryParameters.Top = 1;
                    r.QueryParameters.Select = new[] { "id", "displayName" };
                }, ct).ConfigureAwait(false);

                sw.Stop();
                return new ProbeResult { Success = true, ElapsedMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger().LogError(ex, "SystemDoctor: Graph organization ping failed");
                return new ProbeResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorMessage = ex.Message };
            }
        }

        private async Task<ProbeResult> PingGraphRootSiteAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var graph = ResolveGraphClient();

                // ✅ REAL Graph call (lightweight)
                await graph.Sites["root"].GetAsync(r =>
                {
                    r.QueryParameters.Select = new[] { "id", "displayName", "webUrl" };
                }, ct).ConfigureAwait(false);

                sw.Stop();
                return new ProbeResult { Success = true, ElapsedMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger().LogError(ex, "SystemDoctor: Root site ping failed");
                return new ProbeResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorMessage = ex.Message };
            }
        }

        private async Task<ProbeResult> PingGraphListsLightAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var graph = ResolveGraphClient();

                // ✅ REAL Graph call (lightweight)
                await graph.Sites["root"].Lists.GetAsync(r =>
                {
                    r.QueryParameters.Top = 1;
                    r.QueryParameters.Select = new[] { "id", "displayName" };
                }, ct).ConfigureAwait(false);

                sw.Stop();
                return new ProbeResult { Success = true, ElapsedMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger().LogError(ex, "SystemDoctor: Lists probe failed");
                return new ProbeResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorMessage = ex.Message };
            }
        }

        private async Task<ProbeResult> PingGraphDriveRootAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var graph = ResolveGraphClient();

                // ✅ REAL Graph call (lightweight)
                await graph.Sites["root"].Drive.GetAsync(r =>
                {
                    r.QueryParameters.Select = new[] { "id", "driveType", "webUrl" };
                }, ct).ConfigureAwait(false);

                sw.Stop();
                return new ProbeResult { Success = true, ElapsedMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger().LogError(ex, "SystemDoctor: Drive probe failed");
                return new ProbeResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorMessage = ex.Message };
            }
        }

        // ========================= INTERNAL RESOLUTION HELPERS =========================

        /// <summary>
        /// Resolves GraphServiceClient from the existing SharePointService instance without assuming a field name.
        /// This keeps the "single service" architecture and still uses real data.
        /// </summary>
        private GraphServiceClient ResolveGraphClient()
        {
            var t = GetType();

            // property first
            var prop = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(p => typeof(GraphServiceClient).IsAssignableFrom(p.PropertyType));

            if (prop?.GetValue(this) is GraphServiceClient g1)
                return g1;

            // then field
            var field = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(f => typeof(GraphServiceClient).IsAssignableFrom(f.FieldType));

            if (field?.GetValue(this) is GraphServiceClient g2)
                return g2;

            // If not found, this is a real error (not sample). We fail loudly.
            throw new InvalidOperationException(
                "GraphServiceClient is not available inside SharePointService. " +
                "Inject GraphServiceClient into SharePointService constructor and store it as a field/property.");
        }

        /// <summary>
        /// Uses existing logger if available; otherwise safely falls back.
        /// </summary>
        private ILogger Logger()
        {
            // If your core SharePointService already has _logger, this file won't require it.
            // We'll try to resolve any ILogger field/property; otherwise use NullLogger.
            var t = GetType();

            var prop = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(p => typeof(ILogger).IsAssignableFrom(p.PropertyType) ||
                                             (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ILogger<>)));

            if (prop?.GetValue(this) is ILogger l1)
                return l1;

            var field = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(f => typeof(ILogger).IsAssignableFrom(f.FieldType) ||
                                              (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(ILogger<>)));

            if (field?.GetValue(this) is ILogger l2)
                return l2;

            return NullLogger.Instance;
        }
    }
}