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
        ArgumentNullException.ThrowIfNull(config);
        lock (_gate)
        {
            // Snapshot so later Config.Wled mutations cannot silently retarget UDP without
            // an explicit Configure (Preview / effect-sink must sync Connection → output).
            _config = Clone(config);
            _udp?.Dispose();
            _udp = new UdpClient();
        }
    }

    public Task SendSolidAsync(
        RgbColor color,
        byte? brightness = null,
        CancellationToken cancellationToken = default)
    {
        WledConfig config;
        lock (_gate)
            config = _config;

        var count = Math.Max(1, config.LedCount);
        var pixels = new RgbColor[count];
        var scaled = DrgbPacketBuilder.Scale(color, brightness ?? config.Brightness);
        Array.Fill(pixels, scaled);
        return SendPixelsAsync(pixels, 255, cancellationToken);
    }

    public async Task SendPixelsAsync(
        IReadOnlyList<RgbColor> pixels,
        byte? brightness = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        UdpClient udp;
        WledConfig config;
        lock (_gate)
        {
            if (_udp is null)
            {
                _udp = new UdpClient();
            }

            udp = _udp;
            config = _config;
        }

        if (!config.HasConfiguredController)
        {
            throw new InvalidOperationException(
                "WLED controller IP is not configured — cannot send DRGB realtime frames.");
        }

        if (!IPAddress.TryParse(config.ControllerIp.Trim(), out var address))
        {
            throw new InvalidOperationException(
                $"Invalid WLED controller IP '{config.ControllerIp}' for DRGB.");
        }

        // Frame length is authoritative — callers resolve LedCount from Connection / snapshot.
        var count = Math.Max(1, pixels.Count);
        var scale = brightness ?? config.Brightness;
        var packet = DrgbPacketBuilder.Build(pixels, scale);
        var endpoint = new IPEndPoint(address, config.UdpPort);

        try
        {
            await udp.SendAsync(packet, endpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DRGB send failed to {config.ControllerIp}:{config.UdpPort} " +
                $"({count} LEDs, protocol {DrgbPacketBuilder.ProtocolDrgb}): {ex.Message}",
                ex);
        }
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

    private static WledConfig Clone(WledConfig source) => new()
    {
        ControllerIp = source.ControllerIp,
        UdpPort = source.UdpPort,
        LedCount = source.LedCount,
        Brightness = source.Brightness,
        Protocol = source.Protocol,
        InvertLeftRight = source.InvertLeftRight
    };
}
