namespace GsproLighting.Gspro.Discovery;

public sealed class ConnectDiscoverySnapshot
{
    public DateTimeOffset TakenAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<DiscoveredLogFile> LogFiles { get; init; } = Array.Empty<DiscoveredLogFile>();
    public IReadOnlyList<DiscoveredPeerEndpoint> Peers { get; init; } = Array.Empty<DiscoveredPeerEndpoint>();
    public IReadOnlyList<string> ConnectProcessNames { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }
    public bool NetworkPayloadCaptureLimited { get; init; }

    public string StatusSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Error))
                return Error;

            var logPart = LogFiles.Count switch
            {
                0 => "0 log files",
                1 => "1 log file",
                _ => $"{LogFiles.Count} log files"
            };

            var peer = Peers.FirstOrDefault();
            var peerPart = peer is null
                ? "no R50 peer yet"
                : $"R50 peer {peer.RemoteAddress}:{peer.RemotePort}";

            var processPart = ConnectProcessNames.Count > 0
                ? string.Join(", ", ConnectProcessNames.Take(3))
                : "Connect not running";

            var limited = NetworkPayloadCaptureLimited
                ? " · network payload capture limited"
                : string.Empty;

            return $"Watching: {logPart} · {peerPart} · {processPart}{limited}";
        }
    }
}
