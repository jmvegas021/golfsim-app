using System.Diagnostics;

namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Finds GSPro/Connect processes and their non-921 LAN peer endpoints (R50 candidates).
/// </summary>
public sealed class ConnectPeerDiscoverer
{
    private static readonly string[] ProcessHints =
    {
        "gspro", "connect", "gsp", "garmin", "r50"
    };

    private readonly WindowsTcpConnectionReader _tcp = new();

    public (IReadOnlyList<string> ProcessNames, IReadOnlyList<DiscoveredPeerEndpoint> Peers) Discover()
    {
        var processes = Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    return ProcessHints.Any(h =>
                        p.ProcessName.Contains(h, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            })
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList();

        var names = processes
            .Select(p =>
            {
                try { return p.ProcessName; }
                catch { return "unknown"; }
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        if (!OperatingSystem.IsWindows() || processes.Count == 0)
            return (names, Array.Empty<DiscoveredPeerEndpoint>());

        var pidSet = processes.Select(p => p.Id).ToHashSet();
        var nameByPid = processes.ToDictionary(
            p => p.Id,
            p =>
            {
                try { return p.ProcessName; }
                catch { return "unknown"; }
            });

        var peers = new List<DiscoveredPeerEndpoint>();
        try
        {
            foreach (var row in _tcp.ReadIpv4Rows())
            {
                if (!pidSet.Contains(row.ProcessId))
                    continue;
                if (!IsCandidatePeer(row))
                    continue;

                peers.Add(new DiscoveredPeerEndpoint
                {
                    ProcessName = nameByPid.GetValueOrDefault(row.ProcessId, "unknown"),
                    ProcessId = row.ProcessId,
                    RemoteAddress = row.RemoteAddress,
                    RemotePort = row.RemotePort,
                    LocalAddress = row.LocalAddress,
                    LocalPort = row.LocalPort,
                    State = row.State
                });
            }
        }
        catch
        {
            // TCP table may be unavailable without rights — still return process names.
        }

        return (
            names,
            peers
                .GroupBy(p => $"{p.RemoteAddress}:{p.RemotePort}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.RemoteAddress)
                .ThenBy(p => p.RemotePort)
                .ToList());
    }

    private static bool IsCandidatePeer(WindowsTcpConnectionReader.TcpRow row)
    {
        if (row.State is not ("Established" or "SynSent" or "SynReceived"))
            return false;
        if (row.RemotePort is 0 or 921 or 1921)
            return false;
        if ((row.LocalPort is 921 or 1921) && IsLoopback(row.RemoteAddress))
            return false;
        if (IsLoopback(row.RemoteAddress))
            return false;
        return IsPrivateOrLinkLocal(row.RemoteAddress);
    }

    private static bool IsLoopback(string ip) =>
        ip.StartsWith("127.", StringComparison.Ordinal) ||
        ip.Equals("::1", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivateOrLinkLocal(string ip)
    {
        if (!System.Net.IPAddress.TryParse(ip, out var address))
            return false;
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
            return false;
        if (bytes[0] == 10)
            return true;
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
        if (bytes[0] == 169 && bytes[1] == 254)
            return true;
        return false;
    }
}
