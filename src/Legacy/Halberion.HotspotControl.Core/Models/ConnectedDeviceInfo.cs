namespace Halberion.HotspotControl.Models;

public sealed record ConnectedDeviceInfo(
    string Name,
    string IpAddress,
    string MacAddress,
    string Status,
    DateTimeOffset LastSeen);
