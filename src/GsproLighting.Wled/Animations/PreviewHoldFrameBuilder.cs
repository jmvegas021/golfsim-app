using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Builds a static end-frame for preview holds using the same concentrate-band
/// geometry as live Ready / L·C·R DDP holds.
/// </summary>
public static class PreviewHoldFrameBuilder
{
    public static IReadOnlyList<RgbColor> BuildMarkerFrame(PreviewHoldPlan plan, WledConfig config)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(config);

        var ledCount = Math.Max(1, config.LedCount);
        var direction = ToShotDirection(plan.Direction, config.InvertLeftRight);
        var band = DrgbConcentrateBandGeometry.Resolve(direction, ledCount);
        var color = Scale(plan.Slot.Color, plan.HoldBrightness);
        return DrgbReadyFrameFactory.CreateBand(ledCount, band.Start, band.LitCount, color);
    }

    private static ShotDirection ToShotDirection(AnimationDirection direction, bool invert)
    {
        var effective = invert
            ? direction switch
            {
                AnimationDirection.Left => AnimationDirection.Right,
                AnimationDirection.Right => AnimationDirection.Left,
                _ => direction
            }
            : direction;

        return effective switch
        {
            AnimationDirection.Left => ShotDirection.Left,
            AnimationDirection.Right => ShotDirection.Right,
            _ => ShotDirection.Center
        };
    }

    private static RgbColor Scale(RgbColor color, byte brightness)
    {
        if (brightness >= 255)
            return color;
        return RgbColor.FromRgb(
            (byte)(color.R * brightness / 255),
            (byte)(color.G * brightness / 255),
            (byte)(color.B * brightness / 255));
    }
}
