using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

internal sealed class MarkerAnimation : ILedAnimation
{
    private readonly AnimationDirection? _direction;

    public MarkerAnimation(string id, AnimationDirection? direction = null)
    {
        Id = id;
        _direction = direction;
    }

    public string Id { get; }

    public IEnumerable<LedAnimationFrame> CreateFrames(LedAnimationRequest request)
    {
        var center = ResolveCenter(request);
        var bandRadius = Math.Max(1, request.LedCount / 30);
        var trailRadius = Math.Max(2, request.LedCount / 12);
        var pixels = AnimationPixels.Empty(request.LedCount);

        for (var index = 0; index < request.LedCount; index++)
        {
            var distance = Math.Abs(index - center);
            if (distance <= bandRadius)
                pixels[index] = request.Color;
            else if (distance <= trailRadius)
                pixels[index] = AnimationPixels.Scale(request.Color, 0.25);
        }

        yield return new LedAnimationFrame(pixels, TimeSpan.FromMilliseconds(180));
    }

    private int ResolveCenter(LedAnimationRequest request)
    {
        var direction = _direction ?? request.Direction;
        var fraction = direction switch
        {
            AnimationDirection.Left => 0.15,
            AnimationDirection.Right => 0.85,
            _ => 0.5
        };
        var center = (int)Math.Round((request.LedCount - 1) * fraction);
        return request.InvertLeftRight ? request.LedCount - 1 - center : center;
    }
}
