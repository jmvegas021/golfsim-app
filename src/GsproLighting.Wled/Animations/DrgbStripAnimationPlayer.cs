using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Streams in-process LED frames over <see cref="IWledOutput"/> (DDP realtime).
/// Supports CancellationToken supersede and solid/pixel keepalive holds.
/// </summary>
public sealed class DrgbStripAnimationPlayer
{
    private readonly IWledOutput _output;
    private readonly PreviewHoldKeepalive _keepalive = new()
    {
        // WLED realtime timeout drops live mode — 1.5s keepalive keeps Ready/Not Ready holds alive.
        Interval = TimeSpan.FromMilliseconds(1500)
    };

    public DrgbStripAnimationPlayer(IWledOutput output) =>
        _output = output ?? throw new ArgumentNullException(nameof(output));

    public async Task PlayAsync(
        IReadOnlyList<LedAnimationFrame> frames,
        byte brightness,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);

        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _output.SendPixelsAsync(frame.Pixels, brightness, cancellationToken)
                .ConfigureAwait(false);
            if (frame.Duration > TimeSpan.Zero)
                await Task.Delay(frame.Duration, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task HoldPixelsAsync(
        IReadOnlyList<RgbColor> pixels,
        byte brightness,
        TimeSpan? duration,
        CancellationToken cancellationToken = default,
        bool sendInitialFrame = true) =>
        _keepalive.HoldWhileAsync(
            ct => _output.SendPixelsAsync(pixels, brightness, ct),
            duration,
            cancellationToken,
            sendInitialFrame);

    public async Task HoldBreathingSolidAsync(
        RgbColor color,
        byte brightness,
        IReadOnlyList<double> levels,
        TimeSpan cadence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count == 0)
            throw new ArgumentException("Breathing levels are required.", nameof(levels));

        var index = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var frameBrightness = DrgbNotReadyFrameFactory.ScaleBrightness(
                brightness,
                levels[index % levels.Count]);
            index++;
            await _output.SendSolidAsync(color, frameBrightness, cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(cadence, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
