using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class SolidAnimation : ILedAnimation
{
    public string Id => EffectAnimations.Solid;

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        yield return new LedAnimationFrame(
            AnimationPixels.Solid(request.LedCount, request.Color),
            TimeSpan.Zero);
    }
}
