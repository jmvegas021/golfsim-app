using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class OutsideToCenterAnimation : ILedAnimation
{
    public string Id => EffectAnimations.OutsideToCenter;

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        var pixels = AnimationPixels.Empty(request.LedCount);
        var steps = (request.LedCount + 1) / 2;

        for (var step = 0; step < steps; step++)
        {
            pixels[step] = request.Color;
            pixels[request.LedCount - 1 - step] = request.Color;
            yield return new LedAnimationFrame((RgbColor[])pixels.Clone(), TimeSpan.FromMilliseconds(32));
        }
    }
}
