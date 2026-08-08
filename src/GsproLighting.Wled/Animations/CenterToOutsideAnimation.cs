using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class CenterToOutsideAnimation : ILedAnimation
{
    public string Id => EffectAnimations.CenterToOutside;

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        var pixels = AnimationPixels.Empty(request.LedCount);
        var leftCenter = (request.LedCount - 1) / 2;
        var rightCenter = request.LedCount / 2;
        var steps = Math.Max(leftCenter + 1, request.LedCount - rightCenter);

        for (var step = 0; step < steps; step++)
        {
            var left = leftCenter - step;
            var right = rightCenter + step;
            if (left >= 0)
                pixels[left] = request.Color;
            if (right < request.LedCount)
                pixels[right] = request.Color;
            yield return new LedAnimationFrame((RgbColor[])pixels.Clone(), TimeSpan.FromMilliseconds(32));
        }
    }
}
