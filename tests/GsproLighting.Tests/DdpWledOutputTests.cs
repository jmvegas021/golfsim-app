using System.Net;
using System.Net.Sockets;
using GsproLighting.Core.Config;
using GsproLighting.Wled;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DdpWledOutputTests
{
    [Fact]
    public async Task SendPixelsAsync_DeliversDdpFrameToConfiguredEndpoint()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        var receive = listener.ReceiveAsync();

        await using var output = new DdpWledOutput();
        output.Configure(new WledConfig
        {
            ControllerIp = "127.0.0.1",
            UdpPort = port,
            LedCount = 2,
            Brightness = 255,
            Protocol = "ddp"
        });

        var pixels = new[]
        {
            RgbColor.FromRgb(10, 20, 30),
            RgbColor.FromRgb(40, 50, 60)
        };
        await output.SendPixelsAsync(pixels, brightness: 255);

        var result = await receive.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(
            (byte)(DdpPacketBuilder.FlagsVersion1 | DdpPacketBuilder.FlagsPush),
            result.Buffer[0]);
        Assert.Equal(DdpPacketBuilder.DataTypeRgb24, result.Buffer[2]);
        Assert.Equal(DdpPacketBuilder.HeaderLength + 6, result.Buffer.Length);
        Assert.Equal(10, result.Buffer[10]);
        Assert.Equal(20, result.Buffer[11]);
        Assert.Equal(30, result.Buffer[12]);
        Assert.Equal(40, result.Buffer[13]);
        Assert.Equal(50, result.Buffer[14]);
        Assert.Equal(60, result.Buffer[15]);
    }

    [Fact]
    public async Task SendPixelsAsync_SendsMultiplePacketsForLargeStrip()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        await using var output = new DdpWledOutput();
        output.Configure(new WledConfig
        {
            ControllerIp = "127.0.0.1",
            UdpPort = port,
            LedCount = 585,
            Brightness = 255
        });

        var pixels = new RgbColor[585];
        Array.Fill(pixels, RgbColor.FromRgb(7, 8, 9));

        var receiveFirst = listener.ReceiveAsync();
        var sendTask = output.SendPixelsAsync(pixels, brightness: 255);
        var first = await receiveFirst.WaitAsync(TimeSpan.FromSeconds(2));
        var second = await listener.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await sendTask;

        Assert.Equal(DdpPacketBuilder.FlagsVersion1, first.Buffer[0]);
        Assert.Equal(
            (byte)(DdpPacketBuilder.FlagsVersion1 | DdpPacketBuilder.FlagsPush),
            second.Buffer[0]);
        Assert.Equal(
            DdpPacketBuilder.HeaderLength + DdpPacketBuilder.MaxDataBytes,
            first.Buffer.Length);
    }

    [Fact]
    public async Task SendPixelsAsync_ThrowsWhenControllerIpNotConfigured()
    {
        await using var output = new DdpWledOutput();
        output.Configure(new WledConfig
        {
            ControllerIp = WledConfig.DefaultControllerIp,
            UdpPort = DdpPacketBuilder.DefaultUdpPort,
            LedCount = 8
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            output.SendPixelsAsync([RgbColor.FromRgb(1, 2, 3)], 255));

        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Configure_SnapshotsConnectionIpForLaterSends()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        var receive = listener.ReceiveAsync();

        var shared = new WledConfig
        {
            ControllerIp = "127.0.0.1",
            UdpPort = port,
            LedCount = 1,
            Brightness = 255
        };

        await using var output = new DdpWledOutput();
        output.Configure(shared);

        // Mutating the live config object must not retarget UDP without Configure.
        shared.ControllerIp = "10.255.255.1";
        shared.UdpPort = 1;

        await output.SendPixelsAsync([RgbColor.FromRgb(1, 2, 3)], 255);
        var result = await receive.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, result.Buffer[10]);
    }
}
