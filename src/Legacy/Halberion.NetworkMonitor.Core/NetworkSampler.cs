using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Halberion.NetworkMonitor.Core;

public sealed record TrafficSample(double? DownloadBytesPerSecond, double? UploadBytesPerSecond,
    long SessionReceived, long SessionSent, string Note);

/// <summary>Counts only intervals observed while the same adapter is continuously available.</summary>
public sealed class TrafficCounter
{
    private long _received, _sent;
    private double _seconds;
    private bool _ready;
    private long _sessionReceived, _sessionSent;
    public void Invalidate() => _ready = false;
    public TrafficSample Update(long received, long sent, double seconds)
    {
        if (received < 0 || sent < 0 || !double.IsFinite(seconds) || seconds < 0)
        {
            Invalidate();
            return new(null, null, _sessionReceived, _sessionSent, "Sayaç okunamadı");
        }
        var delta = seconds - _seconds;
        if (!_ready || delta <= 0 || received < _received || sent < _sent)
        {
            var note = _ready ? "Sayaç sıfırlandı; yeni örnek bekleniyor" : "İlk aralık bekleniyor";
            (_received, _sent, _seconds, _ready) = (received, sent, seconds, true);
            return new(null, null, _sessionReceived, _sessionSent, note);
        }
        var down = received - _received;
        var up = sent - _sent;
        _sessionReceived = Saturate(_sessionReceived, down);
        _sessionSent = Saturate(_sessionSent, up);
        (_received, _sent, _seconds) = (received, sent, seconds);
        return new(down / delta, up / delta, _sessionReceived, _sessionSent, "Canlı");
    }
    private static long Saturate(long a, long b) => b > long.MaxValue - a ? long.MaxValue : a + b;
}

public sealed record AdapterSample(string Id, string Name, string Description, string Status,
    bool IsUp, string Addresses, string Gateways, long LinkSpeed, TrafficSample Traffic)
{
    public string DisplayName => $"{Name} • {Status}";
    public override string ToString() => DisplayName;
}
public sealed record NetworkSnapshot(DateTimeOffset Timestamp, IReadOnlyList<AdapterSample> Adapters, IReadOnlyList<string> Warnings);
public sealed record ConnectionRow(string Family, string Local, string Remote, string State);

public sealed class NetworkSampler
{
    private readonly Dictionary<string, TrafficCounter> _counters = [];
    private readonly object _sync = new();
    public NetworkSnapshot Capture()
    {
        lock (_sync)
        {
            var adapters = new List<AdapterSample>();
            var seen = new HashSet<string>();
            var warnings = new List<string>();
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                seen.Add(network.Id);
                if (!_counters.TryGetValue(network.Id, out var counter))
                    _counters[network.Id] = counter = new TrafficCounter();
                try
                {
                    var up = network.OperationalStatus == OperationalStatus.Up;
                    var statistics = network.GetIPStatistics();
                    if (!up) counter.Invalidate();
                    var traffic = counter.Update(statistics.BytesReceived, statistics.BytesSent, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
                    if (!up) { counter.Invalidate(); traffic = traffic with { DownloadBytesPerSecond = null, UploadBytesPerSecond = null, Note = "Bağlantı kapalı" }; }
                    var properties = network.GetIPProperties();
                    adapters.Add(new(network.Id, network.Name, network.Description,
                        up ? "Bağlı" : network.OperationalStatus.ToString(), up,
                        string.Join(" • ", properties.UnicastAddresses.Select(x => x.Address.ToString())),
                        string.Join(" • ", properties.GatewayAddresses.Select(x => x.Address.ToString())),
                        Math.Max(0, network.Speed), traffic));
                }
                catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException or NotSupportedException)
                {
                    counter.Invalidate();
                    warnings.Add($"{network.Name}: sayaç veya adaptör bilgileri okunamadı ({ex.GetType().Name}).");
                }
            }
            foreach (var missing in _counters.Keys.Where(id => !seen.Contains(id))) _counters[missing].Invalidate();
            return new(DateTimeOffset.Now, adapters.OrderByDescending(a => a.IsUp).ThenBy(a => a.Name).ToArray(), warnings);
        }
    }
    public static IReadOnlyList<ConnectionRow> ReadTcpConnections() =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
        .Select(c => new ConnectionRow(c.LocalEndPoint.AddressFamily.ToString(), c.LocalEndPoint.ToString(), c.RemoteEndPoint.ToString(), c.State.ToString()))
        .OrderBy(c => c.Local, StringComparer.Ordinal).ThenBy(c => c.Remote, StringComparer.Ordinal).ToArray();
}
