using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;
using Halberion.StorageManager.Core;

namespace Halberion.Nexus.Modules;

public partial class DashboardView : UserControl, INexusModule
{
    private readonly NexusServices _services; private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) }; private bool _busy; private string? _gpu;
    public event EventHandler<string>? NavigationRequested;
    public string Id => "dashboard"; public string Title => "Anasayfa";
    public DashboardView(NexusServices services) { _services = services; InitializeComponent(); _timer.Tick += async (_, _) => await RefreshAsync(); _services.Settings.Changed += (_, _) => ApplySettings(); ApplySettings(); }
    public async Task ActivateAsync(CancellationToken cancellationToken) { _timer.Start(); await RefreshAsync(); }
    public Task DeactivateAsync() { _timer.Stop(); return Task.CompletedTask; }
    private async Task RefreshAsync()
    {
        if (_busy) return; _busy = true;
        try
        {
            var systemTask = _services.GetSystemAsync(); var networkTask = _services.GetNetworkAsync(); var deviceTask = _services.GetDevicesAsync();
            await Task.WhenAll(systemTask, networkTask, deviceTask); var system = systemTask.Result; var network = networkTask.Result; var device = deviceTask.Result;
            var drives = StorageScanner.ReadDrives(); var adapter = AdapterSelection.Choose(network, _services.Settings.Current.NetworkAdapterId);
            var sensors = await Task.Run(_services.Sensors.Read);
            CpuText.Text = system.CpuSampleReady ? $"%{system.CpuUsagePercent:0.0}" : Localization.T("Örnek"); CpuBar.Value = system.CpuUsagePercent; RamText.Text = $"%{system.MemoryUsagePercent:0.0}"; RamBar.Value = system.MemoryUsagePercent;
            RamDetailText.Text = $"{Bytes(system.TotalMemoryBytes-system.AvailableMemoryBytes)} / {Bytes(system.TotalMemoryBytes)}";
            var gpuLoad=sensors.Where(x=>x.Type=="Load"&&x.Hardware.Contains("GPU",StringComparison.OrdinalIgnoreCase)&&x.Sensor.Contains("Core",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.Value).FirstOrDefault();
            var gpuTemp=sensors.FirstOrDefault(x=>x.Type=="Temperature"&&x.Hardware.Contains("GPU",StringComparison.OrdinalIgnoreCase)); var gpuMem=sensors.FirstOrDefault(x=>x.Type is "SmallData" or "Data"&&x.Hardware.Contains("GPU",StringComparison.OrdinalIgnoreCase)&&x.Sensor.Contains("Memory",StringComparison.OrdinalIgnoreCase));
            GpuText.Text=gpuLoad is null?"—":$"%{gpuLoad.Value:0}"; GpuBar.Value=gpuLoad?.Value??0; GpuDetailText.Text=string.Join(" • ",new[]{gpuTemp is null?null:$"{gpuTemp.Value:0} °C",gpuMem?.CurrentText}.Where(x=>!string.IsNullOrWhiteSpace(x))) is {Length:>0} info?info:Localization.T("GPU sensörü desteklenmiyor");
            var total = drives.Sum(x => (decimal)x.TotalBytes); var used = drives.Sum(x => (decimal)x.UsedBytes); var diskPercent = total == 0 ? 0 : (double)(used * 100 / total); DiskText.Text = $"%{diskPercent:0.0}"; DiskDetailText.Text = $"{drives.Count} {Localization.T("hazır sürücü")}";
            NetworkText.Text = $"↓ {Rate(adapter?.Traffic.DownloadBytesPerSecond)}"; NetworkDetailText.Text = $"↑ {Rate(adapter?.Traffic.UploadBytesPerSecond)} • {adapter?.Name ?? Localization.T("Adaptör yok")}{(adapter is null?"":$" • {Localization.T(adapter.IsUp?"Bağlı":"Bağlı değil")}")}";
            DeviceText.Text = $"{device.Devices.Count:N0} {Localization.T("aygıt")}"; var errors = _services.Log.Snapshot.Count(x => x.Level == "Hata"); AlertText.Text = $"{device.Warnings.Count} {Localization.T("aygıt uyarısı")} • {errors} {Localization.T("Nexus hatası")}";
            _gpu ??= _services.HardwareInfo.Read().GraphicsAdapter; SummaryText.Text = $"{Localization.T("GPU")}: {_gpu}\n{Localization.T("Hotspot durumu")}: {Localization.T(_services.Hotspot.OperationalState?.ToString()??"Henüz taranmadı")} • {Localization.T("Bağlı cihaz")}: {_services.Hotspot.ClientCount}\n{(gpuLoad is null?Localization.T("GPU yük sensörü desteklenmiyor."):$"{Localization.T("GPU YÜKÜ")}: %{gpuLoad.Value:0.0}")}\n{Localization.T("Desteklenmeyen sensör veya API değeri tahmin edilmez.")}"; UpdatedText.Text = $"{DateTime.Now:HH:mm:ss} • {Localization.T("Gerçek Windows sayaçları")} • {adapter?.Name??Localization.T("Ağ adaptörü seçilmedi")}";
            Localization.Apply(this);
        }
        catch (Exception ex) { SummaryText.Text = "Dashboard verisi kısmen okunamadı: " + ex.Message; _services.Log.Add("Dashboard", "Hata", ex.Message); }
        finally { _busy = false; }
    }
    private static string Rate(double? bytes) => bytes is double value ? $"{value * 8 / 1_000_000:0.00} Mbit/s" : "—";
    private static string Bytes(ulong value){double n=value;string[] u=["B","MiB","GiB","TiB"];var i=0;while(n>=1024&&i<u.Length-1){n/=1024;i++;}return $"{n:0.0} {u[i]}";}
    private void ApplySettings() => _timer.Interval = TimeSpan.FromSeconds(_services.Settings.Current.RefreshSeconds);
    private void QuickNavigate_Click(object sender,RoutedEventArgs e){if(sender is Button{Tag:string id})NavigationRequested?.Invoke(this,id);}
}
