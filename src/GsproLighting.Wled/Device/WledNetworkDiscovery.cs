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
    public int LedCount { get; init; }

    public override string ToString() =>
        LedCount > 0
            ? $"{Name} ({IpAddress}) · {LedCount} LEDs"
            : $"{Name} ({IpAddress})";
}

/// <summary>
/// Finds WLED controllers on the local LAN by probing every host on the machine's own /24 (or
/// smaller) IPv4 subnets with a short-timeout GET /json/info — no mDNS/Bonjour dependency.
/// Wider NIC masks (e.g. /16) are clamped to the host's /24 so home/bay LANs still scan.
/// </summary>
public sealed class WledNetworkDiscovery : IDisposable
{
    /// <summary>Probe ceilings — wider than this is clamped to a /24 around the host IP.</summary>
    public const int MaxSubnetHostBits = 8; // /24

    private const int MaxConcurrentProbes = 32;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(900);

    private readonly WledDeviceClient _client;
    private readonly bool _ownsClient;

    public WledNetworkDiscovery(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient(requestTimeout: ProbeTimeout);
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

        if (candidates.Count == 0)
            return [];

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
                    found.Add(new WledDiscoveredDevice
                    {
                        IpAddress = ip,
                        Name = string.IsNullOrWhiteSpace(info.Name) ? "WLED" : info.Name,
                        Version = info.Version,
                        LedCount = info.LedCount
                    });
            }
            catch
            {
                // Expected for non-WLED / slow / unreachable hosts.
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

    internal static IEnumerable<(IPAddress Address, IPAddress Mask)> GetLocalIPv4Subnets()
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
                if (IsLinkLocal(addr.Address))
                    continue;
                yield return (addr.Address, addr.IPv4Mask);
            }
        }
    }

    /// <summary>
    /// Enumerates every host address on the subnet (network/broadcast excluded).
    /// Masks wider than /24 are clamped to the /24 containing <paramref name="subnet"/>.Address
    /// so a mis-reported /16 NIC still probes ~254 hosts instead of silently scanning nothing.
    /// Public for unit testing — pure subnet math, no I/O.
    /// </summary>
    public static IEnumerable<string> BuildCandidateAddresses((IPAddress Address, IPAddress Mask) subnet)
    {
        if (IsLinkLocal(subnet.Address))
            yield break;

        var addressValue = ToUInt32(subnet.Address.GetAddressBytes());
        var maskValue = ToUInt32(subnet.Mask.GetAddressBytes());
        var maskBits = CountBits(maskValue);
        if (maskBits < 32 - MaxSubnetHostBits)
        {
            // Clamp to /24 containing this host (e.g. 10.0.5.20/16 → 10.0.5.0/24).
            maskValue = 0xFFFFFF00u;
        }

        var network = addressValue & maskValue;
        var broadcast = network | ~maskValue;
        for (var host = network + 1; host < broadcast; host++)
            yield return new IPAddress(ToBytes(host)).ToString();
    }

    private static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    private static uint ToUInt32(byte[] bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

    private static byte[] ToBytes(uint value) =>
    [
        (byte)(value >> 24),
        (byte)(value >> 16),
        (byte)(value >> 8),
        (byte)value
    ];

    private static int CountBits(uint value)
    {
        var count = 0;
        while (value != 0)
        {
            count += (int)(value & 1);
            value >>= 1;
        }

        return count;
    }
}
