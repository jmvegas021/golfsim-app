using System.Diagnostics;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Streams in-process LED frames over <see cref="IWledOutput"/> (DDP realtime).
/// Supports CancellationToken supersede, hold-effect loops, and static pixel keepalive.
/// </summary>
public sealed class DrgbStripAnimationPlayer
{
    private readonly IWledOutput _output;
    private readonly PreviewHoldKeepalive _keepalive = new()
    {
        // WLED realtime timeout drops live mode — 1.5s keepalive for static holds only.
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

    /// <summary>
    /// Streams evolving hold frames at intro cadence until cancelled or <paramref name="duration"/> elapses.
    /// Continuous DDP traffic replaces static keepalive while the effect runs.
    /// </summary>
    public async Task HoldEffectAsync(
        IDrgbHoldEffect effect,
        int ledCount,
        byte brightness,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var cadence = TimeSpan.FromMilliseconds(DrgbReadyFrameFactory.FrameCadenceMilliseconds);
        var clock = Stopwatch.StartNew();
        var deadline = duration is TimeSpan finite
            ? DateTime.UtcNow + finite
            : (DateTime?)null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (deadline is DateTime end && DateTime.UtcNow >= end)
                break;

            var pixels = effect.RenderFrame(ledCount, clock.Elapsed);
            await _output.SendPixelsAsync(pixels, brightness, cancellationToken)
                .ConfigureAwait(false);

            if (deadline is DateTime limited)
            {
                var remaining = limited - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;
                if (remaining < cadence)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                    break;
                }
            }

            await Task.Delay(cadence, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
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
}
