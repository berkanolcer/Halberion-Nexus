using Windows.Networking.Connectivity;

namespace Halberion.HotspotControl.Models;

public sealed class DiscoverySnapshot
{
    public required IReadOnlyList<AdapterInfo> Adapters { get; init; }
    public ConnectionProfile? SourceProfile { get; init; }
    public string SourceProfileName { get; init; } = "Bulunamadı";
    public string SourceAdapterName { get; init; } = "Bulunamadı";
    public bool IsPdaNetExactMatch { get; init; }
    public AdapterInfo? KillerWiFiAdapter { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string NetshWlanDrivers { get; init; } = string.Empty;
}
