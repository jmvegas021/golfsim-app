namespace GsproLighting.Gspro.Discovery;

public sealed class DiscoveredPeerEndpoint
{
    public required string ProcessName { get; init; }
    public int ProcessId { get; init; }
    public required string RemoteAddress { get; init; }
    public int RemotePort { get; init; }
    public required string LocalAddress { get; init; }
    public int LocalPort { get; init; }
    public required string State { get; init; }

    public string Display => $"{RemoteAddress}:{RemotePort} ({ProcessName})";
}
