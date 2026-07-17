using System;

namespace MITANZ360Pro.Web.Services.SystemDoctor
{
    public sealed class SystemDoctorSnapshot
    {
        public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        // App identity
        public string ApplicationName { get; set; } = "MITANZ360Pro";
        public string EnvironmentName { get; set; } = "Unknown";
        public string ContentRootPath { get; set; } = string.Empty;
        public string Version { get; set; } = "Unknown";

        // Host
        public string MachineName { get; set; } = Environment.MachineName;
        public string OSDescription { get; set; } = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        public string FrameworkDescription { get; set; } = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        public string ProcessArchitecture { get; set; } = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

        // Runtime metrics
        public double WorkingSetMb { get; set; }
        public double PrivateMemoryMb { get; set; }
        public double GcHeapMb { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }

        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }

        // CPU calculation support (UI computes % between samples)
        public double TotalProcessorSeconds { get; set; }
        public int ProcessorCount { get; set; }

        public double UptimeSeconds { get; set; }

        // ThreadPool
        public int ThreadPoolWorkerAvailable { get; set; }
        public int ThreadPoolWorkerMax { get; set; }
        public int ThreadPoolIoAvailable { get; set; }
        public int ThreadPoolIoMax { get; set; }
    }
}