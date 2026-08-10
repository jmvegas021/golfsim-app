using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Legacy curated marker frames — uses the same concentrate-band geometry as live DDP L/C/R.
/// </summary>
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
        var direction = ToShotDirection(_direction ?? request.Direction, request.InvertLeftRight);
        var band = DrgbConcentrateBandGeometry.Resolve(direction, request.LedCount);
        var pixels = DrgbReadyFrameFactory.CreateBand(
            request.LedCount,
            band.Start,
            band.LitCount,
            request.Color);
        yield return new LedAnimationFrame(pixels, TimeSpan.FromMilliseconds(180));
    }

    private static ShotDirection ToShotDirection(AnimationDirection direction, bool invert)
    {
        var effective = invert
            ? direction switch
            {
                AnimationDirection.Left => AnimationDirection.Right,
                AnimationDirection.Right => AnimationDirection.Left,
                _ => direction
            }
            : direction;

        return effective switch
        {
            AnimationDirection.Left => ShotDirection.Left,
            AnimationDirection.Right => ShotDirection.Right,
            _ => ShotDirection.Center
        };
    }
}
