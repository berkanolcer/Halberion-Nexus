using Windows.Networking.NetworkOperators;

namespace Halberion.HotspotControl.Models;

public sealed record HotspotConfigurationSnapshot(
    string Ssid,
    string Passphrase,
    TetheringWiFiBand Band,
    TetheringOperationalState State,
    bool Supports24Ghz,
    bool Supports5Ghz);
