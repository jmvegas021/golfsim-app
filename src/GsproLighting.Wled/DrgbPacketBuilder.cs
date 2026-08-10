using GsproLighting.Core.Config;

namespace GsproLighting.Wled;

/// <summary>
/// Builds WLED realtime UDP datagrams for DRGB (protocol byte 2).
/// </summary>
public static class DrgbPacketBuilder
{
    public const byte ProtocolDrgb = 2;
    public const byte DefaultTimeoutSeconds = 5;

    public static byte[] Build(
        IReadOnlyList<RgbColor> pixels,
        byte brightness,
        byte timeoutSeconds = DefaultTimeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var count = Math.Max(1, pixels.Count);
        var packet = new byte[2 + count * 3];
        packet[0] = ProtocolDrgb;
        packet[1] = timeoutSeconds;

        for (var i = 0; i < count; i++)
        {
            var color = i < pixels.Count ? pixels[i] : RgbColor.FromRgb(0, 0, 0);
            var scaled = Scale(color, brightness);
            var offset = 2 + i * 3;
            packet[offset] = scaled.R;
            packet[offset + 1] = scaled.G;
            packet[offset + 2] = scaled.B;
        }

        return packet;
    }

    public static RgbColor Scale(RgbColor color, byte brightness)
    {
        if (brightness >= 255)
            return color;

        return RgbColor.FromRgb(
            (byte)(color.R * brightness / 255),
            (byte)(color.G * brightness / 255),
            (byte)(color.B * brightness / 255));
    }
}
