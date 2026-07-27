namespace GsproLighting.Core.Models;

public sealed class ShotFeedEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Kind { get; init; }
    public string Summary { get; init; } = string.Empty;
    public int? ShotNumber { get; init; }
    public double? BallSpeed { get; init; }
    public double? Hla { get; init; }
    public double? SpinAxis { get; init; }
    public double? Carry { get; init; }
    public double? Smash { get; init; }
}
