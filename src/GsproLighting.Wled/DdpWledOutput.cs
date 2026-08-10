using System.Net;
using System.Net.Sockets;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// WLED realtime UDP using DDP (port 4048 by default) — solid fills and full-strip frames.
/// </summary>
public sealed class DdpWledOutput : IWledOutput
{
    private readonly object _gate = new();
    private UdpClient? _udp;
    private WledConfig _config = new();
    private byte _nextSequence = 1;

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
        var scaled = DdpPacketBuilder.Scale(color, brightness ?? config.Brightness);
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
        byte startingSequence;
        lock (_gate)
        {
            if (_udp is null)
                _udp = new UdpClient();

            udp = _udp;
            config = _config;
            startingSequence = _nextSequence;
        }

        if (!config.HasConfiguredController)
        {
            throw new InvalidOperationException(
                "WLED controller IP is not configured — cannot send DDP realtime frames.");
        }

        if (!IPAddress.TryParse(config.ControllerIp.Trim(), out var address))
        {
            throw new InvalidOperationException(
                $"Invalid WLED controller IP '{config.ControllerIp}' for DDP.");
        }

        var count = Math.Max(1, pixels.Count);
        var scale = brightness ?? config.Brightness;
        var packets = DdpPacketBuilder.BuildFrame(pixels, scale, startingSequence);
        var endpoint = new IPEndPoint(address, config.UdpPort);

        try
        {
            foreach (var packet in packets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await udp.SendAsync(packet, endpoint, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DDP send failed to {config.ControllerIp}:{config.UdpPort} " +
                $"({count} LEDs, {packets.Count} packet(s)): {ex.Message}",
                ex);
        }

        lock (_gate)
        {
            // Advance past the sequences used by this frame (1–15 cycle).
            _nextSequence = AdvanceSequence(startingSequence, packets.Count);
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

    private static byte AdvanceSequence(byte startingSequence, int packetCount)
    {
        var sequence = startingSequence == 0 ? (byte)1 : startingSequence;
        for (var i = 0; i < packetCount; i++)
            sequence = sequence >= 15 ? (byte)1 : (byte)(sequence + 1);
        return sequence;
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
