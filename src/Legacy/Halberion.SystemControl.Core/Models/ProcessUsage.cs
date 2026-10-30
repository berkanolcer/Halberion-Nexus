namespace Halberion.SystemControl.Core.Models;

public sealed record ProcessUsage(
    int ProcessId,
    string Name,
    double CpuUsagePercent,
    long WorkingSetBytes,
    bool CpuSampleReady = false);
