using Halberion.HotspotControl.Services;
using Halberion.SystemControl.Core.Services;
using Halberion.NetworkMonitor.Core;
using Halberion.Nexus.Modules;
using Halberion.DeviceControl.Core;
using Halberion.StorageManager.Core;
using Halberion.Nexus.Infrastructure;

var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }

Localization.SetMode("en"); Check(Localization.T("Ayarlar")=="Settings"&&Localization.T("Nexus, Halberion'a hoş geldin")=="Nexus, welcome to Halberion","English localization and welcome text resolve"); Check(Localization.T("Desteklenmeyen sensör veya API değeri tahmin edilmez.")=="Unsupported sensor or API readings are never estimated.","English capability disclosure resolves");
Localization.SetMode("tr"); Check(Localization.T("Ayarlar")=="Ayarlar"&&Localization.T("Nexus, Halberion'a hoş geldin")=="Nexus, Halberion'a hoş geldin","Turkish localization resolves");
Localization.SetMode("en"); Check(Localization.T("Performansa git")=="View performance"&&Localization.T("Sisteminin gerçek zamanlı görünümü ve araçlarına hızlı erişim.")=="Your real-time system overview and quick access to your tools.","English dashboard navigation and helper text resolve"); Localization.SetMode("tr");

var discovery = await new AdapterDiscoveryService().DiscoverAsync();
Check(discovery.Adapters.Count > 0, "Hotspot module reads real adapters");
Check(!string.IsNullOrWhiteSpace(discovery.SourceProfileName), "Hotspot module reports source profile status");
var hotspot = new HotspotService();
var prepared = hotspot.Prepare(discovery.SourceProfile);
Check(!string.IsNullOrWhiteSpace(prepared.Summary) && !string.IsNullOrWhiteSpace(prepared.Detail), "Hotspot capability result is explicit");
var config = hotspot.ReadCurrentConfiguration();
Check(config.Result.Detail.Contains("Parola günlüğe yazılmadı", StringComparison.Ordinal) || config.Snapshot is null,
    "Hotspot diagnostic does not expose passphrase");

var metricsProvider = new WindowsSystemMetricsProvider();
var firstMetrics = metricsProvider.Capture();
await Task.Delay(250);
var secondMetrics = metricsProvider.Capture();
Check(secondMetrics.TotalMemoryBytes > 0 && secondMetrics.AvailableMemoryBytes <= secondMetrics.TotalMemoryBytes,
    "System module reads physical memory");
Check(!secondMetrics.CpuSampleReady || secondMetrics.CpuUsagePercent is >= 0 and <= 100,
    "System module CPU sample is bounded");
var processSampler = new ProcessUsageSampler(); processSampler.CaptureTop(); await Task.Delay(200);
Check(processSampler.CaptureTop().Count > 0, "System module reads live processes");
Check(new StartupItemScanner().Scan().All(x => !string.IsNullOrWhiteSpace(x.Name)), "System startup inventory is valid");

var networkSampler = new NetworkSampler(); networkSampler.Capture(); await Task.Delay(250); var network = networkSampler.Capture();
Check(network.Adapters.Count > 0 && network.Adapters.All(x => x.Traffic.DownloadBytesPerSecond is null or >= 0),
    "Network module reads bounded adapter traffic");
var preferredAdapter=AdapterSelection.Choose(new(DateTimeOffset.Now,[new AdapterSample("virtual","VMware adapter","VMware virtual adapter","Bağlı",true,"192.168.1.2","",1,new(null,null,0,0,"waiting")),new AdapterSample("physical","Wi-Fi","Wireless adapter","Bağlı",true,"192.168.1.10","192.168.1.1",1,new(null,null,0,0,"waiting"))],[]));
Check(preferredAdapter?.Id=="physical"&&AdapterSelection.Choose(new(DateTimeOffset.Now,[preferredAdapter!],[]),"physical")?.Id=="physical","Network adapter selection prefers an active gateway and honors saved choice");
var ownedConnections = OwnedConnectionReader.ReadIpv4();
Check(ownedConnections.Count > 0 && ownedConnections.All(x => x.ProcessId >= 0 && !string.IsNullOrWhiteSpace(x.Protocol)),
    "Network module maps IPv4 connections to process IDs");
