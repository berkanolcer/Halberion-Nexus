namespace Halberion.HotspotControl.Models;

public sealed record AdapterInfo(
    string Id,
    string Name,
    string Description,
    string Status,
    string Type,
    bool IsPdaNetCandidate,
    bool IsKillerWiFiCandidate);
