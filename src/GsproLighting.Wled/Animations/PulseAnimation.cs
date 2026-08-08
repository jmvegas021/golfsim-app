using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class PulseAnimation : ILedAnimation
{
    public string Id => EffectAnimations.Pulse;

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        const int steps = 14;
        for (var step = 0; step < steps; step++)
        {
            var phase = Math.Sin(Math.PI * step / (steps - 1));
            var color = AnimationPixels.Scale(request.Color, 0.2 + 0.8 * phase);
            yield return new LedAnimationFrame(
                AnimationPixels.Solid(request.LedCount, color),
                TimeSpan.FromMilliseconds(38));
        }

        yield return new LedAnimationFrame(
            AnimationPixels.Solid(request.LedCount, request.Color),
            TimeSpan.Zero);
    }
}
