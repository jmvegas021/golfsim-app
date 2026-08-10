using GsproLighting.Core.Config;
using GsproLighting.Wled;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DdpPacketBuilderTests
{
    [Fact]
    public void BuildFrame_WritesVersion1Rgb24HeaderAndScaledPixels()
    {
        var pixels = new[]
        {
            RgbColor.FromRgb(255, 0, 0),
            RgbColor.FromRgb(0, 255, 0)
        };

        var packets = DdpPacketBuilder.BuildFrame(pixels, brightness: 128, startingSequence: 1);

        Assert.Single(packets);
        var packet = packets[0];
        Assert.Equal(DdpPacketBuilder.HeaderLength + 6, packet.Length);
        Assert.Equal(
            (byte)(DdpPacketBuilder.FlagsVersion1 | DdpPacketBuilder.FlagsPush),
            packet[0]);
        Assert.Equal(1, packet[1]);
        Assert.Equal(DdpPacketBuilder.DataTypeRgb24, packet[2]);
        Assert.Equal(DdpPacketBuilder.DestinationDisplay, packet[3]);
        Assert.Equal(0, packet[4]);
        Assert.Equal(0, packet[5]);
        Assert.Equal(0, packet[6]);
        Assert.Equal(0, packet[7]);
        Assert.Equal(0, packet[8]);
        Assert.Equal(6, packet[9]);
        Assert.Equal(128, packet[10]); // 255 * 128 / 255
        Assert.Equal(0, packet[11]);
        Assert.Equal(0, packet[12]);
        Assert.Equal(0, packet[13]);
        Assert.Equal(128, packet[14]);
        Assert.Equal(0, packet[15]);
    }

    [Fact]
    public void BuildFrame_SplitsAbove480PixelsIntoOffsetPacketsWithPushOnLast()
    {
        const int ledCount = 585;
        var pixels = new RgbColor[ledCount];
        for (var i = 0; i < ledCount; i++)
            pixels[i] = RgbColor.FromRgb((byte)(i % 256), 10, 20);

        var packets = DdpPacketBuilder.BuildFrame(pixels, brightness: 255, startingSequence: 3);

        Assert.Equal(2, packets.Count);

        var first = packets[0];
        Assert.Equal(DdpPacketBuilder.HeaderLength + DdpPacketBuilder.MaxDataBytes, first.Length);
        Assert.Equal(DdpPacketBuilder.FlagsVersion1, first[0]);
        Assert.Equal(3, first[1]);
        Assert.Equal(0u, ReadUInt32BigEndian(first, 4));
        Assert.Equal(DdpPacketBuilder.MaxDataBytes, ReadUInt16BigEndian(first, 8));
        Assert.Equal(0, first[10]);
        Assert.Equal(10, first[11]);
        Assert.Equal(20, first[12]);

        var second = packets[1];
        var remainingBytes = (ledCount - DdpPacketBuilder.MaxPixelsPerPacket) * 3;
        Assert.Equal(DdpPacketBuilder.HeaderLength + remainingBytes, second.Length);
        Assert.Equal(
            (byte)(DdpPacketBuilder.FlagsVersion1 | DdpPacketBuilder.FlagsPush),
            second[0]);
        Assert.Equal(4, second[1]);
        Assert.Equal((uint)DdpPacketBuilder.MaxDataBytes, ReadUInt32BigEndian(second, 4));
        Assert.Equal(remainingBytes, ReadUInt16BigEndian(second, 8));

        var lastPixelIndex = ledCount - 1;
        var lastOffset = DdpPacketBuilder.HeaderLength + (remainingBytes - 3);
        Assert.Equal((byte)(lastPixelIndex % 256), second[lastOffset]);
        Assert.Equal(10, second[lastOffset + 1]);
        Assert.Equal(20, second[lastOffset + 2]);
    }

    [Fact]
    public void BuildFrame_PacksExact480PixelsAsSinglePushPacket()
    {
        var pixels = new RgbColor[480];
        Array.Fill(pixels, RgbColor.FromRgb(1, 2, 3));

        var packets = DdpPacketBuilder.BuildFrame(pixels, brightness: 255);

        Assert.Single(packets);
        Assert.Equal(
            (byte)(DdpPacketBuilder.FlagsVersion1 | DdpPacketBuilder.FlagsPush),
            packets[0][0]);
        Assert.Equal(DdpPacketBuilder.MaxDataBytes, ReadUInt16BigEndian(packets[0], 8));
    }

    [Fact]
    public void Scale_LeavesFullBrightnessUnchanged()
    {
        var color = RgbColor.FromRgb(10, 20, 30);
        Assert.Equal(color, DdpPacketBuilder.Scale(color, 255));
    }

    private static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8) |
        buffer[offset + 3];

    private static int ReadUInt16BigEndian(byte[] buffer, int offset) =>
        (buffer[offset] << 8) | buffer[offset + 1];
}
