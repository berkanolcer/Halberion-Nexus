using System.Text.Json;

namespace Halberion.Nexus.Infrastructure;
public sealed record NetworkHistorySample(DateTimeOffset Time, string Adapter, double DownloadBytesPerSecond, double UploadBytesPerSecond, int ActiveConnections)
{
    public string TimeText => Time.LocalDateTime.ToString("g");
    public string DownloadMbit => $"{DownloadBytesPerSecond * 8 / 1_000_000:0.00}";
    public string UploadMbit => $"{UploadBytesPerSecond * 8 / 1_000_000:0.00}";
}
public sealed class NetworkHistoryService
{
    private readonly string _directory; private readonly object _sync = new();
    public NetworkHistoryService(string? directory = null) => _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halberion", "Nexus", "network-history");
    public void Append(NetworkHistorySample sample)
    {
        lock (_sync) { Directory.CreateDirectory(_directory); File.AppendAllText(Path.Combine(_directory, $"traffic-{sample.Time:yyyyMMdd}.jsonl"), JsonSerializer.Serialize(sample) + Environment.NewLine); Cleanup(); }
    }
    public IReadOnlyList<NetworkHistorySample> ReadRecent(int maximum = 500)
    {
        lock (_sync)
        {
            if (!Directory.Exists(_directory)) return [];
            var rows = new List<NetworkHistorySample>();
            foreach (var file in Directory.EnumerateFiles(_directory, "traffic-*.jsonl").OrderByDescending(x => x).Take(30)) foreach (var line in File.ReadLines(file)) try { var row = JsonSerializer.Deserialize<NetworkHistorySample>(line); if (row is not null) rows.Add(row); } catch { }
            return rows.OrderByDescending(x => x.Time).Take(Math.Clamp(maximum, 1, 10_000)).ToArray();
        }
    }
    private void Cleanup() { foreach (var file in Directory.EnumerateFiles(_directory, "traffic-*.jsonl")) if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-30)) try { File.Delete(file); } catch { } }
}
