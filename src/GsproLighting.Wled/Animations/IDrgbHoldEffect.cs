using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Renders evolving hold frames for a DDP status session (e.g. band shimmer).
/// Outside the effect's lit region, pixels stay black.
/// </summary>
public interface IDrgbHoldEffect
{
    /// <summary>Builds one strip frame for the given wall-clock elapsed time.</summary>
    RgbColor[] RenderFrame(int ledCount, TimeSpan elapsed);
}
