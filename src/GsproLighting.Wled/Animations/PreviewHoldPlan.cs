using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;

namespace GsproLighting.Wled.Animations;

/// <summary>Resolved play + hold instructions for a Preview-tab state.</summary>
public sealed class PreviewHoldPlan
{
    public required LightingPreviewItem Item { get; init; }
    public required EffectSlot Slot { get; init; }
    public required AnimationDirection Direction { get; init; }
    public required byte HoldBrightness { get; init; }
    public bool HoldAsSolid { get; init; } = true;
}

/// <summary>Builds <see cref="PreviewHoldPlan"/> from catalog items and WLED config.</summary>
public sealed class PreviewHoldPlanFactory
{
    public PreviewHoldPlan Create(
        LightingPreviewItem item,
        WledConfig config,
        AnimationDirection direction = AnimationDirection.Center)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(config);

        var factor = Math.Clamp(item.HoldBrightnessFactor, 0.05, 1);
        var holdBrightness = (byte)Math.Clamp(
            Math.Round(config.Brightness * factor),
            1,
            255);

        return new PreviewHoldPlan
        {
            Item = item,
            Slot = item.Slot.Clone(),
            Direction = item.SupportsDirection ? direction : AnimationDirection.Center,
            HoldBrightness = holdBrightness,
            HoldAsSolid = item.HoldAsSolid
        };
    }
}
