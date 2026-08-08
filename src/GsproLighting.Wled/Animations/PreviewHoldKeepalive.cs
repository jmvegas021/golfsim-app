using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Re-sends DRGB hold frames so WLED realtime timeout (~5s) cannot drop the hold.
/// </summary>
public sealed class PreviewHoldKeepalive
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(2500);

    public Task HoldAsync(
        IWledOutput output,
        PreviewHoldPlan plan,
        WledConfig config,
        TimeSpan? duration,
        CancellationToken cancellationToken = default,
        bool sendInitialFrame = true) =>
        RunLoopAsync(
            () => SendHoldFrameAsync(output, plan, config, cancellationToken),
            duration,
            cancellationToken,
            sendInitialFrame);

    public Task HoldSolidAsync(
        IWledOutput output,
        RgbColor color,
        byte brightness,
        TimeSpan? duration,
        CancellationToken cancellationToken = default,
        bool sendInitialFrame = true) =>
        RunLoopAsync(
            () => output.SendSolidAsync(color, brightness, cancellationToken),
            duration,
            cancellationToken,
            sendInitialFrame);

    private async Task RunLoopAsync(
        Func<Task> sendFrame,
        TimeSpan? duration,
        CancellationToken cancellationToken,
        bool sendInitialFrame)
    {
        ArgumentNullException.ThrowIfNull(sendFrame);

        if (sendInitialFrame)
            await sendFrame().ConfigureAwait(false);

        var deadline = duration is TimeSpan finite
            ? DateTime.UtcNow + finite
            : (DateTime?)null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            if (deadline is DateTime end && now >= end)
                break;

            var wait = Interval;
            if (deadline is DateTime limited)
            {
                var remaining = limited - now;
                if (remaining <= TimeSpan.Zero)
                    break;
                if (remaining < wait)
                    wait = remaining;
            }

            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);

            if (deadline is DateTime afterDelay && DateTime.UtcNow >= afterDelay)
                break;

            await sendFrame().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static Task SendHoldFrameAsync(
        IWledOutput output,
        PreviewHoldPlan plan,
        WledConfig config,
        CancellationToken cancellationToken)
    {
        if (plan.HoldAsSolid || plan.Slot.Mode != EffectMode.Curated)
            return output.SendSolidAsync(plan.Slot.Color, plan.HoldBrightness, cancellationToken);

        var pixels = PreviewHoldFrameBuilder.BuildMarkerFrame(plan, config);
        return output.SendPixelsAsync(pixels, brightness: 255, cancellationToken);
    }
}
