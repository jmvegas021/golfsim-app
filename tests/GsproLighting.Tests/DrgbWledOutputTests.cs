using System.Net;
using System.Net.Sockets;
using GsproLighting.Core.Config;
using GsproLighting.Wled;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbWledOutputTests
{
    [Fact]
    public void DrgbPacketBuilder_UsesProtocol2AndScalesPixels()
    {
        var pixels = new[]
        {
            RgbColor.FromRgb(255, 0, 0),
            RgbColor.FromRgb(0, 255, 0)
        };

        var packet = DrgbPacketBuilder.Build(pixels, brightness: 128);

        Assert.Equal(DrgbPacketBuilder.ProtocolDrgb, packet[0]);
        Assert.Equal(DrgbPacketBuilder.DefaultTimeoutSeconds, packet[1]);
        Assert.Equal(2 + 2 * 3, packet.Length);
        Assert.Equal(128, packet[2]); // 255 * 128 / 255
        Assert.Equal(0, packet[3]);
        Assert.Equal(0, packet[4]);
        Assert.Equal(0, packet[5]);
        Assert.Equal(128, packet[6]);
        Assert.Equal(0, packet[7]);
    }

    [Fact]
    public async Task SendPixelsAsync_DeliversDrgbFrameToConfiguredEndpoint()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        var receive = listener.ReceiveAsync();

        await using var output = new DrgbWledOutput();
        output.Configure(new WledConfig
        {
            ControllerIp = "127.0.0.1",
            UdpPort = port,
            LedCount = 2,
            Brightness = 255
        });

        var pixels = new[]
        {
            RgbColor.FromRgb(10, 20, 30),
            RgbColor.FromRgb(40, 50, 60)
        };
        await output.SendPixelsAsync(pixels, brightness: 255);

        var result = await receive.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(DrgbPacketBuilder.ProtocolDrgb, result.Buffer[0]);
        Assert.Equal(2 + 6, result.Buffer.Length);
        Assert.Equal(10, result.Buffer[2]);
        Assert.Equal(20, result.Buffer[3]);
        Assert.Equal(30, result.Buffer[4]);
        Assert.Equal(40, result.Buffer[5]);
        Assert.Equal(50, result.Buffer[6]);
        Assert.Equal(60, result.Buffer[7]);
    }

    [Fact]
    public async Task SendPixelsAsync_ThrowsWhenControllerIpNotConfigured()
    {
        await using var output = new DrgbWledOutput();
        output.Configure(new WledConfig
        {
            ControllerIp = WledConfig.DefaultControllerIp,
            UdpPort = 21324,
            LedCount = 8
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            output.SendPixelsAsync([RgbColor.FromRgb(1, 2, 3)], 255));

        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
