using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Halberion.HotspotControl.Models;
using Halberion.HotspotControl.Services;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;
using Microsoft.Win32;
using Windows.Networking.NetworkOperators;

namespace Halberion.Nexus.Modules;

public partial class HotspotView : UserControl, INexusModule
{
    private readonly NexusServices _services;
    private HotspotService Hotspot => _services.Hotspot;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private DiscoverySnapshot? _snapshot;
    private bool _initialized;
    public string Id => "hotspot";
    public string Title => "Hotspot Control";

    public HotspotView(NexusServices services)
    {
        _services = services;
        InitializeComponent();
        BandBox.SelectedIndex = 0;
        _timer.Tick += (_, _) => RenderLive();
    }

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        _timer.Start();
        if (!_initialized) { _initialized = true; await RefreshAsync(cancellationToken); }
        else RenderLive();
    }
    public Task DeactivateAsync() { _timer.Stop(); return Task.CompletedTask; }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync(CancellationToken.None);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        SetBusy(true); Append("Bağlantılar ve Windows hotspot yeteneği okunuyor.");
        try
        {
            _snapshot = await _services.GetHotspotDiscoveryAsync(true, cancellationToken);
            var prepared = Hotspot.Prepare(_snapshot.SourceProfile);
            SourceText.Text = $"Kaynak: {_snapshot.SourceProfileName} • Wi-Fi: {_snapshot.KillerWiFiAdapter?.Name ?? "Bulunamadı"}";
            ApiText.Text = prepared.Summary + " — " + prepared.Detail;
            Append(prepared.Summary + ": " + prepared.Detail);
            var (config, result) = Hotspot.ReadCurrentConfiguration();
            if (config is not null)
            {
                SsidBox.Text = config.Ssid; PasswordBox.Password = config.Passphrase;
                BandBox.SelectedIndex = config.Band == TetheringWiFiBand.TwoPointFourGigahertz ? 1 : config.Band == TetheringWiFiBand.FiveGigahertz ? 2 : 0;
            }
            Append(result.Summary + ": " + result.Detail);
            foreach (var note in _snapshot.Notes) Append(note);
            RenderLive();
        }
        catch (OperationCanceledException) { Append("Yenileme iptal edildi."); }
        catch (Exception ex) { Append("Hata: " + ex.Message); ApiText.Text = "Hotspot API okunamadı: " + ex.Message; }
        finally { SetBusy(false); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null) return;
        if (!_snapshot.IsPdaNetExactMatch && MessageBox.Show("PdaNet adlı kesin kaynak profil bulunamadı. Windows'un seçtiği etkin profille devam edilsin mi?", "Kaynak doğrulaması", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunNetworkOperation(() => Hotspot.StartAsync(SsidBox.Text, PasswordBox.Password, SelectedBand()));
    }
    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Hotspot durdurulsun mu? Bağlı cihazların bağlantısı kesilir.", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunNetworkOperation(Hotspot.StopAsync);
    }
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var restart = !Hotspot.IsActive || MessageBox.Show("Çalışan hotspot yeni ayarlar için yeniden başlatılacak. Devam edilsin mi?", "Yeniden başlatma onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        if (!restart) return;
        await RunNetworkOperation(() => Hotspot.ApplyConfigurationAsync(SsidBox.Text, PasswordBox.Password, SelectedBand(), true));
    }
    private async Task RunNetworkOperation(Func<Task<HotspotResult>> operation)
    {
        SetBusy(true);
        try
        {
            using var gate = await _services.Operations.EnterAsync("NetworkTethering", CancellationToken.None);
            var result = await operation(); Append(result.Summary + ": " + result.Detail);
            _services.Log.Add("Hotspot", result.Success ? "Bilgi" : "Hata", result.Summary);
            ApiText.Text = result.Summary + " — " + result.Detail;
        }
        catch (Exception ex) { Append("Hata: " + ex.Message); }
        finally { SetBusy(false); RenderLive(); }
    }
    private void Qr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = QrCodeService.CreateWifiPng(SsidBox.Text, PasswordBox.Password);
            var dialog = new SaveFileDialog { Filter = "PNG görüntüsü|*.png", FileName = "halberion-wifi-qr.png" };
            if (dialog.ShowDialog() == true) File.WriteAllBytes(dialog.FileName, bytes);
            using var stream = new MemoryStream(bytes); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            var window = new Window { Title = "Wi-Fi QR", Width = 430, Height = 480, Owner = Window.GetWindow(this), Background = (System.Windows.Media.Brush)FindResource("PageBrush"), Content = new Image { Source = bitmap, Margin = new Thickness(24) } }; window.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "QR oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void RenderLive()
    {
        StateText.Text = Hotspot.IsActive ? "AÇIK" : Hotspot.OperationalState?.ToString().ToUpperInvariant() ?? "HAZIR DEĞİL";
        var clients = Hotspot.GetConnectedDevices(); ClientsGrid.ItemsSource = clients; ClientCountText.Text = $"Bağlı cihaz: {clients.Count}";
    }
    private void SetBusy(bool busy) { StartButton.IsEnabled = !busy; StopButton.IsEnabled = !busy; }
    private void Append(string text) { DiagnosticBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}"); DiagnosticBox.ScrollToEnd(); }
    private TetheringWiFiBand SelectedBand() => BandBox.SelectedIndex == 1 ? TetheringWiFiBand.TwoPointFourGigahertz : BandBox.SelectedIndex == 2 ? TetheringWiFiBand.FiveGigahertz : TetheringWiFiBand.Auto;
}
