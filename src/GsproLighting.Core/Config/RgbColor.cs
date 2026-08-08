namespace GsproLighting.Core.Config;

public sealed class RgbColor
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public static RgbColor FromRgb(byte r, byte g, byte b) => new() { R = r, G = g, B = b };

    /// <summary>
    /// Scales channels so the brightest component is 255 (WLED “colors to max”).
    /// </summary>
    public RgbColor WithMaxIntensity()
    {
        var max = Math.Max(R, Math.Max(G, B));
        if (max == 0 || max == 255)
            return FromRgb(R, G, B);

        return FromRgb(
            (byte)Math.Clamp((int)Math.Round(R * 255.0 / max), 0, 255),
            (byte)Math.Clamp((int)Math.Round(G * 255.0 / max), 0, 255),
            (byte)Math.Clamp((int)Math.Round(B * 255.0 / max), 0, 255));
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
