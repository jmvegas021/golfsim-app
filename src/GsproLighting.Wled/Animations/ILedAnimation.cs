namespace GsproLighting.Wled.Animations;

internal interface ILedAnimation
{
    string Id { get; }
    IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request);
}
