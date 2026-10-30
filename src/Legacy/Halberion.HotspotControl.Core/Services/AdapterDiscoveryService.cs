using System.Net.NetworkInformation;
using Halberion.HotspotControl.Models;
using Windows.Networking.Connectivity;

namespace Halberion.HotspotControl.Services;

public sealed class AdapterDiscoveryService
{
    private static readonly string[] PdaNetMarkers = ["pdanet", "broadband connection", "pda net"];
    private static readonly string[] KillerMarkers = ["killer", "ax1650i"];

    public async Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var notes = new List<string>();
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Select(ToAdapterInfo)
            .OrderByDescending(a => a.IsPdaNetCandidate)
            .ThenByDescending(a => a.IsKillerWiFiCandidate)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var killer = adapters.FirstOrDefault(a => a.IsKillerWiFiCandidate)
            ?? adapters.FirstOrDefault(a => a.Type.Equals(nameof(NetworkInterfaceType.Wireless80211), StringComparison.OrdinalIgnoreCase));

        if (killer is null)
            notes.Add("Killer Wi-Fi AX1650i veya başka bir kablosuz adaptör bulunamadı.");
        else if (!killer.IsKillerWiFiCandidate)
            notes.Add($"Killer AX1650i bulunamadı; kablosuz aday olarak '{killer.Name}' görüldü.");

        ConnectionProfile? selectedProfile = null;
        string sourceAdapterName = "Bulunamadı";
        var exactPdaNet = false;

        try
        {
            var profiles = NetworkInformation.GetConnectionProfiles().ToArray();
            var exact = profiles.FirstOrDefault(p => ContainsAny(p.ProfileName, PdaNetMarkers));

            if (exact is null)
            {
                exact = profiles.FirstOrDefault(p =>
                {
                    var adapterId = p.NetworkAdapter?.NetworkAdapterId.ToString("B");
                    var adapter = adapters.FirstOrDefault(a => IdsEqual(a.Id, adapterId));
                    return adapter?.IsPdaNetCandidate == true;
                });
            }

            if (exact is not null)
            {
                selectedProfile = exact;
                exactPdaNet = true;
            }
            else
            {
                selectedProfile = NetworkInformation.GetInternetConnectionProfile();
                if (selectedProfile is not null)
                    notes.Add("PdaNet adlı profil bulunamadı. Windows'un etkin internet profili yalnızca güvenli geri dönüş adayı olarak seçildi; Başlat düğmesi bunu açıkça gösterecek.");
                else
                    notes.Add("Windows etkin bir internet bağlantı profili bildirmedi.");
            }

            if (selectedProfile?.NetworkAdapter is not null)
            {
                var id = selectedProfile.NetworkAdapter.NetworkAdapterId.ToString("B");
                sourceAdapterName = adapters.FirstOrDefault(a => IdsEqual(a.Id, id))?.Name ?? id;
            }
        }
        catch (Exception ex)
        {
            notes.Add($"Windows bağlantı profilleri okunamadı: {ex.GetType().Name}: {ex.Message}");
        }

        string netsh;
        try
        {
            netsh = await ProcessRunner.RunReadOnlyAsync("netsh.exe", "wlan show drivers", cancellationToken);
        }
        catch (Exception ex)
        {
            netsh = $"netsh tanısı çalıştırılamadı: {ex.GetType().Name}: {ex.Message}";
        }

        return new DiscoverySnapshot
        {
            Adapters = adapters,
            SourceProfile = selectedProfile,
            SourceProfileName = selectedProfile?.ProfileName ?? "Bulunamadı",
            SourceAdapterName = sourceAdapterName,
            IsPdaNetExactMatch = exactPdaNet,
            KillerWiFiAdapter = killer,
            Notes = notes,
            NetshWlanDrivers = netsh
        };
    }

    private static AdapterInfo ToAdapterInfo(NetworkInterface adapter)
    {
        var combined = $"{adapter.Name} {adapter.Description}";
        var isWireless = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
        var isKillerWiFi = ContainsAny(combined, ["ax1650i"])
            || (isWireless && ContainsAny(combined, KillerMarkers));
        return new AdapterInfo(
            adapter.Id,
            adapter.Name,
            adapter.Description,
            adapter.OperationalStatus.ToString(),
            adapter.NetworkInterfaceType.ToString(),
            ContainsAny(combined, PdaNetMarkers),
            isKillerWiFi);
    }

    private static bool ContainsAny(string? value, IEnumerable<string> markers) =>
        !string.IsNullOrWhiteSpace(value) && markers.Any(m => value.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static bool IdsEqual(string? left, string? right)
    {
        if (Guid.TryParse(left, out var l) && Guid.TryParse(right, out var r))
            return l == r;
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
