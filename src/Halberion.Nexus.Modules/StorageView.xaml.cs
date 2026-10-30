using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;
using Halberion.StorageManager.Core;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace Halberion.Nexus.Modules;

public partial class StorageView : UserControl, INexusModule
{
    private readonly NexusServices _services; private string? _folder; private CancellationTokenSource? _cts,_duplicateCts; private TempCandidateResult? _tempResult; private IReadOnlyList<DuplicateFileGroup> _duplicateGroups=[]; private bool _loaded;
    public string Id => "storage"; public string Title => "Storage Manager";
    public StorageView(NexusServices services) { _services = services; InitializeComponent(); }
    public async Task ActivateAsync(CancellationToken cancellationToken) { if (!_loaded) { _loaded = true; await RefreshDrivesAsync(); } }
    public Task DeactivateAsync() { _cts?.Cancel();_duplicateCts?.Cancel(); return Task.CompletedTask; }
    private async void RefreshDrives_Click(object sender, RoutedEventArgs e) => await RefreshDrivesAsync();
    private async Task RefreshDrivesAsync()
    {
        try
        {
            // Drive enumeration can block on removable/network volumes. Keep it
            // off the UI thread so a slow or disconnected volume cannot make the
            // page appear frozen while it opens.
            var drives = await Task.Run(StorageScanner.ReadDrives);
            DrivesGrid.ItemsSource = drives;
            StatusText.Text = "Disk envanteri yenilendi • Salt-okunur";
            UsedTotalText.Text = ByteSize(SafeTotal(drives.Select(x => x.UsedBytes)));
            FreeTotalText.Text = ByteSize(SafeTotal(drives.Select(x => x.FreeBytes)));
            UsedDriveCountText.Text = $"{drives.Count} hazır sürücü • {drives.Count(x => x.UsedPercent >= 90)} sürücüde %90 üzeri kullanım";

            HealthText.Text = await ReadPhysicalDiskHealthAsync();
            HealthSummaryText.Text = HealthText.Text.Contains("Healthy", StringComparison.OrdinalIgnoreCase) || HealthText.Text.Contains("Sağlıklı", StringComparison.OrdinalIgnoreCase)
                ? (Localization.IsEnglish ? "Reported healthy" : "Sağlıklı bildirildi")
                : Localization.IsEnglish ? "Review details" : "Ayrıntıyı inceleyin";
            Localization.Apply(this);
        }
        catch (Exception ex)
        {
            // Keep navigation usable and surface the failure in the page/log.
            _loaded = false;
            DrivesGrid.ItemsSource = Array.Empty<object>();
            UsedTotalText.Text = "—";
            FreeTotalText.Text = "—";
            UsedDriveCountText.Text = "Disk bilgisi alınamadı";
            HealthSummaryText.Text = "Bilgi alınamadı";
            HealthText.Text = ex.Message;
            StatusText.Text = "Disk bilgisi okunamadı; Yenile ile tekrar deneyin.";
            _services.Log.Add("Storage", "Hata", ex.ToString());
            Localization.Apply(this);
        }
    }

