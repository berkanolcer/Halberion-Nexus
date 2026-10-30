namespace Halberion.DeviceControl.Core;

public sealed class DeviceSnapshotProvider
{
    public DeviceSnapshot Capture()
    {
        var warnings = new List<string>();
        IReadOnlyList<DeviceItem> devices = [];
        IReadOnlyList<AudioEndpoint> audio = [];
        IReadOnlyList<DisplayItem> displays = [];
        PowerSummary power;
        try { devices = new PnpDeviceReader().ReadPresentDevices(); }
        catch (Exception ex) { warnings.Add($"Aygıt listesi okunamadı: {ex.Message}"); }
        try { audio = new AudioEndpointReader().Read(); }
        catch (Exception ex) { warnings.Add($"Ses uçları okunamadı: {ex.Message}"); }
        try { displays = new DisplayReader().Read(); }
        catch (Exception ex) { warnings.Add($"Ekran bilgisi okunamadı: {ex.Message}"); }
        try { power = new PowerReader().Read(); }
        catch (Exception ex)
        {
            warnings.Add($"Güç bilgisi okunamadı: {ex.Message}");
            power = new(Guid.Empty, "Bilinmiyor", "Bilinmiyor", "Bilinmiyor", "Bilinmiyor", -1);
        }
        return new(DateTimeOffset.Now, devices, audio, displays, power, warnings);
    }
}
