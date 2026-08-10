using GsproLighting.Core.Config;

namespace GsproLighting.Wled;

/// <summary>
/// Builds WLED-compatible DDP UDP datagrams (10-byte header + RGB payload).
/// Splits frames larger than <see cref="MaxDataBytes"/> into multiple packets;
/// only the final packet sets the PUSH flag so WLED commits the frame.
/// </summary>
public static class DdpPacketBuilder
{
    public const int HeaderLength = 10;
    public const int MaxDataBytes = 1440;
    public const int MaxPixelsPerPacket = MaxDataBytes / 3;
    public const int DefaultUdpPort = 4048;

    public const byte FlagsVersion1 = 0x40;
    public const byte FlagsPush = 0x01;
    public const byte DataTypeRgb24 = 0x0B;
    public const byte DestinationDisplay = 1;

    /// <summary>
    /// Builds one or more DDP packets for a full RGB strip frame.
    /// Sequence numbers cycle 1–15 (spec); zero is unused.
    /// </summary>
    public static IReadOnlyList<byte[]> BuildFrame(
        IReadOnlyList<RgbColor> pixels,
        byte brightness,
        byte startingSequence = 1)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        var count = Math.Max(1, pixels.Count);
        var totalBytes = count * 3;
        var packetCount = (totalBytes + MaxDataBytes - 1) / MaxDataBytes;
        var packets = new byte[packetCount][];
        var sequence = NormalizeSequence(startingSequence);

        for (var packetIndex = 0; packetIndex < packetCount; packetIndex++)
        {
            var byteOffset = packetIndex * MaxDataBytes;
            var dataLength = Math.Min(MaxDataBytes, totalBytes - byteOffset);
            var isLast = packetIndex == packetCount - 1;
            var flags = (byte)(FlagsVersion1 | (isLast ? FlagsPush : 0));
            var packet = new byte[HeaderLength + dataLength];

            packet[0] = flags;
            packet[1] = sequence;
            packet[2] = DataTypeRgb24;
            packet[3] = DestinationDisplay;
            WriteUInt32BigEndian(packet, 4, (uint)byteOffset);
            WriteUInt16BigEndian(packet, 8, (ushort)dataLength);

            var pixelStart = byteOffset / 3;
            var pixelCount = dataLength / 3;
            for (var i = 0; i < pixelCount; i++)
            {
                var pixelIndex = pixelStart + i;
                var color = pixelIndex < pixels.Count
                    ? pixels[pixelIndex]
                    : RgbColor.FromRgb(0, 0, 0);
                var scaled = Scale(color, brightness);
                var offset = HeaderLength + i * 3;
                packet[offset] = scaled.R;
                packet[offset + 1] = scaled.G;
                packet[offset + 2] = scaled.B;
            }

            packets[packetIndex] = packet;
            sequence = NextSequence(sequence);
        }

        return packets;
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

    private static byte NormalizeSequence(byte sequence) =>
        sequence == 0 ? (byte)1 : (byte)((sequence - 1) % 15 + 1);

    private static byte NextSequence(byte sequence) =>
        sequence >= 15 ? (byte)1 : (byte)(sequence + 1);

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }
}
