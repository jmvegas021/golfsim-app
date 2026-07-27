namespace GsproLighting.Core.Models;

public sealed class RawTrafficMessage
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Direction { get; init; }
    public required string RawJson { get; init; }
    public ShotPayload? Shot { get; init; }
    public GsproResponse? Response { get; init; }
    public IReadOnlyDictionary<string, object?>? UnknownFields { get; init; }
}
