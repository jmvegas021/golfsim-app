using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// Simple preview animations for the settings Test button (v0.2 smoke test).
/// </summary>
public sealed class WledPreviewPlayer
{
    private readonly IWledOutput _output;

    public WledPreviewPlayer(IWledOutput output)
    {
        _output = output;
    }

    public async Task PlaySweepAsync(RgbColor color, int ledCount, CancellationToken cancellationToken = default)
    {
        ledCount = Math.Max(1, ledCount);
        for (var head = 0; head < ledCount; head++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = new RgbColor[ledCount];
            for (var i = 0; i < ledCount; i++)
            {
                var distance = Math.Abs(i - head);
                if (distance == 0)
                    frame[i] = color;
                else if (distance == 1)
                    frame[i] = RgbColor.FromRgb((byte)(color.R / 3), (byte)(color.G / 3), (byte)(color.B / 3));
                else
                    frame[i] = RgbColor.FromRgb(0, 0, 0);
            }

            await _output.SendPixelsAsync(frame, cancellationToken: cancellationToken);
            await Task.Delay(28, cancellationToken);
        }

        await _output.SendSolidAsync(color, cancellationToken: cancellationToken);
        await Task.Delay(250, cancellationToken);
        await _output.ClearAsync(cancellationToken);
    }

    public async Task PlayIdleGlowAsync(RgbColor color, CancellationToken cancellationToken = default)
    {
        await _output.SendSolidAsync(color, cancellationToken: cancellationToken);
        await Task.Delay(800, cancellationToken);
        await _output.ClearAsync(cancellationToken);
    }
}
