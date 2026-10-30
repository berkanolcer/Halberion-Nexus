using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace Halberion.Nexus.Modules;

public sealed record OwnedConnection(string Protocol, string Local, string Remote, string State,
    int ProcessId, string ProcessName, string ProcessPath);

public static class OwnedConnectionReader
{
    public static IReadOnlyList<OwnedConnection> ReadIpv4()
    {
        var rows = new List<OwnedConnection>();
        ReadTcp(rows); ReadUdp(rows);
        return rows.OrderBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Remote).ToArray();
    }
    private static void ReadTcp(List<OwnedConnection> rows)
    {
        var size = 0; _ = GetExtendedTcpTable(IntPtr.Zero, ref size, true, 2, 5, 0);
        if (size <= 0) return; var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, 2, 5, 0) != 0) return;
            var count = Marshal.ReadInt32(buffer); var pointer = buffer + sizeof(int); var rowSize = Marshal.SizeOf<TcpRow>();
            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<TcpRow>(pointer + i * rowSize); var process = ProcessInfo((int)row.Pid);
                rows.Add(new("TCP", Endpoint(row.LocalAddress, row.LocalPort), Endpoint(row.RemoteAddress, row.RemotePort), TcpState(row.State), (int)row.Pid, process.Name, process.Path));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static void ReadUdp(List<OwnedConnection> rows)
    {
        var size = 0; _ = GetExtendedUdpTable(IntPtr.Zero, ref size, true, 2, 1, 0);
        if (size <= 0) return; var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedUdpTable(buffer, ref size, true, 2, 1, 0) != 0) return;
            var count = Marshal.ReadInt32(buffer); var pointer = buffer + sizeof(int); var rowSize = Marshal.SizeOf<UdpRow>();
            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<UdpRow>(pointer + i * rowSize); var process = ProcessInfo((int)row.Pid);
                rows.Add(new("UDP", Endpoint(row.LocalAddress, row.LocalPort), "*:*", "Dinleme", (int)row.Pid, process.Name, process.Path));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static (string Name, string Path) ProcessInfo(int pid)
    {
        try { using var process = Process.GetProcessById(pid); string path; try { path = process.MainModule?.FileName ?? ""; } catch { path = ""; } return (process.ProcessName, path); }
        catch { return (pid == 0 ? "System Idle" : "Korumalı/Bilinmiyor", ""); }
    }
    private static string Endpoint(uint address, uint port) => $"{new IPAddress(address)}:{Port(port)}";
    private static ushort Port(uint port) => (ushort)IPAddress.NetworkToHostOrder((short)port);
    private static string TcpState(uint state) => state switch { 1 => "Closed", 2 => "Listen", 3 => "SynSent", 4 => "SynReceived", 5 => "Established", 6 => "FinWait1", 7 => "FinWait2", 8 => "CloseWait", 9 => "Closing", 10 => "LastAck", 11 => "TimeWait", 12 => "DeleteTcb", _ => state.ToString() };
    [StructLayout(LayoutKind.Sequential)] private struct TcpRow { public uint State, LocalAddress, LocalPort, RemoteAddress, RemotePort, Pid; }
    [StructLayout(LayoutKind.Sequential)] private struct UdpRow { public uint LocalAddress, LocalPort, Pid; }
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
}
