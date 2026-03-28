using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SortMyMedia
{
    public sealed class ResourceUsageReport
    {
        public double AvgCpuPercent { get; init; }
        public double MaxCpuPercent { get; init; }
        public double AvgMemoryMB { get; init; }
        public double MaxMemoryMB { get; init; }
        public double AvgGpuPercent { get; init; }
        public double MaxGpuPercent { get; init; }
        public double AvgGpuMemoryPercent { get; init; }
        public double MaxGpuMemoryPercent { get; init; }
        public double GpuMemoryUsedMB { get; init; }
        public double GpuMemoryTotalMB { get; init; }
        public bool GpuAvailable { get; init; }
        public int SampleCount { get; init; }
    }

    public sealed class ResourceMonitor : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly List<double> _cpuSamples = new();
        private readonly List<double> _memorySamples = new();
        private readonly List<double> _gpuUtilSamples = new();
        private readonly List<double> _gpuMemSamples = new();
        private double _lastGpuMemUsedMB;
        private double _lastGpuMemTotalMB;
        private bool _gpuAvailable;
        private Task? _samplingTask;
        private readonly int _intervalMs;
        private readonly int _processorCount;

        private TimeSpan _prevCpuTime;
        private DateTime _prevTimestamp;

        public ResourceMonitor(int intervalMs = 500)
        {
            _intervalMs = intervalMs;
            _processorCount = Environment.ProcessorCount;
        }

        public void Start()
        {
            var proc = Process.GetCurrentProcess();
            _prevCpuTime = proc.TotalProcessorTime;
            _prevTimestamp = DateTime.UtcNow;

            _samplingTask = Task.Run(() => SampleLoop(_cts.Token));
        }

        public ResourceUsageReport Stop()
        {
            _cts.Cancel();

            try { _samplingTask?.Wait(); }
            catch (AggregateException) { }

            lock (_cpuSamples)
            {
                return new ResourceUsageReport
                {
                    AvgCpuPercent = _cpuSamples.Count > 0 ? _cpuSamples.Average() : 0,
                    MaxCpuPercent = _cpuSamples.Count > 0 ? _cpuSamples.Max() : 0,
                    AvgMemoryMB = _memorySamples.Count > 0 ? _memorySamples.Average() : 0,
                    MaxMemoryMB = _memorySamples.Count > 0 ? _memorySamples.Max() : 0,
                    AvgGpuPercent = _gpuUtilSamples.Count > 0 ? _gpuUtilSamples.Average() : 0,
                    MaxGpuPercent = _gpuUtilSamples.Count > 0 ? _gpuUtilSamples.Max() : 0,
                    AvgGpuMemoryPercent = _gpuMemSamples.Count > 0 ? _gpuMemSamples.Average() : 0,
                    MaxGpuMemoryPercent = _gpuMemSamples.Count > 0 ? _gpuMemSamples.Max() : 0,
                    GpuMemoryUsedMB = _lastGpuMemUsedMB,
                    GpuMemoryTotalMB = _lastGpuMemTotalMB,
                    GpuAvailable = _gpuAvailable,
                    SampleCount = _cpuSamples.Count
                };
            }
        }

        private void SampleLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    SampleCpuAndMemory();
                    SampleGpu();
                }
                catch
                {
                    // Sampling failures should not crash the monitor
                }

                try { Task.Delay(_intervalMs, ct).Wait(ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void SampleCpuAndMemory()
        {
            var proc = Process.GetCurrentProcess();
            proc.Refresh();

            var now = DateTime.UtcNow;
            var currentCpuTime = proc.TotalProcessorTime;
            var elapsed = (now - _prevTimestamp).TotalMilliseconds;

            if (elapsed > 0)
            {
                double cpuUsed = (currentCpuTime - _prevCpuTime).TotalMilliseconds;
                double cpuPercent = (cpuUsed / (elapsed * _processorCount)) * 100.0;
                cpuPercent = Math.Clamp(cpuPercent, 0, 100);

                double memoryMB = proc.WorkingSet64 / (1024.0 * 1024.0);

                lock (_cpuSamples)
                {
                    _cpuSamples.Add(cpuPercent);
                    _memorySamples.Add(memoryMB);
                }
            }

            _prevCpuTime = currentCpuTime;
            _prevTimestamp = now;
        }

        private void SampleGpu()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=utilization.gpu,utilization.memory,memory.used,memory.total --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var p = Process.Start(psi);
                if (p == null) return;

                string? output = p.StandardOutput.ReadToEnd()?.Trim();
                p.WaitForExit(2000);

                if (string.IsNullOrWhiteSpace(output))
                    return;

                // Parse: "gpuUtil, memUtil, memUsed, memTotal"
                var parts = output.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[0], out double gpuUtil) &&
                    double.TryParse(parts[1], out double memUtil) &&
                    double.TryParse(parts[2], out double memUsed) &&
                    double.TryParse(parts[3], out double memTotal))
                {
                    lock (_cpuSamples)
                    {
                        _gpuAvailable = true;
                        _gpuUtilSamples.Add(gpuUtil);
                        _gpuMemSamples.Add(memUtil);
                        _lastGpuMemUsedMB = memUsed;
                        _lastGpuMemTotalMB = memTotal;
                    }
                }
            }
            catch
            {
                // nvidia-smi not available — GPU monitoring disabled
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
