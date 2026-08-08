using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

public sealed record LedAnimationFrame(IReadOnlyList<RgbColor> Pixels, TimeSpan Duration);
