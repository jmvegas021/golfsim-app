using System.Net;
using System.Net.Sockets;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// WLED realtime UDP using DRGB (protocol 2) — solid fills and full-strip frames.
/// </summary>
public sealed class DrgbWledOutput : IWledOutput
{
    private readonly object _gate = new();
    private UdpClient? _udp;
    private WledConfig _config = new();

    public void Configure(WledConfig config)
    {
        lock (_gate)
        {
            _config = config;
            _udp?.Dispose();
            _udp = new UdpClient();
        }
    }

    public Task SendSolidAsync(
        RgbColor color,
        byte? brightness = null,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Max(1, _config.LedCount);
        var pixels = new RgbColor[count];
        var scaled = Scale(color, brightness ?? _config.Brightness);
        Array.Fill(pixels, scaled);
        return SendPixelsAsync(pixels, 255, cancellationToken);
    }

    public async Task SendPixelsAsync(
        IReadOnlyList<RgbColor> pixels,
        byte? brightness = null,
        CancellationToken cancellationToken = default)
    {
        UdpClient udp;
        WledConfig config;
        lock (_gate)
        {
            if (_udp is null)
                Configure(_config);
            udp = _udp!;
            config = _config;
        }

        var count = Math.Min(pixels.Count, Math.Max(1, config.LedCount));
        var packet = new byte[2 + count * 3];
        packet[0] = 2; // DRGB
        packet[1] = 5; // seconds until WLED resumes normal effects
        var scale = brightness ?? config.Brightness;

        for (var i = 0; i < count; i++)
        {
            var c = Scale(pixels[i], scale);
            var offset = 2 + i * 3;
            packet[offset] = c.R;
            packet[offset + 1] = c.G;
            packet[offset + 2] = c.B;
        }

        var endpoint = new IPEndPoint(IPAddress.Parse(config.ControllerIp), config.UdpPort);
        await udp.SendAsync(packet, endpoint, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        SendSolidAsync(RgbColor.FromRgb(0, 0, 0), 0, cancellationToken);

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _udp?.Dispose();
            _udp = null;
        }

        return ValueTask.CompletedTask;
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
