using System.Runtime.InteropServices;

namespace Halberion.DeviceControl.Core;

public sealed class AudioEndpointReader
{
    private const uint DeviceStateAll = 0x0000000F;
    private static readonly PropertyKey FriendlyNameKey =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public IReadOnlyList<AudioEndpoint> Read()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Yalnızca Windows desteklenir.");
        var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), throwOnError: true)!;
        var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
        var result = new List<AudioEndpoint>();
        try
        {
            ReadDirection(enumerator, 0, "Çıkış", result);
            ReadDirection(enumerator, 1, "Giriş", result);
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumerator);
        }
        return result.OrderBy(endpoint => endpoint.Direction)
            .ThenByDescending(endpoint => endpoint.IsDefault)
            .ThenBy(endpoint => endpoint.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void ReadDirection(IMMDeviceEnumerator enumerator, int flow, string direction,
        ICollection<AudioEndpoint> result)
    {
        string? defaultId = null;
        IMMDevice? defaultDevice = null;
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(flow, 0, out defaultDevice) >= 0 && defaultDevice is not null)
                defaultId = GetId(defaultDevice);
        }
        finally
        {
            if (defaultDevice is not null) Marshal.FinalReleaseComObject(defaultDevice);
        }

        var hr = enumerator.EnumAudioEndpoints(flow, DeviceStateAll, out var collection);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
            for (uint index = 0; index < count; index++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(index, out var device));
                try
                {
                    var id = GetId(device);
                    Marshal.ThrowExceptionForHR(device.GetState(out var state));
                    result.Add(new(id, ReadFriendlyName(device) ?? "(Adsız ses aygıtı)", direction,
                        StateName(state), string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)));
                }
                finally
                {
                    Marshal.FinalReleaseComObject(device);
                }
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(collection);
        }
    }

    private static string GetId(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.GetId(out var idPointer));
        try { return Marshal.PtrToStringUni(idPointer) ?? string.Empty; }
        finally { Marshal.FreeCoTaskMem(idPointer); }
    }

    private static string? ReadFriendlyName(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out var store));
        try
        {
            var key = FriendlyNameKey;
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var value));
            try { return value.ValueType == 31 ? Marshal.PtrToStringUni(value.PointerValue) : null; }
            finally { PropVariantClear(ref value); }
        }
        finally
        {
            Marshal.FinalReleaseComObject(store);
        }
    }

    private static string StateName(uint state) => state switch
    {
        1 => "Etkin",
        2 => "Devre dışı",
        4 => "Mevcut değil",
        8 => "Takılı değil",
        _ => $"Durum 0x{state:X}"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
        public PropertyKey(Guid formatId, int propertyId) { FormatId = formatId; PropertyId = propertyId; }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort ValueType;
        [FieldOffset(8)] public IntPtr PointerValue;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant variant);
}
