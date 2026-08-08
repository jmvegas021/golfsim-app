using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class SweepAnimation : ILedAnimation
{
    public string Id => EffectAnimations.Sweep;

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        for (var step = 0; step < request.LedCount; step++)
        {
            var head = request.InvertLeftRight ? request.LedCount - 1 - step : step;
            var pixels = AnimationPixels.Empty(request.LedCount);
            pixels[head] = request.Color;
            AddTrail(pixels, head - 1, request.Color);
            AddTrail(pixels, head + 1, request.Color);
            yield return new LedAnimationFrame(pixels, TimeSpan.FromMilliseconds(28));
        }

        yield return new LedAnimationFrame(
            AnimationPixels.Solid(request.LedCount, request.Color),
            TimeSpan.Zero);
    }

    private static void AddTrail(RgbColor[] pixels, int index, RgbColor color)
    {
        if (index >= 0 && index < pixels.Length)
            pixels[index] = AnimationPixels.Scale(color, 0.33);
    }
}
