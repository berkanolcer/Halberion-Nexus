namespace Halberion.DeviceControl.Core;

public sealed record DeviceItem(
    string InstanceId,
    string Name,
    string Category,
    string Manufacturer,
    string Status,
    uint ProblemCode);

public sealed record AudioEndpoint(
    string Id,
    string Name,
    string Direction,
    string State,
    bool IsDefault);

public sealed record DisplayItem(
    string DeviceName,
    string Name,
    string Status,
    int Width,
    int Height,
    int RefreshRate,
    int BitsPerPixel);

public sealed record PowerSummary(
    Guid ActiveSchemeId,
    string ActiveSchemeName,
    string PowerSource,
    string BatteryLevel,
    string BatteryState,
    int EstimatedMinutes);

public sealed record DeviceSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<DeviceItem> Devices,
    IReadOnlyList<AudioEndpoint> AudioEndpoints,
    IReadOnlyList<DisplayItem> Displays,
    PowerSummary Power,
    IReadOnlyList<string> Warnings);
