using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using Halberion.NetworkMonitor.Core;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;

namespace Halberion.Nexus.Modules;

public partial class NetworkView : UserControl, INexusModule
{
    private readonly NexusServices _services;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private IReadOnlyList<OwnedConnection> _connections = []; private readonly List<OwnedConnection> _alerts = [];
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase); private bool _baseline, _busy; private string? _lastRule;
    private DateTimeOffset _lastHistoryWrite = DateTimeOffset.MinValue;
    private CancellationTokenSource? _pingCancellation;
    private string? _selectedAdapterId; private bool _bindingAdapters;
    public string Id => "network"; public string Title => "Network Monitor";
    public NetworkView(NexusServices services) { _services = services; InitializeComponent(); _selectedAdapterId=services.Settings.Current.NetworkAdapterId; _timer.Tick += async (_, _) => await RefreshAsync(); _services.Settings.Changed += (_, _) => ApplySettings(); ApplySettings(); _lastRule = _services.FirewallJournal.Entries.LastOrDefault()?.RuleName; UndoButton.IsEnabled = _lastRule is not null; HistoryGrid.ItemsSource = _services.NetworkHistory.ReadRecent(); TrafficChart.SizeChanged += (_,_)=>DrawHistory(); }
    public async Task ActivateAsync(CancellationToken cancellationToken) { _timer.Start(); await RefreshAsync(); }
    public Task DeactivateAsync() { _timer.Stop(); _pingCancellation?.Cancel(); return Task.CompletedTask; }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        if (_busy) return; _busy = true;
        try
        {
            var network = await _services.GetNetworkAsync(true); var connections = await Task.Run(OwnedConnectionReader.ReadIpv4); var result = (network, connections);
            _connections = result.Item2; _bindingAdapters=true;
            var previous=_selectedAdapterId??_services.Settings.Current.NetworkAdapterId;
            AdapterBox.ItemsSource=result.Item1.Adapters;
            var chosen=AdapterSelection.Choose(result.Item1,previous);
            if(chosen is not null){AdapterBox.SelectedValue=chosen.Id;_selectedAdapterId=chosen.Id;}
            _bindingAdapters=false; var adapter = chosen;
            AdapterText.Text = adapter is null ? "Hazır ağ adaptörü bulunamadı" : $"{adapter.Name} • {adapter.Status} • IPv4 uygulama eşlemesi";
            var down=adapter?.Traffic.DownloadBytesPerSecond;var up=adapter?.Traffic.UploadBytesPerSecond;
            DownText.Text = Rate(down); UpText.Text = Rate(up); ConnectionCountText.Text = _connections.Count.ToString("N0");
            TrafficNoteText.Text=down is null||up is null?"İlk sayaç aralığı bekleniyor; henüz hız ölçümü yok.":$"{adapter?.Traffic.Note} • Hat hızı: {adapter?.LinkSpeed/1_000_000d:0.#} Mbit/s";
            if (adapter is not null && down is not null && up is not null && DateTimeOffset.Now - _lastHistoryWrite >= TimeSpan.FromSeconds(10)) { _services.NetworkHistory.Append(new(DateTimeOffset.Now, adapter.Name, down.Value, up.Value, _connections.Count)); _lastHistoryWrite = DateTimeOffset.Now; HistoryGrid.ItemsSource = _services.NetworkHistory.ReadRecent(); }
            foreach (var row in _connections.Where(x => x.Remote != "*:*"))
            {
                var key = $"{row.ProcessId}|{row.Remote}|{row.Protocol}"; if (_seen.Add(key) && _baseline && _services.Settings.Current.NetworkAlertsEnabled) { _alerts.Insert(0, row); if (_alerts.Count > 500) _alerts.RemoveAt(_alerts.Count - 1); }
            }
            _baseline = true; AlertCountText.Text = _alerts.Count.ToString("N0"); AlertsGrid.ItemsSource = null; AlertsGrid.ItemsSource = _alerts; RenderConnections(); DrawHistory(); Localization.Apply(this);
        }
        catch (Exception ex) { AdapterText.Text = "Ağ envanteri okunamadı: " + ex.Message; _services.Log.Add("Network", "Hata", ex.Message); }
        finally { _busy = false; }
    }
    private void Search_Changed(object sender, TextChangedEventArgs e) => RenderConnections();
    private void Adapter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if(_bindingAdapters||AdapterBox.SelectedItem is not AdapterSample adapter)return;
        _selectedAdapterId=adapter.Id;
        try { if(_services.Settings.Current.NetworkAdapterId!=adapter.Id)_services.Settings.Save(_services.Settings.Current with{NetworkAdapterId=adapter.Id}); }
        catch(Exception ex){_services.Log.Add("Network","Uyarı","Bağdaştırıcı tercihi kaydedilemedi: "+ex.Message);}
    }
    private void TrafficChart_SizeChanged(object sender,SizeChangedEventArgs e)=>DrawHistory();
    private void DrawHistory()
    {
        if(TrafficChart is null||TrafficChart.ActualWidth<20||TrafficChart.ActualHeight<20)return;
        var selectedName=AdapterBox.SelectedItem is AdapterSample selected?selected.Name:null;
        var rows=_services.NetworkHistory.ReadRecent(120).Where(x=>selectedName is null||x.Adapter==selectedName).OrderBy(x=>x.Time).ToArray();
        if(rows.Length==0){DownloadLine.Points=[];UploadLine.Points=[];return;}
        var max=Math.Max(0.05,rows.Max(x=>Math.Max(x.DownloadBytesPerSecond,x.UploadBytesPerSecond))*8/1_000_000d);
        PointCollection points(double selector)=>new(rows.Select((row,index)=>new Point(4+(TrafficChart.ActualWidth-12)*index/Math.Max(1,rows.Length-1),TrafficChart.ActualHeight-5-Math.Min(1,(selector==0?row.DownloadBytesPerSecond:row.UploadBytesPerSecond)*8/1_000_000d/max)*(TrafficChart.ActualHeight-10))));
        DownloadLine.Points=points(0);UploadLine.Points=points(1);
    }
    private async void Ping_Click(object sender, RoutedEventArgs e)
    {
        if (!LatencyProbe.TryParseTarget(PingTargetBox.Text, out var address) || address is null) { PingSummaryText.Text = "Geçerli bir IPv4 veya IPv6 adresi girin."; return; }
        _pingCancellation?.Cancel(); _pingCancellation?.Dispose(); var pingCancellation = new CancellationTokenSource(); _pingCancellation = pingCancellation; var token = pingCancellation.Token;
        PingButton.IsEnabled = false; PingResultsList.ItemsSource = null; PingSummaryText.Text = $"{address} adresine dört ping gönderiliyor…";
        try
        {
            var result = await LatencyProbe.RunAsync(address, token);
            PingSummaryText.Text = result.AverageMs is double average ? $"Ortalama {average:0.0} ms • Paket kaybı %{result.LossPercent:0}" : $"Yanıt alınamadı • Paket kaybı %{result.LossPercent:0}";
            PingResultsList.ItemsSource = result.Outcomes;
        }
        catch (OperationCanceledException) { PingSummaryText.Text = "Test durduruldu."; }
        catch (Exception ex) { PingSummaryText.Text = "Ping testi başarısız: " + ex.Message; _services.Log.Add("Network", "Uyarı", "Ping testi başarısız: " + ex.Message); }
        finally { if (ReferenceEquals(_pingCancellation, pingCancellation)) { PingButton.IsEnabled = true; _pingCancellation = null; } pingCancellation.Dispose(); }
    }
    private void RenderConnections() { var q = SearchBox.Text.Trim(); ConnectionsGrid.ItemsSource = string.IsNullOrEmpty(q) ? _connections : _connections.Where(x => $"{x.ProcessName} {x.ProcessId} {x.Local} {x.Remote} {x.State} {x.ProcessPath}".Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray(); }
    private async void Block_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsGrid.SelectedItem is not OwnedConnection row || string.IsNullOrWhiteSpace(row.ProcessPath)) { MessageBox.Show("Yolu okunabilen bir uygulama bağlantısı seçin."); return; }
        if (row.ProcessId == Environment.ProcessId) { MessageBox.Show("Nexus kendi ağ erişimini bu ekrandan engellemez."); return; }
        if (MessageBox.Show($"{row.ProcessName} uygulamasının giden ağ erişimi Windows Güvenlik Duvarı ile engellensin mi?\n\n{row.ProcessPath}\n\nWindows yönetici onayı isteyecektir.", "Geri alınabilir engelleme", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _lastRule = "Halberion Nexus - " + row.ProcessId + " - " + Guid.NewGuid().ToString("N")[..8];
        try { var code = await RunNetshAsync(["advfirewall", "firewall", "add", "rule", $"name={_lastRule}", "dir=out", "action=block", $"program={row.ProcessPath}", "enable=yes"]); UndoButton.IsEnabled = code == 0; if (code == 0) _services.FirewallJournal.Add(new(_lastRule, row.ProcessPath, DateTimeOffset.Now)); _services.Log.Add("Network", code == 0 ? "Bilgi" : "Hata", code == 0 ? $"Firewall engeli eklendi: {row.ProcessName}" : $"Firewall kuralı eklenemedi: çıkış {code}"); }
        catch (Exception ex) { _lastRule = null; UndoButton.IsEnabled = false; _services.Log.Add("Network", "Hata", "Firewall işlemi iptal/başarısız: " + ex.Message); MessageBox.Show(ex.Message, "Firewall işlemi tamamlanamadı", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastRule is null) return; try { var code = await RunNetshAsync(["advfirewall", "firewall", "delete", "rule", $"name={_lastRule}"]); if (code == 0) { _services.FirewallJournal.Remove(_lastRule); _services.Log.Add("Network", "Bilgi", "Son firewall engeli geri alındı"); _lastRule = _services.FirewallJournal.Entries.LastOrDefault()?.RuleName; UndoButton.IsEnabled = _lastRule is not null; } }
        catch (Exception ex) { _services.Log.Add("Network", "Hata", "Firewall geri alma iptal/başarısız: " + ex.Message); MessageBox.Show(ex.Message, "Geri alma tamamlanamadı", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private static async Task<int> RunNetshAsync(IEnumerable<string> args)
    {
        var info = new ProcessStartInfo("netsh.exe") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden }; foreach (var arg in args) info.ArgumentList.Add(arg); using var process = Process.Start(info) ?? throw new InvalidOperationException("netsh başlatılamadı"); await process.WaitForExitAsync(); return process.ExitCode;
    }
    private static string Rate(double? bytes) => bytes is double value ? $"{value * 8 / 1_000_000:0.00} Mbit/s" : "—";
    private void ApplySettings() => _timer.Interval = TimeSpan.FromSeconds(_services.Settings.Current.RefreshSeconds);
}
