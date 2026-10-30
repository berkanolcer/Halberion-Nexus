using Halberion.Nexus.Core;
using Halberion.DeviceControl.Core;
using Halberion.HotspotControl.Models;
using Halberion.HotspotControl.Services;
using Halberion.NetworkMonitor.Core;
using Halberion.SystemControl.Core.Models;
using Halberion.SystemControl.Core.Services;

namespace Halberion.Nexus.Infrastructure;

public sealed class NexusServices : IDisposable
{
    public OperationGate Operations { get; } = new();
    public NexusLog Log { get; } = new();
    public WindowsSystemMetricsProvider SystemMetrics { get; } = new();
    public ProcessUsageSampler ProcessSampler { get; } = new();
    public HardwareInfoProvider HardwareInfo { get; } = new();
    public StartupItemScanner StartupItems { get; } = new();
    public NetworkSampler NetworkSampler { get; } = new();
    public DeviceSnapshotProvider DeviceProvider { get; } = new();
    public AdapterDiscoveryService HotspotDiscovery { get; } = new();
    public HotspotService Hotspot { get; } = new();
    public HardwareSensorService Sensors { get; } = new();
    public NexusSettingsService Settings { get; } = new();
    public FirewallRuleJournal FirewallJournal { get; } = new();
    public NetworkHistoryService NetworkHistory { get; } = new();
    private readonly SemaphoreSlim _systemLock = new(1, 1), _networkLock = new(1, 1), _deviceLock = new(1, 1), _hotspotLock = new(1, 1);
    private (DateTime Time, SystemSnapshot Value)? _systemCache;
    private (DateTime Time, NetworkSnapshot Value)? _networkCache;
    private (DateTime Time, DeviceSnapshot Value)? _deviceCache;
    private (DateTime Time, DiscoverySnapshot Value)? _hotspotCache;
    private readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halberion", "Nexus", "logs", $"nexus-{DateTime.Now:yyyyMMdd}.jsonl");

    public NexusServices() { Log.EntryAdded += PersistLog; }

    public async Task<SystemSnapshot> GetSystemAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _systemLock.WaitAsync(cancellationToken); try { if (!force && _systemCache is { } c && DateTime.UtcNow - c.Time < TimeSpan.FromSeconds(1)) return c.Value; var value = await Task.Run(SystemMetrics.Capture, cancellationToken); _systemCache = (DateTime.UtcNow, value); return value; } finally { _systemLock.Release(); }
    }
    public async Task<NetworkSnapshot> GetNetworkAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _networkLock.WaitAsync(cancellationToken); try { if (!force && _networkCache is { } c && DateTime.UtcNow - c.Time < TimeSpan.FromSeconds(1)) return c.Value; var value = await Task.Run(NetworkSampler.Capture, cancellationToken); _networkCache = (DateTime.UtcNow, value); return value; } finally { _networkLock.Release(); }
    }
    public async Task<DeviceSnapshot> GetDevicesAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _deviceLock.WaitAsync(cancellationToken); try { if (!force && _deviceCache is { } c && DateTime.UtcNow - c.Time < TimeSpan.FromSeconds(15)) return c.Value; var value = await Task.Run(DeviceProvider.Capture, cancellationToken); _deviceCache = (DateTime.UtcNow, value); return value; } finally { _deviceLock.Release(); }
    }
    public async Task<DiscoverySnapshot> GetHotspotDiscoveryAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _hotspotLock.WaitAsync(cancellationToken); try { if (!force && _hotspotCache is { } c && DateTime.UtcNow - c.Time < TimeSpan.FromSeconds(10)) return c.Value; var value = await HotspotDiscovery.DiscoverAsync(cancellationToken); _hotspotCache = (DateTime.UtcNow, value); return value; } finally { _hotspotLock.Release(); }
    }
    public void Dispose() { Sensors.Dispose(); _systemLock.Dispose(); _networkLock.Dispose(); _deviceLock.Dispose(); _hotspotLock.Dispose(); }
    private void PersistLog(NexusLogEntry entry)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!); File.AppendAllText(_logPath, System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine); }
        catch { }
    }
}
