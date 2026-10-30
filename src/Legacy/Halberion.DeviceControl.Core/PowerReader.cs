using System.Runtime.InteropServices;
using System.Text;

namespace Halberion.DeviceControl.Core;

public sealed class PowerReader
{
    private const byte Unknown = 255;
    public PowerSummary Read()
    {
        var schemeId = Guid.Empty;
        var schemeName = "Windows tarafından raporlanmadı";
        if (PowerGetActiveScheme(IntPtr.Zero, out var schemePointer) == 0 && schemePointer != IntPtr.Zero)
        {
            try
            {
                schemeId = Marshal.PtrToStructure<Guid>(schemePointer);
                schemeName = ReadSchemeName(schemeId) ?? schemeName;
            }
            finally { LocalFree(schemePointer); }
        }

        if (!GetSystemPowerStatus(out var status))
            return new(schemeId, schemeName, "Bilinmiyor", "Bilinmiyor", "Bilinmiyor", -1);
        var source = status.AcLineStatus switch { 0 => "Pil", 1 => "Prize bağlı", _ => "Bilinmiyor" };
        var level = status.BatteryLifePercent == Unknown ? "Bilinmiyor" : $"%{status.BatteryLifePercent}";
        var state = status.BatteryFlag switch
        {
            Unknown => "Bilinmiyor",
            128 => "Pil yok",
            var flag when (flag & 8) != 0 => "Şarj oluyor",
            var flag when (flag & 4) != 0 => "Kritik",
            var flag when (flag & 2) != 0 => "Düşük",
            var flag when (flag & 1) != 0 => "Yüksek",
            _ => "Normal"
        };
        var minutes = status.BatteryLifeTime == uint.MaxValue ? -1 :
            (int)Math.Min(int.MaxValue, status.BatteryLifeTime / 60);
        return new(schemeId, schemeName, source, level, state, minutes);
    }

    private static string? ReadSchemeName(Guid scheme)
    {
        uint size = 0;
        _ = PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, null, ref size);
        if (size == 0 || size > 64 * 1024) return null;
        var buffer = new byte[size];
        if (PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, ref size) != 0)
            return null;
        return Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadFriendlyName(IntPtr rootPowerKey, ref Guid schemeGuid,
        IntPtr subGroupGuid, IntPtr powerSettingGuid, [Out] byte[]? buffer, ref uint bufferSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
