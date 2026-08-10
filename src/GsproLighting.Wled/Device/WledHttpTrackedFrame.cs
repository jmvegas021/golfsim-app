using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Animation frame plus the solid color/brightness it represents for visual-state tracking.</summary>
public sealed record WledHttpTrackedFrame(
    WledHttpAnimationFrame Frame,
    RgbColor Color,
    byte Brightness);
