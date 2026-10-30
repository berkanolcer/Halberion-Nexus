using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Halberion.DeviceControl.Core;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;

namespace Halberion.Nexus.Modules;

public sealed record BluetoothAccessory(DeviceItem Source,string Name,string Kind,string State)
{
    public string Manufacturer=>DeviceView.CleanManufacturer(Source.Manufacturer);
}
public sealed record FriendlyDevice(DeviceItem Source,string Name,string Category,string Manufacturer,string UserStatus);

public partial class DeviceView : UserControl, INexusModule
{
    private readonly NexusServices _services; private DeviceSnapshot? _snapshot; private bool _loaded, _busy;
    public string Id => "device"; public string Title => "Device Control";
    public DeviceView(NexusServices services) { _services = services; InitializeComponent(); CategoryBox.SelectedIndex = 0; }
    public async Task ActivateAsync(CancellationToken cancellationToken) { if (!_loaded) await RefreshAsync(); }
    public Task DeactivateAsync() => Task.CompletedTask;
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        if (_busy) return; _busy = true; StatusText.Text = Localization.IsEnglish?"Checking Bluetooth accessories and Windows devices…":"Bluetooth aksesuarları ve Windows aygıtları okunuyor…";
        try
        {
            _snapshot = await _services.GetDevicesAsync(true); _loaded = true;
            var bluetooth=_snapshot.Devices.Where(IsBluetoothAccessory).DistinctBy(x=>x.InstanceId).Select(x=>new BluetoothAccessory(x,CleanName(x.Name),GuessKind(x.Name),FriendlyState(x))).ToArray();
            BluetoothText.Text = bluetooth.Length.ToString("N0"); UsbText.Text = _snapshot.Devices.Count(x => x.Category == "USB").ToString("N0"); AudioText.Text = _snapshot.AudioEndpoints.Count.ToString("N0"); PowerText.Text = _snapshot.Power.ActiveSchemeName;
            BluetoothGrid.ItemsSource=bluetooth;AudioGrid.ItemsSource = _snapshot.AudioEndpoints; DisplayGrid.ItemsSource = _snapshot.Displays; PowerDetailText.Text = $"Plan: {_snapshot.Power.ActiveSchemeName}\nGüç kaynağı: {_snapshot.Power.PowerSource}\nPil: {_snapshot.Power.BatteryLevel} • {_snapshot.Power.BatteryState}\nWindows durum kodları: {bluetooth.Count(x=>x.Source.ProblemCode!=0)} Bluetooth aygıtında sorun bildiriyor.";
            StatusText.Text = $"{_snapshot.Timestamp:HH:mm:ss} • {bluetooth.Length:N0} Windows tarafından algılanan Bluetooth aygıtı • {_snapshot.Warnings.Count} uyarı"; RenderDevices();
            if(bluetooth.Length>0){BluetoothGrid.SelectedIndex=0;ShowBluetooth((BluetoothAccessory)BluetoothGrid.SelectedItem);}
            else {BluetoothNameText.Text=Localization.IsEnglish?"No Bluetooth accessories are currently listed":"Şu anda Bluetooth aksesuarı listelenmiyor";BluetoothDescriptionText.Text=Localization.IsEnglish?"Pair an accessory in Windows Bluetooth settings, then refresh this view.":"Aygıtı Windows Bluetooth ayarlarından eşleştirip bu ekranı yenileyin.";BluetoothStatusDetailText.Text=Localization.IsEnglish?"Nexus does not claim to control pairing or report a connection when Windows does not provide it.":"Windows bağlantı bilgisi sunmadığında Nexus eşleştirme yaptığını veya bağlantı varmış gibi göstermez.";}
            Localization.Apply(this);
        }
        catch (Exception ex) { StatusText.Text = "Aygıt envanteri okunamadı: " + ex.Message; _services.Log.Add("Device", "Hata", ex.Message); }
        finally { _busy = false; }
    }
    private static bool IsBluetoothAccessory(DeviceItem x)=>(x.Category=="Bluetooth"||x.InstanceId.Contains("BTH",StringComparison.OrdinalIgnoreCase)||x.Name.Contains("Bluetooth",StringComparison.OrdinalIgnoreCase))&&!x.Name.Contains("Enumerator",StringComparison.OrdinalIgnoreCase)&&!x.Name.Contains("Adapter",StringComparison.OrdinalIgnoreCase)&&!x.Name.Contains("Radio",StringComparison.OrdinalIgnoreCase);
    private static string GuessKind(string name)
    {
        if(name.Contains("mouse",StringComparison.OrdinalIgnoreCase)||name.Contains("fare",StringComparison.OrdinalIgnoreCase))return Localization.IsEnglish?"Mouse":"Fare";
        if(name.Contains("keyboard",StringComparison.OrdinalIgnoreCase)||name.Contains("klavye",StringComparison.OrdinalIgnoreCase))return Localization.IsEnglish?"Keyboard":"Klavye";
        if(name.Contains("headphone",StringComparison.OrdinalIgnoreCase)||name.Contains("headset",StringComparison.OrdinalIgnoreCase)||name.Contains("audio",StringComparison.OrdinalIgnoreCase)||name.Contains("kulaklık",StringComparison.OrdinalIgnoreCase))return Localization.IsEnglish?"Headset":"Kulaklık";
        if(name.Contains("controller",StringComparison.OrdinalIgnoreCase)||name.Contains("gamepad",StringComparison.OrdinalIgnoreCase))return Localization.IsEnglish?"Game controller":"Oyun kumandası";
        return Localization.IsEnglish?"Accessory":"Aksesuar";
    }
    private static string FriendlyState(DeviceItem x)=>x.ProblemCode!=0?(Localization.IsEnglish?$"Needs attention · code {x.ProblemCode}":$"İnceleme gerekli · kod {x.ProblemCode}"):x.Status.Equals("Çalışıyor",StringComparison.OrdinalIgnoreCase)?(Localization.IsEnglish?"Ready in Windows":"Windows'ta hazır"):(Localization.IsEnglish?"Detected by Windows":"Windows algılıyor");
    internal static string CleanManufacturer(string value)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Contains('%')){var parts=value.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);value=parts.LastOrDefault(x=>!x.Contains('%'))??"";}
        if(string.IsNullOrWhiteSpace(value)||value.Contains('%'))return Localization.IsEnglish?"Not reported":"Bildirilmiyor";
        return value.Replace("Inc.","",StringComparison.OrdinalIgnoreCase).Replace("Corporation","",StringComparison.OrdinalIgnoreCase).Trim();
    }
    private static string CleanName(string name)=>string.IsNullOrWhiteSpace(name)?(Localization.IsEnglish?"Unnamed accessory":"Adsız aksesuar"):name.Replace("Bluetooth Device (Personal Area Network)",Localization.IsEnglish?"Bluetooth network adapter":"Bluetooth ağ bağdaştırıcısı",StringComparison.OrdinalIgnoreCase).Replace("Bluetooth HID Device",Localization.IsEnglish?"Bluetooth input device":"Bluetooth giriş aygıtı",StringComparison.OrdinalIgnoreCase).Trim();
    private void Bluetooth_SelectionChanged(object sender,SelectionChangedEventArgs e){if(BluetoothGrid.SelectedItem is BluetoothAccessory row)ShowBluetooth(row);}
    private void ShowBluetooth(BluetoothAccessory row){BluetoothNameText.Text=row.Name;BluetoothDescriptionText.Text=Localization.BluetoothDescription(row.Source);BluetoothStatusDetailText.Text=$"{row.State}\n{(Localization.IsEnglish?"Maker":"Üretici")}: {row.Manufacturer}\n{Localization.DeviceDescription(row.Source)}";}
    private void Filter_Changed(object sender, EventArgs e) => RenderDevices();
    private void RenderDevices()
    {
        if (_snapshot is null || CategoryBox is null || SearchBox is null) return; var q = SearchBox.Text.Trim(); var category = Localization.ToTurkish((CategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tümü");
        var rows=_snapshot.Devices.Where(x => (category == "Tümü" || x.Category == category) && (string.IsNullOrEmpty(q) || $"{x.Name} {x.Manufacturer} {x.InstanceId}".Contains(q, StringComparison.OrdinalIgnoreCase))).Select(x=>new FriendlyDevice(x,CleanName(x.Name),Localization.T(x.Category),CleanManufacturer(x.Manufacturer),x.ProblemCode==0?(Localization.IsEnglish?"Working normally":"Sorunsuz"):(Localization.IsEnglish?$"Needs attention ({x.ProblemCode})":$"İnceleme gerekli ({x.ProblemCode})"))).ToArray();
        DevicesGrid.ItemsSource=rows;if(rows.Length>0&&DevicesGrid.SelectedItem is null)DevicesGrid.SelectedIndex=0;
    }
    private void Device_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(DevicesGrid.SelectedItem is not FriendlyDevice row)return;DeviceDescriptionText.Text=Localization.DeviceDescription(row.Source);DeviceStatusDetailText.Text=$"{row.UserStatus} • {row.Source.Status}";DeviceProblemText.Text=row.Source.ProblemCode==0?Localization.ProblemDescription(0):Localization.ProblemDescription(row.Source.ProblemCode);
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string target }) return;
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Windows ekranı açılamadı", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
