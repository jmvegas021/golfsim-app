using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal static class AnimationPixels
{
    public static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);

    public static RgbColor[] Empty(int ledCount)
    {
        var pixels = new RgbColor[ledCount];
        Array.Fill(pixels, Black);
        return pixels;
    }

    public static RgbColor[] Solid(int ledCount, RgbColor color)
    {
        var pixels = new RgbColor[ledCount];
        Array.Fill(pixels, color);
        return pixels;
    }

    public static RgbColor Scale(RgbColor color, double factor) =>
        RgbColor.FromRgb(
            (byte)Math.Clamp(Math.Round(color.R * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0, 255));
}