var historyTestRoot = Path.Combine(Path.GetTempPath(), "HalberionNexusNetworkTest-" + Guid.NewGuid().ToString("N"));
try
{
    var history = new NetworkHistoryService(historyTestRoot); var now = DateTimeOffset.Now;
    history.Append(new(now, "Test", 125_000, 62_500, 3));
    var historyRows = history.ReadRecent();
    Check(historyRows.Count == 1 && historyRows[0].ActiveConnections == 3 && historyRows[0].DownloadBytesPerSecond == 125_000, "Network history persists aggregate samples without remote endpoints");
}
finally { if (Directory.Exists(historyTestRoot)) Directory.Delete(historyTestRoot, true); }

var deviceSnapshot = new DeviceSnapshotProvider().Capture();
Check(deviceSnapshot.Devices.Count > 0 && deviceSnapshot.Devices.All(x => !string.IsNullOrWhiteSpace(x.InstanceId)),
    "Device module reads present Windows devices");
Check(deviceSnapshot.AudioEndpoints.Select(x => x.Id).Distinct().Count() == deviceSnapshot.AudioEndpoints.Count,
    "Device module audio endpoint IDs are unique");
Check(deviceSnapshot.Displays.All(x => x.Width >= 0 && x.Height >= 0), "Device module display modes are valid");

Check(StorageScanner.ReadDrives().Count > 0, "Storage module reads ready drives");
using (var sensorService = new HardwareSensorService())
{
    var sensors = sensorService.Read();
    Check(sensors.Count > 0 || !string.IsNullOrWhiteSpace(sensorService.LastError),
        "System sensor provider returns real sensors or an explicit capability error");
    Check(sensors.All(x => float.IsFinite(x.Value) && x.Minimum <= x.Value && x.Maximum >= x.Value),
        "System sensor values and min/max ranges are valid");
}
using(var fpsParser=new PresentMonCaptureService())
{
    Check(!fpsParser.TryParseCsvRow("Application,ProcessID,MsBetweenPresents,MsGPUBusy,MsCPUBusy",out _,out _,out _,out _,out _),"FPS parser recognizes PresentMon CSV headers");
    Check(fpsParser.TryParseCsvRow("\"Sample Game.exe\",1234,10.0,4.0,3.0",out var pid,out var app,out var frame,out var gpuBusy,out var cpuBusy)&&pid==1234&&app=="Sample Game.exe"&&Math.Abs(frame-10)<0.01&&gpuBusy==4&&cpuBusy==3,"FPS parser reads real frame-time metrics");
}
var storageTestRoot = Path.Combine(Path.GetTempPath(), "HalberionNexusStorageTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(storageTestRoot);
try
{
    File.WriteAllBytes(Path.Combine(storageTestRoot, "one.bin"), new byte[1024]);
    Directory.CreateDirectory(Path.Combine(storageTestRoot, "sub"));
    File.WriteAllBytes(Path.Combine(storageTestRoot, "sub", "two.bin"), new byte[2048]);
    var storageScan = StorageScanner.Scan(storageTestRoot, 10);
    Check(storageScan.TotalBytes == 3072 && storageScan.FileCount == 2, "Storage module folder totals are exact");
    Check(storageScan.LargestFiles[0].Bytes == 2048, "Storage module largest-file order is exact");
    File.WriteAllText(Path.Combine(storageTestRoot,"copy-a.txt"),"identical bytes");File.WriteAllText(Path.Combine(storageTestRoot,"copy-b.txt"),"identical bytes");File.WriteAllText(Path.Combine(storageTestRoot,"copy-c.txt"),"different bytes");
    var duplicates=StorageScanner.FindDuplicates(storageTestRoot);
    Check(duplicates.Count==1&&duplicates[0].Copies==2&&duplicates[0].Files.All(x=>x.Bytes=="identical bytes".Length),"Storage duplicate detection verifies matching file contents");
}
finally
{
    var resolved = Path.GetFullPath(storageTestRoot); var temp = Path.GetFullPath(Path.GetTempPath());
    if (resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("HalberionNexusStorageTest-", StringComparison.Ordinal)) Directory.Delete(resolved, true);
}

Console.WriteLine($"{passed} Nexus checks passed. No system state was changed.");
