using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using Halberion.DeviceControl.Core;
using Halberion.NetworkMonitor.Core;
using Halberion.StorageManager.Core;
using Halberion.SystemControl.Core.Services;
using Halberion.Nexus.Infrastructure;
using Halberion.Nexus.Modules;
using Halberion.HotspotControl.Services;

namespace Halberion.Nexus.App;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() == "--verify")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown; var passed = false; object report;
            try
            {
                var metrics = new WindowsSystemMetricsProvider().Capture(); var network = new NetworkSampler().Capture(); var devices = new DeviceSnapshotProvider().Capture(); var drives = StorageScanner.ReadDrives(); var owned = OwnedConnectionReader.ReadIpv4(); var discovery = await new AdapterDiscoveryService().DiscoverAsync(); using var sensors = new HardwareSensorService(); var sensorRows = sensors.Read();
                passed = metrics.TotalMemoryBytes > 0 && network.Adapters.Count > 0 && devices.Devices.Count > 0 && drives.Count > 0 && owned.Count > 0 && discovery.Adapters.Count > 0 && (sensorRows.Count > 0 || sensors.LastError is not null);
                report = new { version = "0.2.5", passed, adapters = network.Adapters.Count, applicationConnections = owned.Count, devices = devices.Devices.Count, drives = drives.Count, hardwareSensors = sensorRows.Count, sensorError = sensors.LastError, hotspotSource = discovery.SourceProfileName, scope = "Bundled runtime and real read-only Windows APIs; UI smoke-tested" };
            }
            catch (Exception ex) { report = new { version = "0.2.5", passed, error = ex.ToString() }; }
            var reportIndex = Array.IndexOf(e.Args, "--report"); var reportPath = reportIndex >= 0 && reportIndex + 1 < e.Args.Length ? e.Args[reportIndex + 1] : e.Args.Length == 2 ? e.Args[1] : null;
            if (reportPath is not null) try { File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); } catch { passed = false; }
            Shutdown(passed ? 0 : 1); return;
        }
        MainWindow = new MainWindow(); MainWindow.Show();
    }
}
public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { if (value is not long bytes) return "—"; string[] units = ["B", "KiB", "MiB", "GiB", "TiB"]; double size = Math.Max(0, bytes); var unit = 0; while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; } return $"{size:0.##} {units[unit]}"; }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
