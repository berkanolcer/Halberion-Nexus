using Halberion.NetworkMonitor.Core;

namespace Halberion.Nexus.Infrastructure;

public static class AdapterSelection
{
    public static AdapterSample? Choose(NetworkSnapshot snapshot, string? selectedId = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedId) && snapshot.Adapters.FirstOrDefault(x => x.Id == selectedId) is { } saved) return saved;
        return snapshot.Adapters.OrderByDescending(x => x.IsUp)
            .ThenByDescending(x => HasGateway(x.Gateways))
            .ThenBy(x => IsVirtual(x) ? 1 : 0)
            .ThenBy(x => IsPdaNet(x) ? 1 : 0)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }
    public static bool HasGateway(string gateways) => !string.IsNullOrWhiteSpace(gateways) && gateways.Split('•').Any(x => !string.IsNullOrWhiteSpace(x) && !x.Trim().StartsWith("0.0.0.0", StringComparison.Ordinal));
    private static bool IsPdaNet(AdapterSample x) => (x.Name + " " + x.Description).Contains("PdaNet", StringComparison.OrdinalIgnoreCase);
    private static bool IsVirtual(AdapterSample x) => new[] { "VMware", "VirtualBox", "Hyper-V", "TAP-Windows", "Loopback", "Npcap" }.Any(k => (x.Name + " " + x.Description).Contains(k, StringComparison.OrdinalIgnoreCase));
}
