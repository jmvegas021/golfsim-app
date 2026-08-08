using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

public sealed class LedAnimationRequest
{
    public required string Animation { get; init; }
    public required RgbColor Color { get; init; }
    public int LedCount { get; init; }
    public bool InvertLeftRight { get; init; }
    public AnimationDirection Direction { get; init; } = AnimationDirection.Center;
    public byte? Brightness { get; init; }
}
