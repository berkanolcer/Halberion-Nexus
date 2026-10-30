namespace Halberion.SystemControl.Core.Models;

public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    double CpuUsagePercent,
    ulong TotalMemoryBytes,
    ulong AvailableMemoryBytes,
    TimeSpan Uptime,
    bool CpuSampleReady = false)
{
    public ulong UsedMemoryBytes => TotalMemoryBytes - Math.Min(AvailableMemoryBytes, TotalMemoryBytes);

    public double MemoryUsagePercent => TotalMemoryBytes == 0
        ? 0
        : UsedMemoryBytes * 100d / TotalMemoryBytes;
}
