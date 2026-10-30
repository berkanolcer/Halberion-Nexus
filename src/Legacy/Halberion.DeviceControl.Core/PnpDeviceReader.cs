using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Halberion.DeviceControl.Core;

public sealed class PnpDeviceReader
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint SpdrpFriendlyName = 0x0000000C;
    private const uint SpdrpClass = 0x00000007;
    private const int ErrorNoMoreItems = 259;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public IReadOnlyList<DeviceItem> ReadPresentDevices()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Yalnızca Windows desteklenir.");
        var set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (set == InvalidHandleValue)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows aygıt listesi açılamadı.");

        var result = new List<DeviceItem>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ErrorNoMoreItems) break;
                    throw new Win32Exception(error, "Windows aygıt listesi okunamadı.");
                }

                var instanceId = ReadInstanceId(set, ref data);
                var name = ReadProperty(set, ref data, SpdrpFriendlyName)
                    ?? ReadProperty(set, ref data, SpdrpDeviceDesc)
                    ?? "(Adsız aygıt)";
                var setupClass = ReadProperty(set, ref data, SpdrpClass) ?? "Diğer";
                var category = Classify(setupClass, instanceId);
                var manufacturer = ReadProperty(set, ref data, SpdrpMfg) ?? "Bilinmiyor";
                var status = "Mevcut";
                uint problem = 0;
                var cr = CM_Get_DevNode_Status(out var nodeStatus, out problem, data.DevInst, 0);
                if (cr == 0)
                {
                    status = problem != 0 ? $"Sorun kodu {problem}" :
                        (nodeStatus & 0x00000008) != 0 ? "Çalışıyor" : "Mevcut";
                }
                result.Add(new(instanceId, name.Trim(), category, manufacturer.Trim(), status, problem));
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }

        return result
            .DistinctBy(device => device.InstanceId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(device => device.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal static string Classify(string setupClass, string instanceId)
    {
        if (setupClass.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
            instanceId.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return "Bluetooth";
        if (setupClass.Equals("AudioEndpoint", StringComparison.OrdinalIgnoreCase) ||
            setupClass.Equals("MEDIA", StringComparison.OrdinalIgnoreCase)) return "Ses";
        if (setupClass.Equals("Display", StringComparison.OrdinalIgnoreCase) ||
            setupClass.Equals("Monitor", StringComparison.OrdinalIgnoreCase)) return "Ekran";
        if (setupClass.Equals("USB", StringComparison.OrdinalIgnoreCase) ||
            instanceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) return "USB";
        if (setupClass.Equals("Battery", StringComparison.OrdinalIgnoreCase)) return "Pil";
        if (setupClass.Equals("Net", StringComparison.OrdinalIgnoreCase)) return "Ağ";
        if (setupClass.Equals("HIDClass", StringComparison.OrdinalIgnoreCase) ||
            setupClass.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) ||
            setupClass.Equals("Mouse", StringComparison.OrdinalIgnoreCase)) return "Giriş";
        return "Diğer";
    }

    private static string ReadInstanceId(IntPtr set, ref SpDevInfoData data)
    {
        var buffer = new StringBuilder(1024);
        return SetupDiGetDeviceInstanceId(set, ref data, buffer, buffer.Capacity, out _)
            ? buffer.ToString()
            : $"DEVINST:{data.DevInst}";
    }

    private static string? ReadProperty(IntPtr set, ref SpDevInfoData data, uint property)
    {
        var buffer = new byte[4096];
        if (!SetupDiGetDeviceRegistryProperty(set, ref data, property, out _, buffer, (uint)buffer.Length, out _))
            return null;
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SpDevInfoData data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref SpDevInfoData data, uint property,
        out uint propertyType, [Out] byte[] buffer, uint bufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr set, ref SpDevInfoData data,
        StringBuilder instanceId, int instanceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);
}
