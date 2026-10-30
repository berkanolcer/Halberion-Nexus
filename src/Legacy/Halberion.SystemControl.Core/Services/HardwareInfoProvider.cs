using Microsoft.Win32;
using System.Runtime.InteropServices;
using Halberion.SystemControl.Core.Models;

namespace Halberion.SystemControl.Core.Services;

public sealed class HardwareInfoProvider
{
    public HardwareSummary Read()
    {
        var processor = ReadRegistryString(
            Registry.LocalMachine,
            @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
            "ProcessorNameString") ?? "Bilinmiyor";

        var graphics = ReadGraphicsAdapters();

        var windowsName = ReadRegistryString(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "ProductName") ?? "Windows";

        var displayVersion = ReadRegistryString(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "DisplayVersion");

        var version = new OsVersionInfo();
        var hasNativeVersion = RtlGetVersion(version) == 0;
        var build = hasNativeVersion ? (int)version.BuildNumber : Environment.OSVersion.Version.Build;
        windowsName = NormalizeWindowsName(windowsName, build, hasNativeVersion && version.ProductType == 1);
        var os = displayVersion is null
            ? $"{windowsName} (Derleme {build})"
            : $"{windowsName} {displayVersion} (Derleme {build})";

        return new HardwareSummary(
            Environment.MachineName,
            os,
            processor.Trim(),
            graphics.Trim(),
            Environment.ProcessorCount);
    }

    private static string? ReadRegistryString(RegistryKey root, string path, string name)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            return key?.GetValue(name) as string;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return null;
        }
    }

    internal static string NormalizeWindowsName(string productName, int build, bool workstation) =>
        workstation && build >= 22000 && productName.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase)
            ? "Windows 11" + productName[10..]
            : productName;

    private static string ReadGraphicsAdapters()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (uint index = 0; index < 256; index++)
        {
            var device = new DisplayDevice();
            if (!EnumDisplayDevices(null, index, device, 0))
            {
                break;
            }

            // Mirroring drivers are not hardware display adapters.
            if ((device.StateFlags & 0x8) == 0 && !string.IsNullOrWhiteSpace(device.DeviceString))
            {
                names.Add(device.DeviceString.Trim());
            }
        }

        return names.Count == 0 ? "Windows tarafından raporlanmadı" : string.Join(" • ", names);
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint deviceNumber, [In, Out] DisplayDevice displayDevice, uint flags);

    [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
    private static extern int RtlGetVersion([In, Out] OsVersionInfo version);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DisplayDevice
    {
        public uint Size = (uint)Marshal.SizeOf<DisplayDevice>();
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName = string.Empty;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString = string.Empty;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId = string.Empty;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey = string.Empty;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class OsVersionInfo
    {
        public uint Size = (uint)Marshal.SizeOf<OsVersionInfo>();
        public uint MajorVersion;
        public uint MinorVersion;
        public uint BuildNumber;
        public uint PlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ServicePack = string.Empty;
        public ushort ServicePackMajor;
        public ushort ServicePackMinor;
        public ushort SuiteMask;
        public byte ProductType;
        public byte Reserved;
    }
}
