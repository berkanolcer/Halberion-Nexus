using System.Runtime.InteropServices;
using Halberion.SystemControl.Core.Models;

namespace Halberion.SystemControl.Core.Services;

public sealed class WindowsSystemMetricsProvider
{
    private readonly object _sync = new();
    private SystemCpuTimes? _previousSample;

    public string? CpuUnavailableReason => GetActiveProcessorCount(0xffff) > 64
        ? "64 üzeri mantıksal işlemcide toplam CPU ölçümü bu sürümde desteklenmiyor."
        : null;

    public SystemSnapshot Capture()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Halberion System Control yalnızca Windows üzerinde çalışır.");
        }

        var cpu = CaptureCpuUsage();
        var memory = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(memory))
        {
            throw new InvalidOperationException($"Bellek bilgisi okunamadı. Windows hata kodu: {Marshal.GetLastWin32Error()}");
        }

        return new SystemSnapshot(
            DateTimeOffset.Now,
            cpu ?? 0,
            memory.TotalPhysical,
            memory.AvailablePhysical,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            cpu.HasValue);
    }

    private double? CaptureCpuUsage()
    {
        lock (_sync)
        {
            if (CpuUnavailableReason is not null)
            {
                return null;
            }

            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            {
                throw new InvalidOperationException($"CPU bilgisi okunamadı. Windows hata kodu: {Marshal.GetLastWin32Error()}");
            }

            var idle = ToUInt64(idleTime);
            var kernel = ToUInt64(kernelTime);
            var user = ToUInt64(userTime);

            var current = new SystemCpuTimes(idle, kernel, user);
            var result = _previousSample is { } previous ? CpuUsageCalculator.SystemUsage(previous, current) : null;
            _previousSample = current;
            return result;
        }
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll")]
    private static extern uint GetActiveProcessorCount(ushort groupNumber);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
