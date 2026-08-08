namespace GsproLighting.Core.Config;

public sealed class RgbColor
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public static RgbColor FromRgb(byte r, byte g, byte b) => new() { R = r, G = g, B = b };

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
