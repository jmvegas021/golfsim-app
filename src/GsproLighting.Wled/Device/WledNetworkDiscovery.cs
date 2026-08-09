using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GsproLighting.Wled.Device;

/// <summary>A WLED device found on the local network via subnet probing.</summary>
public sealed class WledDiscoveredDevice
{
    public required string IpAddress { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }

    public override string ToString() => $"{Name} ({IpAddress})";
}

/// <summary>
/// Finds WLED controllers on the local LAN by probing every host on the machine's own /24 (or
/// smaller) IPv4 subnets with a short-timeout GET /json/info — no mDNS/Bonjour dependency, works
/// reliably on the typical flat home/bay network this app targets.
/// </summary>
public sealed class WledNetworkDiscovery : IDisposable
{
    private const int MinSubnetMaskBits = 24;
    private const int MaxConcurrentProbes = 48;

    private readonly WledDeviceClient _client;
    private readonly bool _ownsClient;

    public WledNetworkDiscovery(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient(requestTimeout: TimeSpan.FromMilliseconds(400));
        _ownsClient = client is null;
    }

    public async Task<IReadOnlyList<WledDiscoveredDevice>> ScanAsync(
        IProgress<(int Scanned, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = GetLocalIPv4Subnets()
            .SelectMany(BuildCandidateAddresses)
            .Distinct()
            .ToList();

        var found = new ConcurrentBag<WledDiscoveredDevice>();
        using var throttle = new SemaphoreSlim(MaxConcurrentProbes);
        var scanned = 0;

        var probes = candidates.Select(async ip =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var info = await _client.GetInfoAsync(ip, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.Version))
                    found.Add(new WledDiscoveredDevice { IpAddress = ip, Name = info.Name, Version = info.Version });
            }
            catch
            {
                // Expected for the overwhelming majority of the subnet — not a WLED device,
                // unreachable, or didn't respond in time. Nothing to report.
            }
            finally
            {
                throttle.Release();
                var done = Interlocked.Increment(ref scanned);
                progress?.Report((done, candidates.Count));
            }
        });

        await Task.WhenAll(probes).ConfigureAwait(false);
        return found.OrderBy(d => d.IpAddress, StringComparer.Ordinal).ToList();
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private static IEnumerable<(IPAddress Address, IPAddress Mask)> GetLocalIPv4Subnets()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(addr.Address) || addr.IPv4Mask is null)
                    continue;
                yield return (addr.Address, addr.IPv4Mask);
            }
        }
    }

    /// <summary>
    /// Enumerates every host address on the subnet, excluding the network/broadcast addresses.
    /// Skips anything larger than /24 (255+ hosts) to keep a scan bounded to a few seconds.
    /// Public for unit testing — pure subnet math, no I/O.
    /// </summary>
    public static IEnumerable<string> BuildCandidateAddresses((IPAddress Address, IPAddress Mask) subnet)
    {
        var addressBytes = subnet.Address.GetAddressBytes();
        var maskBytes = subnet.Mask.GetAddressBytes();
        var maskBits = maskBytes.Sum(CountSetBits);
        if (maskBits < MinSubnetMaskBits)
            yield break;

        var networkBytes = new byte[4];
        for (var i = 0; i < 4; i++)
            networkBytes[i] = (byte)(addressBytes[i] & maskBytes[i]);

        var hostBits = 32 - maskBits;
        var hostCount = (1 << hostBits) - 2;
        for (var host = 1; host <= hostCount; host++)
        {
            var candidate = (byte[])networkBytes.Clone();
            candidate[3] = (byte)(candidate[3] | host);
            yield return new IPAddress(candidate).ToString();
        }
    }

    private static int CountSetBits(byte value)
    {
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }
}
