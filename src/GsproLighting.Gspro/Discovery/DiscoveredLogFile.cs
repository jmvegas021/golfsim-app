namespace GsproLighting.Gspro.Discovery;

public sealed class DiscoveredLogFile
{
    public required string FullPath { get; init; }
    public DateTimeOffset LastWriteUtc { get; init; }
    public long LengthBytes { get; init; }
}
