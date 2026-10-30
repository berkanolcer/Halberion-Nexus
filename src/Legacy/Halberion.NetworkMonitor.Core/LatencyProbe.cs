using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Halberion.NetworkMonitor.Core;

public sealed record LatencyResult(int Sent, int Received, double? AverageMs, IReadOnlyList<string> Outcomes)
{
    public double LossPercent => Sent == 0 ? 0 : (Sent - Received) * 100d / Sent;
}
public static class LatencyProbe
{
    public static bool TryParseTarget(string target, out IPAddress? address)
    {
        if (!IPAddress.TryParse(target.Trim(), out address)) return false;
        return !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any) &&
            !address.Equals(IPAddress.Broadcast) && !address.IsIPv6Multicast &&
            !(address.AddressFamily == AddressFamily.InterNetwork && address.GetAddressBytes()[0] is >= 224 and <= 239);
    }
    // Only called by an explicit test action; never from the background sampler.
    public static async Task<LatencyResult> RunAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var outcomes = new List<string>();
        var times = new List<long>();
        for (var i = 0; i < 4; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(address, TimeSpan.FromSeconds(2), new byte[32], cancellationToken: cancellationToken);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
                outcomes.Add(reply.Status == IPStatus.Success ? $"{i + 1}. yanıt: {reply.RoundtripTime} ms" : $"{i + 1}. yanıt: {reply.Status}");
            }
            catch (PingException) { outcomes.Add($"{i + 1}. yanıt: ICMP gönderilemedi"); }
            if (i < 3) await Task.Delay(250, cancellationToken);
        }
        return new(4, times.Count, times.Count == 0 ? null : times.Average(), outcomes);
    }
}
