using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;
using Halberion.SystemControl.Core.Services;

namespace Halberion.Nexus.Modules;

public partial class SystemView : UserControl, INexusModule
{
    private readonly NexusServices _services;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly PresentMonCaptureService _fps = new();
    private IReadOnlyList<HardwareSensorReading> _sensors=[];
    private bool _inventoryLoaded, _busy;private string? _gpuName;
    public string Id => "system";
    public string Title => "System Control";
    public SystemView(NexusServices services)
    {
        _services = services; InitializeComponent(); _timer.Tick += async (_, _) => await RefreshAsync(false); _services.Settings.Changed += (_, _) => ApplySettings(); ApplySettings();
        PresentMonPathBox.Text=services.Settings.Current.PresentMonPath??FindPresentMon()??"";
        _fps.Updated+=(_,rows)=>Dispatcher.Invoke(()=>{FpsGrid.ItemsSource=rows;FpsHeadlineText.Text=rows.FirstOrDefault()?.FpsText??"—";Localization.Apply(this);});
        _fps.StatusChanged+=(_,status)=>Dispatcher.Invoke(()=>{FpsStatusText.Text=status;FpsButton.Content=_fps.IsRunning?"FPS ölçümünü durdur":"FPS ölçümünü başlat";Localization.Apply(this);});
        SensorFilterBox.SelectedIndex=0;
    }
    public async Task ActivateAsync(CancellationToken cancellationToken) { _timer.Start(); await RefreshAsync(!_inventoryLoaded); }
    public Task DeactivateAsync() { _timer.Stop(); _fps.Stop(); return Task.CompletedTask; }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync(true);
    private async Task RefreshAsync(bool inventory)
    {
        if (_busy) return; _busy = true;
        try
        {
            var result = await Task.Run(() =>
            {
                var snapshot = _services.SystemMetrics.Capture(); var processes = _services.ProcessSampler.CaptureTop(40);
                return (snapshot, processes, hardware: inventory ? _services.HardwareInfo.Read() : null, startup: inventory ? _services.StartupItems.Scan() : null);
            });
            CpuText.Text = result.snapshot.CpuSampleReady ? $"%{result.snapshot.CpuUsagePercent:0.0}" : "Örnekleniyor"; CpuBar.Value = result.snapshot.CpuUsagePercent;
            RamText.Text = $"%{result.snapshot.MemoryUsagePercent:0.0}"; RamBar.Value = result.snapshot.MemoryUsagePercent; RamDetailText.Text=$"{Bytes(result.snapshot.TotalMemoryBytes-result.snapshot.AvailableMemoryBytes)} / {Bytes(result.snapshot.TotalMemoryBytes)}"; UptimeText.Text = $"{(int)result.snapshot.Uptime.TotalDays}g {result.snapshot.Uptime.Hours:00}:{result.snapshot.Uptime.Minutes:00}";
            ProcessGrid.ItemsSource = result.processes; UpdatedText.Text = $"{DateTime.Now:HH:mm:ss} • Gerçek Windows sayaçları • {_services.ProcessSampler.LastSkippedProcessCount} korumalı/kapanan işlem atlandı";
            _sensors=await Task.Run(_services.Sensors.Read); RenderSensors();
            var gpu=_sensors.Where(x=>x.Type=="Load"&&IsGpu(x.Hardware)&&x.Sensor.Contains("Core",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.Value).FirstOrDefault();
            var gpuTemps=_sensors.Where(x=>x.Type=="Temperature"&&IsGpu(x.Hardware)).ToArray();var cpuTemps=_sensors.Where(x=>x.Type=="Temperature"&&IsCpu(x.Hardware)).ToArray();
            var fans=_sensors.Where(x=>x.Type=="Fan").ToArray();var gpuMem=_sensors.Where(x=>(x.Type is "Data" or "SmallData")&&IsGpu(x.Hardware)&&x.Sensor.Contains("Memory",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(result.hardware is not null)_gpuName=result.hardware.GraphicsAdapter;
            GpuText.Text=gpu is null?"—":$"%{gpu.Value:0.0}";GpuBar.Value=gpu?.Value??0;GpuDetailText.Text=$"{_gpuName??"Grafik bağdaştırıcısı"} • {string.Join(" / ",gpuMem.Select(x=>x.CurrentText))}".TrimEnd(' ', '•','/');
            TemperatureText.Text=$"CPU {SensorSummary(cpuTemps)}   GPU {SensorSummary(gpuTemps)}";FanText.Text=fans.Length>0?"Fan: "+string.Join(" • ",fans.Take(3).Select(x=>$"{x.Sensor} {x.Value:0} RPM")):"Fan: desteklenen sensör yok";
            SensorCapabilityText.Text=_sensors.Count>0?$"{_sensors.Count:N0} canlı sensör":_services.Sensors.LastError is {Length:>0} error?"Sensörler erişilemiyor":Localization.T("Değer bu donanımda sunulmuyor");
            if(gpu is not null) PerformanceSummaryText.Text=$"GPU: {gpu.Value:0.0}% • {gpu.Hardware} • {gpuTemps.FirstOrDefault()?.CurrentText??"Sıcaklık sensörü yok"} • {gpuMem.FirstOrDefault()?.CurrentText??"GPU bellek sensörü yok"} • FPS kare ölçümü ayrı olarak başlatılır.";
            else PerformanceSummaryText.Text="GPU yükü için donanım sensörü bulunamadı. Görev Yöneticisi veya üretici sürücüsü değer sunmuyorsa Nexus tahmin yürütmez. FPS ölçümü PresentMon ile ayrıca başlatılır.";
            if (result.hardware is not null)
            {
                var h = result.hardware; _gpuName=h.GraphicsAdapter;HardwareText.Text = $"Bilgisayar: {h.ComputerName}\nWindows: {h.OperatingSystem}\nİşlemci: {h.Processor}\nGrafik: {h.GraphicsAdapter}\nMantıksal işlemci: {h.LogicalProcessorCount}\nFiziksel bellek: {Bytes(result.snapshot.TotalMemoryBytes)}\nÇalışma süresi: {UptimeText.Text}\nOkunan sensör: {_sensors.Count}"; StartupGrid.ItemsSource = result.startup; _inventoryLoaded = true;
            }
            Localization.Apply(this);
        }
        catch (Exception ex) { UpdatedText.Text = "Ölçüm hatası: " + ex.Message; _services.Log.Add("System", "Hata", ex.Message); }
        finally { _busy = false; }
    }
    private void RenderSensors()
    {
        if(SensorGrid is null||SensorFilterBox.SelectedItem is not ComboBoxItem item)return;
        var key=item.Tag?.ToString()??"all";
        var rows=key switch{"cpu"=>_sensors.Where(x=>IsCpu(x.Hardware)),"gpu"=>_sensors.Where(x=>IsGpu(x.Hardware)),"memory"=>_sensors.Where(x=>x.Hardware.Contains("Memory",StringComparison.OrdinalIgnoreCase)||x.Type is "Data" or "SmallData"),"all"=>_sensors,_=>_sensors.Where(x=>x.Type.Equals(key,StringComparison.OrdinalIgnoreCase))};
        SensorGrid.ItemsSource=rows.ToArray();SensorStatusText.Text=_sensors.Count>0?$"{_sensors.Count:N0} canlı donanım sensörü • Min/maks bu oturumda kaydedilir • Görüntülenen: {SensorGrid.Items.Count:N0}":"Sensör okunamadı: "+(_services.Sensors.LastError??"Donanım veya izin sağlayıcısı değer sunmadı");
    }
    private void SensorFilter_Changed(object sender,SelectionChangedEventArgs e)=>RenderSensors();
    private async void BrowsePresentMon_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Title="Resmi PresentMon yürütülebilir dosyasını seçin",Filter="PresentMon (*.exe)|*.exe",FileName="PresentMon-2.3.1-x64.exe",CheckFileExists=true};
        if(dialog.ShowDialog()!=true)return;if(!PresentMonCaptureService.IsPresentMonPath(dialog.FileName)){MessageBox.Show("Seçilen dosyanın adı PresentMon ile başlamalıdır.","PresentMon",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        PresentMonPathBox.Text=dialog.FileName;_services.Settings.Save(_services.Settings.Current with{PresentMonPath=dialog.FileName});
    }
    private void Fps_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            if(_fps.IsRunning){_fps.Stop();return;}
            var path=PresentMonPathBox.Text.Trim();if(!PresentMonCaptureService.IsPresentMonPath(path)){FpsStatusText.Text="Resmi PresentMon x64 aracını seçin veya ‘PresentMon indir’ düğmesini kullanın.";return;}
            _services.Settings.Save(_services.Settings.Current with{PresentMonPath=path});FpsGrid.ItemsSource=null;FpsHeadlineText.Text="—";_fps.Start(path);
        }
        catch(Exception ex){FpsStatusText.Text="FPS capture başlatılamadı: "+ex.Message;_services.Log.Add("System","Uyarı",ex.Message);}
    }
    private void PresentMonLink_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("https://github.com/GameTechDev/PresentMon/releases/latest"){UseShellExecute=true});
    private static string? FindPresentMon()
    {
        var candidates=new[]{Path.Combine(AppContext.BaseDirectory,"PresentMon-x64.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Intel","PresentMon","PresentMon-x64.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PresentMon","PresentMon-x64.exe")};return candidates.FirstOrDefault(PresentMonCaptureService.IsPresentMonPath);
    }
    private static bool IsGpu(string h)=>h.Contains("GPU",StringComparison.OrdinalIgnoreCase)||h.Contains("NVIDIA",StringComparison.OrdinalIgnoreCase)||h.Contains("AMD",StringComparison.OrdinalIgnoreCase)||h.Contains("Intel Graphics",StringComparison.OrdinalIgnoreCase);
    private static bool IsCpu(string h)=>h.Contains("CPU",StringComparison.OrdinalIgnoreCase)||h.Contains("Processor",StringComparison.OrdinalIgnoreCase)||h.Contains("Intel Core",StringComparison.OrdinalIgnoreCase)||h.Contains("Ryzen",StringComparison.OrdinalIgnoreCase);
    private static string SensorSummary(IReadOnlyList<HardwareSensorReading> readings)=>readings.FirstOrDefault()?.CurrentText??"—";
    private static string Bytes(ulong value){double n=value;string[] u=["B","MiB","GiB","TiB"];var i=0;while(n>=1024&&i<u.Length-1){n/=1024;i++;}return $"{n:0.0} {u[i]}";}
    private void ApplySettings() => _timer.Interval = TimeSpan.FromSeconds(_services.Settings.Current.RefreshSeconds);
}
