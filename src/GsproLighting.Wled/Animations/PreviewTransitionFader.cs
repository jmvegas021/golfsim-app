using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Animations;

/// <summary>Brief brightness fade between held preview states (avoids hard cuts).</summary>
public sealed class PreviewTransitionFader
{
    public static readonly TimeSpan DefaultStep = TimeSpan.FromMilliseconds(45);
    private const int Steps = 4;

    public async Task FadeOutAsync(
        IWledOutput output,
        RgbColor color,
        byte fromBrightness,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (fromBrightness == 0)
            return;

        for (var step = Steps; step >= 1; step--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var brightness = (byte)Math.Max(1, fromBrightness * step / (Steps + 1));
            await output.SendSolidAsync(color, brightness, cancellationToken).ConfigureAwait(false);
            await Task.Delay(DefaultStep, cancellationToken).ConfigureAwait(false);
        }
    }
}
