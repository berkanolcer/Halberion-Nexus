namespace Halberion.SystemControl.Core.Services;

internal readonly record struct SystemCpuTimes(ulong Idle, ulong Kernel, ulong User);

internal static class CpuUsageCalculator
{
    public static double? SystemUsage(SystemCpuTimes previous, SystemCpuTimes current)
    {
        if (current.Idle < previous.Idle || current.Kernel < previous.Kernel || current.User < previous.User)
        {
            return null;
        }

        // Subtract before converting to double to retain precision on long-running systems.
        var idleDelta = current.Idle - previous.Idle;
        var totalDelta = (double)(current.Kernel - previous.Kernel) + (current.User - previous.User);
        if (totalDelta <= 0 || idleDelta > totalDelta)
        {
            return null;
        }

        // Kernel time includes idle time, as documented by GetSystemTimes.
        return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
    }

    public static double? ProcessUsage(TimeSpan previousCpu, TimeSpan currentCpu, double elapsedSeconds,
        int logicalProcessors, bool sameProcess)
    {
        if (!sameProcess || currentCpu < previousCpu || elapsedSeconds <= 0 ||
            !double.IsFinite(elapsedSeconds) || logicalProcessors <= 0)
        {
            return null;
        }

        return Math.Clamp((currentCpu - previousCpu).TotalSeconds * 100d / elapsedSeconds / logicalProcessors, 0, 100);
    }
}