    private static long SafeTotal(IEnumerable<long> values)
    {
        long total = 0;
        foreach (var value in values)
        {
            var positive = Math.Max(0, value);
            total = positive > long.MaxValue - total ? long.MaxValue : total + positive;
        }
        return total;
    }
    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Analiz edilecek klasör", Multiselect = false, InitialDirectory = _folder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        if (dialog.ShowDialog() != true) return; _folder = Path.GetFullPath(dialog.FolderName); FolderText.Text = _folder; ScanButton.IsEnabled = true;DuplicateScanButton.IsEnabled=true;DuplicateSummaryText.Text=$"Seçili klasör: {_folder}";
    }
    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_folder is null) return; _cts?.Dispose(); _cts = new(); ScanButton.IsEnabled = false; CancelButton.IsEnabled = true; StatusText.Text = "Klasör taranıyor…";
        try
        {
            var progress = new Progress<ScanProgress>(x => StatusText.Text = $"{x.Files:N0} dosya • {ByteSize(x.Bytes)}"); var result = await StorageScanner.ScanAsync(_folder, 100, progress, _cts.Token); FolderGrid.ItemsSource = result.TopDirectories; LargestGrid.ItemsSource = result.LargestFiles; StatusText.Text = $"{result.FileCount:N0} dosya • {ByteSize(result.TotalBytes)} • {result.SkippedCount} atlandı • Değişiklik yapılmadı";ScanSummaryText.Text=$"{result.DirectoryCount:N0} klasör • {result.Duration.TotalSeconds:0.0} saniye • En büyük 100 dosya • {result.SkippedCount} okunamayan öğe";Localization.Apply(this);
        }
        catch (OperationCanceledException) { StatusText.Text = "Klasör analizi iptal edildi"; }
        catch (Exception ex) { StatusText.Text = "Analiz hatası: " + ex.Message; _services.Log.Add("Storage", "Hata", ex.Message); }
        finally { ScanButton.IsEnabled = true; CancelButton.IsEnabled = false; }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
    private void OpenLargest_Click(object sender,RoutedEventArgs e){if(LargestGrid.SelectedItem is not FileEntry file)return;OpenInExplorer(file.Path);}
    private async void Duplicates_Click(object sender,RoutedEventArgs e)
    {
        if(_folder is null)return;_duplicateCts?.Cancel();_duplicateCts?.Dispose();_duplicateCts=new();var token=_duplicateCts.Token;DuplicateScanButton.IsEnabled=false;DuplicateCancelButton.IsEnabled=true;DuplicateSummaryText.Text="Dosyalar boyut ve SHA-256 ile salt-okunur karşılaştırılıyor…";
        try{var progress=new Progress<ScanProgress>(x=>DuplicateSummaryText.Text=$"{x.Files:N0} dosya incelendi • {ByteSize(x.Bytes)}");_duplicateGroups=await StorageScanner.FindDuplicatesAsync(_folder,progress,token);DuplicateGrid.ItemsSource=_duplicateGroups.Select(x=>new DuplicateViewRow(x)).ToArray();var total=_duplicateGroups.Sum(x=>x.PotentialDuplicateBytes);DuplicateSummaryText.Text=$"{_duplicateGroups.Count:N0} aynı içerikli grup • {ByteSize(total)} olası yinelenen veri • Hiçbir dosya değiştirilmedi veya silinmedi.";if(DuplicateGrid.Items.Count>0)DuplicateGrid.SelectedIndex=0;Localization.Apply(this);}
        catch(OperationCanceledException){DuplicateSummaryText.Text="Yinelenen dosya taraması iptal edildi.";}
        catch(Exception ex){DuplicateSummaryText.Text="Yinelenen dosya taranamadı: "+ex.Message;_services.Log.Add("Storage","Hata",ex.Message);}
        finally{DuplicateScanButton.IsEnabled=true;DuplicateCancelButton.IsEnabled=false;}
    }
    private void DuplicateCancel_Click(object sender,RoutedEventArgs e)=>_duplicateCts?.Cancel();
    private void Duplicate_SelectionChanged(object sender,SelectionChangedEventArgs e){if(DuplicateGrid.SelectedItem is DuplicateViewRow row)DuplicateSummaryText.Text=$"{row.Files.Copies} dosyanın içerikleri SHA-256 ile aynı • {ByteSize(row.Files.PotentialDuplicateBytes)} yinelenen veri • Silme işlemi sunulmaz.";}
    private void OpenDuplicate_Click(object sender,RoutedEventArgs e){if(DuplicateGrid.SelectedItem is DuplicateViewRow row)OpenInExplorer(row.Files.Files[0].Path);}
    private static void OpenInExplorer(string path){try{var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};info.ArgumentList.Add("/select,");info.ArgumentList.Add(path);Process.Start(info);}catch(Exception ex){MessageBox.Show(ex.Message,"Konum açılamadı",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    public sealed class DuplicateViewRow
    {
        public DuplicateFileGroup Files{get;}public int Copies=>Files.Copies;public string DisplayName=>Files.Files[0].Name;public string SizeText=>ByteSize(Files.Bytes);public string RepeatedDataText=>ByteSize(Files.PotentialDuplicateBytes);public string ShortHash=>Files.Sha256[..Math.Min(12,Files.Sha256.Length)];public DuplicateViewRow(DuplicateFileGroup files)=>Files=files;
    }
    private async void TempScan_Click(object sender, RoutedEventArgs e)
    {
        TempScanButton.IsEnabled = false; StatusText.Text = "Geçici dosya adayları salt-okunur taranıyor…";
        try { _tempResult = await StorageScanner.FindTempCandidatesAsync(Path.GetTempPath(), DateTime.UtcNow.AddDays(-_services.Settings.Current.TemporaryFileAgeDays), 500, CancellationToken.None); TempGrid.ItemsSource = _tempResult.Candidates; StatusText.Text = $"{_tempResult.CandidateCount:N0} aday • {ByteSize(_tempResult.TotalBytes)} • Henüz silme yapılmadı"; }
        catch (Exception ex) { StatusText.Text = "Geçici tarama hatası: " + ex.Message; }
        finally { TempScanButton.IsEnabled = true; }
    }
    private void Recycle_Click(object sender, RoutedEventArgs e)
    {
        if (TempGrid.SelectedItem is not TempCandidate candidate || _tempResult is null) { MessageBox.Show("Önce listeden bir dosya seçin."); return; }
        try
        {
            var full = Path.GetFullPath(candidate.Path); var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !_tempResult.Candidates.Any(x => string.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase)) || candidate.LastWriteUtc >= _tempResult.CutoffUtc) throw new InvalidOperationException("Dosya doğrulanmış geçici aday kapsamı dışında.");
            if (MessageBox.Show($"Bu dosya Geri Dönüşüm Kutusu'na gönderilsin mi?\n\n{full}\n{ByteSize(candidate.Bytes)} • {candidate.AgeDays} gün", "Dosya temizleme onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            FileSystem.DeleteFile(full, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); _services.Log.Add("Storage", "Bilgi", $"Geçici dosya geri dönüşüme gönderildi: {candidate.Name}"); TempGrid.ItemsSource = _tempResult.Candidates.Where(x => !string.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase)).ToArray(); StatusText.Text = "Seçili dosya Geri Dönüşüm Kutusu'na gönderildi";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Dosya temizlenemedi", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private static async Task<string> ReadPhysicalDiskHealthAsync()
    {
        try
        {
            var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive"); info.ArgumentList.Add("-Command"); info.ArgumentList.Add("Get-PhysicalDisk | ForEach-Object { \"$($_.FriendlyName): $($_.HealthStatus) / $($_.OperationalStatus) / $($_.MediaType)\" }");
            using var process = Process.Start(info) ?? throw new InvalidOperationException("PowerShell başlatılamadı");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var output = await outputTask;
                var error = await errorTask;
                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : "Disk sağlık sağlayıcısı kullanılamadı: " + (string.IsNullOrWhiteSpace(error) ? "Windows veri döndürmedi" : error.Trim());
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                return "Disk sağlık sağlayıcısı 8 saniyede yanıt vermedi; disk envanteri kullanılabilir.";
            }
        }
        catch (Exception ex) { return "Disk sağlık sağlayıcısı kullanılamadı: " + ex.Message; }
    }
    private static string ByteSize(long bytes) { string[] units = ["B", "KiB", "MiB", "GiB", "TiB"]; double size = Math.Max(0, bytes); var unit = 0; while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; } return $"{size:0.##} {units[unit]}"; }
}
