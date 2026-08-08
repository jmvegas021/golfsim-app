using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class FlashAnimation : ILedAnimation
{
    public string Id => EffectAnimations.Flash;

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        for (var flash = 0; flash < 2; flash++)
        {
            yield return Frame(request, request.Color, 110);
            yield return Frame(request, AnimationPixels.Black, 80);
        }

        yield return Frame(request, request.Color, 0);
    }

    private static LedAnimationFrame Frame(LedAnimationRequest request, RgbColor color, int durationMs) =>
        new(AnimationPixels.Solid(request.LedCount, color), TimeSpan.FromMilliseconds(durationMs));
}
