using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>Builds a static end-frame for marker-style preview holds.</summary>
public static class PreviewHoldFrameBuilder
{
    public static IReadOnlyList<RgbColor> BuildMarkerFrame(PreviewHoldPlan plan, WledConfig config)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(config);

        var ledCount = Math.Max(1, config.LedCount);
        var pixels = new RgbColor[ledCount];
        var center = ResolveCenter(plan.Direction, ledCount, config.InvertLeftRight);
        var color = Scale(plan.Slot.Color, plan.HoldBrightness);

        for (var i = 0; i < ledCount; i++)
        {
            var intensity = Math.Abs(i - center) switch
            {
                0 => 1.0,
                1 => 0.65,
                2 => 0.28,
                _ => 0.08
            };
            pixels[i] = Scale(color, (byte)Math.Clamp((int)(intensity * 255), 0, 255));
        }

        return pixels;
    }

    private static int ResolveCenter(AnimationDirection direction, int ledCount, bool invert)
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
            AnimationDirection.Left => Math.Max(0, ledCount / 5),
            AnimationDirection.Right => Math.Min(ledCount - 1, ledCount - 1 - ledCount / 5),
            _ => ledCount / 2
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
