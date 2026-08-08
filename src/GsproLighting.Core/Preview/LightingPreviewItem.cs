using GsproLighting.Core.Config;

namespace GsproLighting.Core.Preview;

/// <summary>
/// One previewable bay lighting state (slot + hold policy). Not editable from the Preview tab.
/// </summary>
public sealed class LightingPreviewItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required EffectSlot Slot { get; init; }
    public bool SupportsDirection { get; init; }

    /// <summary>
    /// Brightness multiplier applied when holding after the animation (1 = full, ~0.33 = dim).
    /// </summary>
    public double HoldBrightnessFactor { get; init; } = 1;

    /// <summary>
    /// When true, hold uses a solid fill of <see cref="Slot"/>.Color after the animation.
    /// Marker-style states leave the last frame pattern instead.
    /// </summary>
    public bool HoldAsSolid { get; init; } = true;
}
