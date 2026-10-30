using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Halberion.DeviceControl.Core;

namespace Halberion.Nexus.Modules;

public static class Localization
{
    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["Anasayfa"]="Home",["Dashboard"]="Dashboard",["Ayarlar"]="Settings",["Kayıtlar"]="Activity log",["Hakkında"]="About",
        ["System Control"]="System Control",["Network Monitor"]="Network Monitor",["Device Control"]="Device Control",["Storage Manager"]="Storage Manager",["Hotspot Control"]="Hotspot Control",
        ["Ayarlar yerel kullanıcı profilinde saklanır; parola veya gizli bilgi içermez."]="Settings are stored in your local profile and contain no passwords or secrets.",
        ["Canlı yenileme aralığı"]="Live refresh interval",["Yeni ağ bağlantısı uyarılarını etkinleştir"]="Enable new network connection alerts",["Geçici dosya aday yaşı"]="Temporary file minimum age",["Ayarları kaydet"]="Save settings",["Ayarlar kaydedildi."]="Settings saved.",
        ["Sistem özeti"]="System overview",["Hızlı erişim"]="Quick access",["Son ölçümler"]="Live readings",["CPU"]="CPU",["RAM"]="RAM",["DİSK"]="STORAGE",["AĞ"]="NETWORK",["PDANET / HOTSPOT"]="PDANET / HOTSPOT",["AYGITLAR / UYARILAR"]="DEVICES / ALERTS",["Durum"]="Status",
        ["Gerçek veriler • Açık onay • Geri alınabilir işlemler"]="Live data • Explicit consent • Reversible actions",["Dashboard etkin • Yönetici yetkisi yalnız açıkça onaylanan işlemde istenir"]="Dashboard active • Admin rights are requested only for actions you approve",
        ["Aygıtların ne işe yaradığını öğrenin; Bluetooth aksesuarlarınızın bağlantı durumunu görün."]="Learn what your devices do and check the connection status of Bluetooth accessories.",
        ["Uygulama internetini ve bağlantı geçmişini inceleyin; erişim kurallarını siz yönetin."]="Review app network activity and history; manage access rules yourself.",
        ["Disk alanını bulun, klasörleri analiz edin ve güvenli temizlik adaylarını inceleyin."]="Review disk usage, analyze folders, and inspect safe cleanup candidates.",
        ["Sistem kaynaklarını, grafik işlemcisini ve donanım sensörlerini canlı izleyin."]="Monitor system resources, graphics processors, and hardware sensors live.",
        ["Sistem durumu"]="System status",["Performans"]="Performance",["Sıcaklık ve fan"]="Temperatures & fans",["Sensörler"]="Sensors",["İşlemler"]="Processes",["Başlangıç"]="Startup apps",["Donanım"]="Hardware",
        ["Aygıtlar"]="Devices",["Bluetooth aksesuarları"]="Bluetooth accessories",["Aygıt Yöneticisi"]="Device Manager",["Ses"]="Audio",["Ekran ve güç"]="Display & power",["Açıklama"]="What it is",["Sorun açıklaması"]="Issue details",["Sorunsuz"]="Working normally",["Bilinmiyor"]="Unknown",["Yenile"]="Refresh",
        ["Diskleri yenile"]="Refresh drives",["Diskler"]="Drives",["Klasör analizi"]="Folder analysis",["Güvenli temizlik"]="Safe cleanup",["Klasör seç"]="Choose folder",["Analiz et"]="Analyze",["İptal"]="Cancel",["Geçici adayları tara"]="Scan temporary files",["Seçili dosyayı Geri Dönüşüm Kutusu'na gönder"]="Send selected file to Recycle Bin",
        ["Uygulamalar ve bağlantılar"]="Apps & connections",["Trafik geçmişi"]="Traffic history",["Gecikme testi"]="Latency test",["Yeni bağlantı uyarıları"]="New connection alerts",["Kapsam ve gizlilik"]="Scope & privacy",["Seçili uygulamayı engelle"]="Block selected app",["Son engeli geri al"]="Undo last block",["İNDİRME"]="DOWNLOAD",["YÜKLEME"]="UPLOAD",["ÇALIŞMA SÜRESİ"]="UPTIME",["SENSÖR DURUMU"]="SENSOR STATUS",["BLUETOOTH"]="BLUETOOTH",["USB"]="USB",["GÜÇ PLANI"]="POWER PLAN",["SES UCU"]="AUDIO ENDPOINTS",["Tümü"]="All",["Otomatik"]="Automatic",["Bağlı"]="Connected",["Bağlı değil"]="Disconnected",
        ["Nexus, Halberion'a hoş geldin"]="Nexus, welcome to Halberion",["Devam et"]="Continue",["Hoş geldin"]="Welcome",["Hemen başla"]="Get started",["FPS — oyun çalışırken ölçüm başlatın"]="FPS — start a capture while a game is running",["FPS ölçümünü başlat"]="Start FPS capture",["FPS ölçümünü durdur"]="Stop FPS capture",["GPU YÜKÜ"]="GPU LOAD",["GPU BELLEĞİ"]="GPU MEMORY",["CPU SICAKLIĞI"]="CPU TEMPERATURE",["GPU SICAKLIĞI"]="GPU TEMPERATURE",["FAN HIZI"]="FAN SPEED",
        ["Fare"]="Mouse",["Klavye"]="Keyboard",["Kulaklık"]="Headphones",["Ses aygıtı"]="Audio device",["Depolama"]="Storage",["Ağ bağdaştırıcısı"]="Network adapter",["Ekran"]="Display",["Giriş aygıtı"]="Input device",["Güç planı"]="Power plan",["Sorun yok"]="No issue",["Aygıt durumu okunamadı"]="Device status is unavailable",
        ["Accurate game FPS capture requires the official PresentMon tool. No value is fabricated when unavailable."]="Accurate game FPS capture requires the official PresentMon tool. No value is fabricated when unavailable.",
        ["Yerel Windows sayacı"]="Local Windows counter",["Ölçüm hazırlanıyor…"]="Preparing measurement…",["Gerçek donanım sensörleri"]="Live hardware sensors",["Değer bu donanımda sunulmuyor"]="This hardware does not report this value",["Performans artışı veya FPS/RAM kazanımı garanti edilmez."]="No performance, FPS, or memory gains are guaranteed."
        ,["Sistem dili"]="System language",["Türkçe"]="Turkish",["Yöneticilik yalnız seçili işlem için açık onay sonrasında istenir."]="Administrator approval is requested only after you explicitly approve an action.",
        ["Klasör seçilmedi"]="No folder selected",["Fiziksel disk sağlık bilgisi"]="Physical disk health",["Yalnız Windows geçici klasöründe, ayarladığınız yaştan eski dosyalar incelenir. Seçilen tek dosya için ayrıca onay alınır ve Geri Dönüşüm Kutusu'na gönderilir. Kullanıcı belgeleri, klasörler, yeniden ayrıştırma noktaları ve Unity önbelleği otomatik silinmez."]="Only files in the Windows temporary folder older than your selected age are reviewed. A separate confirmation is required for each selected file, which is sent to the Recycle Bin. User documents, folders, reparse points, and Unity caches are never automatically removed.",
        ["Aynı boyuttaki dosyalar SHA-256 ile doğrulanır. Hiçbir dosya silinmez."]="Same-sized files are verified with SHA-256. No files are deleted.",
        ["Aygıt listesi Windows'un şu anda algıladığı aygıtlardan oluşur. Eşleştirme ve bağlanma Windows Bluetooth ayarlarından yapılır."]="This list shows devices Windows currently detects. Pairing and connecting are managed in Windows Bluetooth settings.",
        ["Fare, klavye, kulaklık ve diğer Bluetooth aksesuarları"]="Mice, keyboards, headsets, and other Bluetooth accessories",
        ["Bu teknik envanter ayrıntılı aygıt listesidir. Günlük kullanımda Bluetooth aksesuarları sekmesini kullanabilirsiniz."]="This technical inventory lists devices in detail. For everyday use, open Bluetooth accessories.",
        ["HWiNFO benzeri geniş sensör envanteri, LibreHardwareMonitor tarafından donanımın gerçekten sunduğu ölçümlerle sağlanır. Anakart, BIOS, sürücü ve yetki sınırları nedeniyle bazı değerler eksik olabilir. Nexus fan eğrisi, voltaj veya saat hızını değiştirmez; FPS/RAM kazanımı vaat etmez."]="A broad HWiNFO-style sensor inventory is provided by LibreHardwareMonitor using readings exposed by your hardware. Some values may be unavailable because of motherboard, BIOS, driver, or permission limits. Nexus does not change fan curves, voltage, or clocks, and does not promise FPS or memory gains.",
        ["Windows ses ayarları"]="Windows sound settings",["Ekran ayarları"]="Display settings",["Güç ayarları"]="Power settings",["Görüntülenen değer desteklenmiyor"]="This reading is unsupported",
        ["Dil"]="Language",["Sisteminin gerçek zamanlı görünümü ve araçlarına hızlı erişim."]="Your real-time system overview and quick access to your tools.", ["Örnek"]="Sampling…",["GPU sensörü desteklenmiyor"]="GPU sensor unavailable",["hazır sürücü"]="ready drives",["aygıt uyarısı"]="device alerts",["Nexus hatası"]="Nexus errors",["aygıt"]="devices",["Adaptör yok"]="No adapter",["Ağ adaptörü seçilmedi"]="No network adapter selected",["Performansa git"]="View performance",["Ağ etkinliğini incele"]="View network activity",["Aygıtları gör"]="View devices",["Depolamayı incele"]="Review storage",["Gerçek Windows sayaçları"]="Live Windows counters",["Aygıtlar"]="Devices",["Hotspot durumu"]="Hotspot status",["Bağlı cihaz"]="Connected devices",["GPU yük sensörü desteklenmiyor."]="GPU load sensor unavailable.", ["Dashboard verisi kısmen okunamadı: "]="Some dashboard data could not be read: ",["Henüz taranmadı"]="Not scanned yet",["Desteklenmeyen sensör veya API değeri tahmin edilmez."]="Unsupported sensor or API readings are never estimated.", ["Bağlı"]="Connected",["Bağlı değil"]="Disconnected"
        ,["Bağdaştırıcı ve uygulama bağlantıları okunuyor…"]="Reading adapters and app connections…",["İzlenen bağdaştırıcı"]="Monitored network adapter",["Canlı hız ilk ölçüm aralığından sonra görünür."]="Live speed appears after the first counter interval.",
        ["UYGULAMA BAĞLANTISI"]="APP CONNECTIONS",["YENİ UYARI"]="NEW ALERTS",["Uygulama"]="Application",["Tür"]="Type",["Yerel"]="Local",["Uzak"]="Remote",["Yol"]="Path",["Bağlantı"]="Connections",["Adaptör"]="Adapter",["İndirme Mbit/s"]="Download Mbit/s",["Yükleme Mbit/s"]="Upload Mbit/s",["Zaman"]="Time",["4 ping gönder"]="Send 4 pings",["IP adresine dört ölçümlü gecikme testi"]="Four-sample latency test to an IP address",["Yeni ağ bağlantısı uyarılarını etkinleştir"]="Enable new network connection alerts",["Trafik geçmişi"]="Traffic history",
        ["Günlük kullanımda Bluetooth aksesuarları sekmesini kullanabilirsiniz."]="For everyday tasks, use the Bluetooth accessories tab.", ["Windows Aygıt Yöneticisi"]="Windows Device Manager",["Windows tarafından algılanan Bluetooth aygıtı"]="Bluetooth devices detected by Windows",["Üretici"]="Manufacturer",["Ne işe yarıyor?"]="What does it do?",["Listeden bir aygıt seçin."]="Select a device from the list.",
        ["KULLANILAN ALAN"]="USED SPACE",["BOŞ ALAN"]="FREE SPACE",["DİSK SAĞLIĞI"]="DISK HEALTH",["Sürücü toplamı"]="Total across drives",["Hazır yerel sürücüler"]="Ready local drives",["Kullanım"]="Usage",["Sürücü"]="Drive",["Etiket"]="Label",["Biçim"]="Format",["Kullanılan"]="Used",["Boş"]="Free",["Doluluk"]="Used %",["Üst klasör"]="Folder",["Dosya"]="File",["Boyut"]="Size",["Büyük dosya"]="Large file",["Konum"]="Location",["Dosya grubu"]="Duplicate group",["Kopya"]="Copies",["Dosya boyutu"]="File size",["Olası yinelenen veri"]="Potential duplicate data",["Yinelenen dosyalar"]="Duplicate files",["Dosya konumunu aç"]="Open file location",
        ["Uygulama, adres veya PID ara"]="Search app, address, or PID",["Seçili uygulamayı engelle"]="Block selected app",["Son engeli geri al"]="Undo last block",["FPS — oyun çalışırken ölçüm başlatın"]="FPS — start a capture while a game is running",["Oyun kare hızı"]="Game frame rate",["Araca göz at"]="Browse for tool",["PresentMon indir"]="Get PresentMon",["Oyun / uygulama"]="Game / application",["Kare süresi"]="Frame time",["GPU meşgul"]="GPU busy",["CPU meşgul"]="CPU busy",["CPU / GPU SICAKLIĞI"]="CPU / GPU TEMPERATURE",["FAN HIZI"]="FAN SPEED",["Sensör"]="Sensor",["Anlık"]="Current",["Min"]="Min",["Maks"]="Max",["Saat hızı"]="Clock speed",["Sıcaklık"]="Temperature",["Fan"]="Fan",["Yük"]="Load",["Güç"]="Power",["Bellek"]="Memory",["Bilinmiyor"]="Unknown",["Diğer"]="Other",["Pil"]="Battery"
    };
    private static readonly Dictionary<string,string> Tr = En.Where(x=>x.Key!=x.Value).ToDictionary(x=>x.Value,x=>x.Key,StringComparer.Ordinal);
    public static string Mode { get; private set; } = "tr";
    public static bool IsEnglish => Mode == "en";
    public static void SetMode(string? mode) => Mode = mode is "en" or "tr" ? mode : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
    public static string T(string text) => (IsEnglish ? En : Tr).TryGetValue(text,out var value) ? value : text;
    public static string ToTurkish(string text) => Tr.TryGetValue(text,out var value)?value:text;
    public static void Apply(DependencyObject root)
    {
        if(root is TextBlock tb && tb.Text.Length>0) tb.Text=T(tb.Text);
        else if(root is Button b && b.Content is string s) b.Content=T(s);
        else if(root is CheckBox cb && cb.Content is string cs) cb.Content=T(cs);
        else if(root is TabItem tab && tab.Header is string h) tab.Header=T(h);
        else if(root is ComboBoxItem ci && ci.Content is string c) ci.Content=T(c);
        else if(root is DataGrid dg) foreach(var col in dg.Columns) if(col.Header is string ch) col.Header=T(ch);
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) Apply(VisualTreeHelper.GetChild(root,i));
    }
    public static string BluetoothDescription(DeviceItem device)
    {
        var name=device.Name;
        if(name.Contains("mouse",StringComparison.OrdinalIgnoreCase)||name.Contains("fare",StringComparison.OrdinalIgnoreCase)) return IsEnglish?"A pointing device used to move the pointer and click.":"İşaretçiyi hareket ettirmek ve tıklamak için kullanılan fare.";
        if(name.Contains("keyboard",StringComparison.OrdinalIgnoreCase)||name.Contains("klavye",StringComparison.OrdinalIgnoreCase)) return IsEnglish?"A keyboard used to type and control the computer.":"Yazı yazmak ve bilgisayarı kontrol etmek için kullanılan klavye.";
        if(name.Contains("headset",StringComparison.OrdinalIgnoreCase)||name.Contains("headphone",StringComparison.OrdinalIgnoreCase)||name.Contains("kulaklık",StringComparison.OrdinalIgnoreCase)||name.Contains("audio",StringComparison.OrdinalIgnoreCase)) return IsEnglish?"A Bluetooth audio device for listening or speaking.":"Ses dinlemek veya konuşmak için kullanılan Bluetooth ses aygıtı.";
        if(name.Contains("controller",StringComparison.OrdinalIgnoreCase)||name.Contains("gamepad",StringComparison.OrdinalIgnoreCase)) return IsEnglish?"A game controller used to control games.":"Oyunları kontrol etmek için kullanılan oyun kumandası.";
        return IsEnglish?"A paired Bluetooth accessory. Windows provides its current device status.":"Eşleştirilmiş Bluetooth aksesuarı. Güncel aygıt durumunu Windows bildirir.";
    }
    public static string DeviceDescription(DeviceItem device)
    {
        var text=device.Name+" "+device.Category;
        if(text.Contains("Bluetooth",StringComparison.OrdinalIgnoreCase)) return IsEnglish?"Bluetooth radio or accessory used for wireless devices.":"Kablosuz aksesuarları bağlayan Bluetooth birimi veya aygıtı.";
        if(device.Category=="Ağ") return IsEnglish?"Network adapter used for Wi-Fi, Ethernet, or a virtual network connection.":"Wi-Fi, Ethernet veya sanal ağ bağlantısı sağlayan bağdaştırıcı.";
        if(device.Category=="Ses") return IsEnglish?"Audio input or output hardware, such as a microphone, speaker, or headset.":"Mikrofon, hoparlör veya kulaklık gibi ses giriş/çıkış donanımı.";
        if(device.Category=="USB") return IsEnglish?"USB device or controller connected to the computer.":"Bilgisayara USB üzerinden bağlı aygıt veya denetleyici.";
        if(device.Category=="Giriş") return IsEnglish?"Keyboard, mouse, touch, or another user input device.":"Klavye, fare, dokunmatik yüzey veya başka bir kullanıcı giriş aygıtı.";
        if(device.Category=="Ekran") return IsEnglish?"Graphics adapter or display-related hardware.":"Grafik bağdaştırıcısı veya ekranla ilgili donanım.";
        if(device.Category=="Pil") return IsEnglish?"Battery and power reporting hardware.":"Pil ve güç durumu bilgisi sağlayan donanım.";
        return IsEnglish?"Windows hardware component. Select it to review the reported status.":"Windows donanım bileşeni. Bildirilen durumu incelemek için seçin.";
    }
    public static string ProblemDescription(uint code) => code switch
    {
        0 => IsEnglish?"No problem reported by Windows.":"Windows bir sorun bildirmiyor.",
        10 => IsEnglish?"Windows could not start this device. Try restarting Windows or checking its driver.":"Windows bu aygıtı başlatamadı. Windows'u yeniden başlatmayı veya sürücüyü kontrol etmeyi deneyin.",
        22 => IsEnglish?"The device is disabled. Enable it in Device Manager if you need it.":"Aygıt devre dışı. Gerekliyse Aygıt Yöneticisi'nden etkinleştirin.",
        28 => IsEnglish?"A driver is not installed. Check Windows Update or the device maker's support page.":"Sürücü yüklü değil. Windows Update'i veya aygıt üreticisinin destek sayfasını kontrol edin.",
        43 => IsEnglish?"Windows stopped this device after reporting a problem. Reconnect it and check its driver.":"Windows bir sorun bildirildiği için aygıtı durdurdu. Yeniden bağlayıp sürücüsünü kontrol edin.",
        45 => IsEnglish?"The device is not currently connected. Reconnect it to use it.":"Aygıt şu anda bağlı değil. Kullanmak için yeniden bağlayın.",
        _ => IsEnglish?$"Windows reports problem code {code}. Open Device Manager for details.":$"Windows {code} sorun kodunu bildirdi. Ayrıntı için Aygıt Yöneticisi'ni açın."
    };
}
