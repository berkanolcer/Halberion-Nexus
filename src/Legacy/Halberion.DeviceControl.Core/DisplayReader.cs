using System.Runtime.InteropServices;

namespace Halberion.DeviceControl.Core;

public sealed class DisplayReader
{
    private const uint AttachedToDesktop = 0x00000001;
    private const uint MirroringDriver = 0x00000008;
    private const int CurrentSettings = -1;

    public IReadOnlyList<DisplayItem> Read()
    {
        var result = new List<DisplayItem>();
        for (uint index = 0; index < 64; index++)
        {
            var device = new DisplayDevice();
            if (!EnumDisplayDevices(null, index, device, 0)) break;
            if ((device.StateFlags & MirroringDriver) != 0) continue;

            var mode = new DevMode();
            mode.Size = (ushort)Marshal.SizeOf<DevMode>();
            var hasMode = EnumDisplaySettings(device.DeviceName, CurrentSettings, ref mode);
            var attached = (device.StateFlags & AttachedToDesktop) != 0;
            result.Add(new(device.DeviceName, string.IsNullOrWhiteSpace(device.DeviceString)
                    ? device.DeviceName : device.DeviceString,
                attached ? "Masaüstüne bağlı" : "Bağlı değil",
                hasMode ? (int)mode.PelsWidth : 0,
                hasMode ? (int)mode.PelsHeight : 0,
                hasMode ? (int)mode.DisplayFrequency : 0,
                hasMode ? (int)mode.BitsPerPel : 0));
        }
        return result.OrderByDescending(display => display.Status == "Masaüstüne bağlı")
            .ThenBy(display => display.DeviceName).ToArray();
    }

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
    private struct DevMode
    {
        private const int CchDeviceName = 32;
        private const int CchFormName = 32;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)] public string DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint deviceNumber,
        [In, Out] DisplayDevice displayDevice, uint flags);

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DevMode mode);
}
