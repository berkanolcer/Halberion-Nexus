using Halberion.HotspotControl.Models;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace Halberion.HotspotControl.Services;

public sealed class HotspotService
{
    private readonly Dictionary<string, DateTimeOffset> _lastSeenByMac = new(StringComparer.OrdinalIgnoreCase);
    private NetworkOperatorTetheringManager? _manager;

    public TetheringOperationalState? OperationalState => _manager?.TetheringOperationalState;
    public uint ClientCount => _manager?.ClientCount ?? 0;
    public bool IsActive => _manager?.TetheringOperationalState == TetheringOperationalState.On;

    public HotspotResult Prepare(ConnectionProfile? sourceProfile)
    {
        _manager = null;
        if (sourceProfile is null)
            return new(false, "İnternet kaynağı yok", "NetworkOperatorTetheringManager oluşturmak için bir Windows ConnectionProfile gerekiyor.");

        try
        {
            var capability = NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(sourceProfile);
            _manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(sourceProfile);
            return new(
                capability == TetheringCapability.Enabled,
                capability == TetheringCapability.Enabled ? "Hotspot API hazır" : $"Hotspot yeteneği: {capability}",
                $"Kaynak profil: {sourceProfile.ProfileName}; yetenek: {capability}; durum: {_manager.TetheringOperationalState}; istemci: {_manager.ClientCount}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new(false, "Windows erişimi reddetti", ExplainException(ex, "Masaüstü uygulaması için gerekli hotspot yetkisi bu sistemde verilmedi."));
        }
        catch (Exception ex)
        {
            return new(false, "Hotspot yöneticisi oluşturulamadı", ExplainException(ex));
        }
    }

    public (HotspotConfigurationSnapshot? Snapshot, HotspotResult Result) ReadCurrentConfiguration()
    {
        if (_manager is null)
            return (null, new(false, "Hotspot API hazır değil", "Bağlantıları yenileyin."));

        try
        {
            var config = _manager.GetCurrentAccessPointConfiguration();
            var snapshot = new HotspotConfigurationSnapshot(
                config.Ssid,
                config.Passphrase,
                config.Band,
                _manager.TetheringOperationalState,
                config.IsBandSupported(TetheringWiFiBand.TwoPointFourGigahertz),
                config.IsBandSupported(TetheringWiFiBand.FiveGigahertz));
            return (snapshot, new(
                true,
                "Geçerli hotspot yapılandırması okundu",
                $"SSID: {config.Ssid}; Bant: {config.Band}; Güvenlik: Windows yönetimli WPA; Durum: {_manager.TetheringOperationalState}; İstemci: {_manager.ClientCount}. Parola günlüğe yazılmadı."));
        }
        catch (Exception ex)
        {
            return (null, new(false, "Yapılandırma okunamadı", ExplainException(ex)));
        }
    }

    public async Task<HotspotResult> ApplyConfigurationAsync(string ssid, string passphrase, TetheringWiFiBand band, bool restartIfRunning)
    {
        if (_manager is null)
            return new(false, "Hotspot API hazır değil", "Önce bağlantıları yenileyin ve geçerli bir kaynak profil seçildiğini doğrulayın.");

        var validation = ValidateConfiguration(ssid, passphrase);
        if (validation is not null) return validation;

        var wasRunning = IsActive;
        if (wasRunning && !restartIfRunning)
            return new(false, "Yeniden başlatma gerekli", "SSID, parola veya bant değişikliğini uygulamak için çalışan hotspot durdurulup yeniden başlatılmalıdır.");

        NetworkOperatorTetheringAccessPointConfiguration? oldConfiguration = null;
        try
        {
            var current = _manager.GetCurrentAccessPointConfiguration();
            oldConfiguration = Clone(current);
            if (!current.IsBandSupported(band))
                return new(false, "Seçilen frekans desteklenmiyor", $"Windows Wi-Fi sürücüsü {band} bandını bu hotspot yapılandırmasında desteklemediğini bildirdi.");

            if (wasRunning)
            {
                var stop = await _manager.StopTetheringAsync();
                if (stop.Status != TetheringOperationStatus.Success)
                    return new(false, $"Yeniden başlatma için hotspot durdurulamadı: {stop.Status}", BuildOperationDetail(stop));
            }

            current.Ssid = ssid.Trim();
            current.Passphrase = passphrase;
            current.Band = band;
            await _manager.ConfigureAccessPointAsync(current);

            if (wasRunning)
            {
                var start = await _manager.StartTetheringAsync();
                if (start.Status != TetheringOperationStatus.Success)
                {
                    var rollback = await TryRollbackAsync(oldConfiguration, restart: true);
                    return new(false, $"Yeni ayarlarla hotspot başlatılamadı: {start.Status}", $"{BuildOperationDetail(start, current)} Geri alma: {rollback}");
                }
            }

            return new(true,
                wasRunning ? "Ayarlar uygulandı ve hotspot yeniden başlatıldı" : "Hotspot ayarları kaydedildi",
                $"SSID='{current.Ssid}', Bant={current.Band}, Güvenlik=Windows yönetimli WPA. Parola günlüğe yazılmadı.{(wasRunning ? " Mevcut istemci bağlantıları kesilmiş olabilir." : string.Empty)}");
        }
        catch (Exception ex)
        {
            var rollback = oldConfiguration is null ? "Önceki yapılandırma alınamadı." : await TryRollbackAsync(oldConfiguration, wasRunning);
            return new(false, "Ayarlar uygulanamadı", $"{ExplainException(ex)} Geri alma: {rollback}");
        }
    }

    public async Task<HotspotResult> StartAsync(string ssid, string passphrase, TetheringWiFiBand band)
    {
        if (_manager is null)
            return new(false, "Hotspot API hazır değil", "Önce bağlantıları yenileyin ve geçerli bir kaynak profil seçildiğini doğrulayın.");

        var apply = await ApplyConfigurationAsync(ssid, passphrase, band, restartIfRunning: true);
        if (!apply.Success) return apply;
        if (IsActive) return new(true, "Hotspot zaten çalışıyor", apply.Detail);

        try
        {
            var result = await _manager.StartTetheringAsync();
            var success = result.Status == TetheringOperationStatus.Success;
            return new(success, success ? "Hotspot çalışıyor" : $"Hotspot başlatılamadı: {result.Status}",
                BuildOperationDetail(result, _manager.GetCurrentAccessPointConfiguration()));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new(false, "Windows erişimi reddetti", ExplainException(ex, "Bu, yönetici izninden farklı bir uygulama yetkisi/Windows ilke kısıtı olabilir."));
        }
        catch (Exception ex)
        {
            return new(false, "Başlatma çağrısı başarısız", ExplainException(ex));
        }
    }

    public async Task<HotspotResult> StopAsync()
    {
        if (_manager is null)
            return new(false, "Hotspot API hazır değil", "Durdurulacak bir hotspot yöneticisi yok.");
        try
        {
            var result = await _manager.StopTetheringAsync();
            var success = result.Status == TetheringOperationStatus.Success;
            return new(success, success ? "Hotspot durduruldu" : $"Hotspot durdurulamadı: {result.Status}", BuildOperationDetail(result));
        }
        catch (Exception ex)
        {
            return new(false, "Durdurma çağrısı başarısız", ExplainException(ex));
        }
    }

    public IReadOnlyList<ConnectedDeviceInfo> GetConnectedDevices()
    {
        if (_manager is null || !IsActive) return [];
        var now = DateTimeOffset.Now;
        return _manager.GetTetheringClients().Select(client =>
        {
            var key = string.IsNullOrWhiteSpace(client.MacAddress)
                ? string.Join("|", client.HostNames.Select(h => h.CanonicalName))
                : client.MacAddress;
            _lastSeenByMac[key] = now;
            var ip = client.HostNames.FirstOrDefault(h => h.Type is HostNameType.Ipv4 or HostNameType.Ipv6)?.CanonicalName ?? "Bilinmiyor";
            var name = client.HostNames.FirstOrDefault(h => h.Type == HostNameType.DomainName)?.DisplayName;
            return new ConnectedDeviceInfo(
                string.IsNullOrWhiteSpace(name) ? "Bilinmiyor" : name,
                string.IsNullOrWhiteSpace(ip) ? "Bilinmiyor" : ip,
                string.IsNullOrWhiteSpace(client.MacAddress) ? "Bilinmiyor" : client.MacAddress,
                "Bağlı", _lastSeenByMac[key]);
        }).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private async Task<string> TryRollbackAsync(NetworkOperatorTetheringAccessPointConfiguration oldConfig, bool restart)
    {
        if (_manager is null) return "Yönetici yok; geri alınamadı.";
        try
        {
            await _manager.ConfigureAccessPointAsync(oldConfig);
            if (restart)
            {
                var result = await _manager.StartTetheringAsync();
                return result.Status == TetheringOperationStatus.Success
                    ? "Önceki ayarlar geri yüklendi ve hotspot yeniden açıldı."
                    : $"Önceki ayarlar geri yüklendi fakat hotspot açılamadı: {result.Status}.";
            }
            return "Önceki ayarlar geri yüklendi.";
        }
        catch (Exception ex) { return $"Geri alma başarısız: {ExplainException(ex)}"; }
    }

    private static NetworkOperatorTetheringAccessPointConfiguration Clone(NetworkOperatorTetheringAccessPointConfiguration source) => new()
    {
        Ssid = source.Ssid,
        Passphrase = source.Passphrase,
        Band = source.Band
    };

    private static HotspotResult? ValidateConfiguration(string ssid, string passphrase)
    {
        var trimmedSsid = ssid.Trim();
        if (trimmedSsid.Length is < 1 or > 32) return new(false, "SSID geçersiz", "Wi-Fi adı 1–32 karakter olmalıdır.");
        if (trimmedSsid.Any(char.IsControl)) return new(false, "SSID geçersiz", "Wi-Fi adı denetim karakterleri içeremez.");
        if (passphrase.Length is < 8 or > 63) return new(false, "Parola geçersiz", "WPA2-Personal parolası 8–63 karakter olmalıdır.");
        if (passphrase.Any(char.IsControl)) return new(false, "Parola geçersiz", "Parola satır sonu veya başka denetim karakterleri içeremez.");
        return null;
    }

    private static string BuildOperationDetail(NetworkOperatorTetheringOperationResult result, NetworkOperatorTetheringAccessPointConfiguration? config = null)
    {
        var configText = config is null ? string.Empty : $" SSID='{config.Ssid}', Bant={config.Band}.";
        var error = string.IsNullOrWhiteSpace(result.AdditionalErrorMessage) ? "Ek Windows mesajı yok." : result.AdditionalErrorMessage;
        return $"Windows durumu: {result.Status}.{configText} {error} Parola günlüğe yazılmadı.";
    }

    private static string ExplainException(Exception ex, string? hint = null)
    {
        var prefix = string.IsNullOrWhiteSpace(hint) ? string.Empty : $"{hint} ";
        return $"{prefix}{ex.GetType().FullName}; HRESULT=0x{ex.HResult:X8}; Mesaj={ex.Message}";
    }
}
