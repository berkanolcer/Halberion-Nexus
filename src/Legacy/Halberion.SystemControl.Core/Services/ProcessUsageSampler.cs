using System.Diagnostics;
using Halberion.SystemControl.Core.Models;

namespace Halberion.SystemControl.Core.Services;

public sealed class ProcessUsageSampler
{
    private readonly object _sync = new();
    private Dictionary<int, ProcessTiming> _previous = [];

    public int LastSkippedProcessCount { get; private set; }

    public IReadOnlyList<ProcessUsage> CaptureTop(int count = 8)
    {
        lock (_sync)
        {
            var current = new Dictionary<int, ProcessTiming>();
            var samples = new List<ProcessUsage>();
            var skipped = 0;
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    // Idle represents unused capacity, not a CPU-consuming application.
                    if (process.Id == 0)
                    {
                        continue;
                    }

                    try
                    {
                        var started = process.StartTime.ToUniversalTime();
                        var cpuTime = process.TotalProcessorTime;
                        var timestamp = Stopwatch.GetTimestamp();
                        var workingSet = process.WorkingSet64;
                        var name = process.ProcessName;
                        current[process.Id] = new ProcessTiming(started, cpuTime, timestamp);
                        double? cpu = null;
                        if (_previous.TryGetValue(process.Id, out var previous))
                        {
                            cpu = CpuUsageCalculator.ProcessUsage(previous.CpuTime, cpuTime,
                                Stopwatch.GetElapsedTime(previous.Timestamp, timestamp).TotalSeconds,
                                Environment.ProcessorCount, previous.Started == started);
                        }

                        samples.Add(new ProcessUsage(process.Id,
                            string.IsNullOrWhiteSpace(name) ? "Bilinmeyen" : name,
                            cpu ?? 0, Math.Max(0, workingSet), cpu.HasValue));
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or
                               System.ComponentModel.Win32Exception or NotSupportedException or
                               UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        skipped++;
                    }
                }
            }

            _previous = current;
            LastSkippedProcessCount = skipped;
            return samples.OrderByDescending(sample => sample.CpuSampleReady)
                .ThenByDescending(sample => sample.CpuUsagePercent)
                .ThenByDescending(sample => sample.WorkingSetBytes)
                .Take(Math.Max(0, count)).ToArray();
        }
    }

    private readonly record struct ProcessTiming(DateTime Started, TimeSpan CpuTime, long Timestamp);
}
