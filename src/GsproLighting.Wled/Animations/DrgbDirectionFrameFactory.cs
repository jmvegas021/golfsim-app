using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Pixel-frame hit-direction choreography for DDP: slide/expand a concentrate-sized
/// band into Left / Center / Right; hold shimmer is owned by <see cref="DrgbBandShimmerEffect"/>.
/// </summary>
public static class DrgbDirectionFrameFactory
{
    public const int FrameCadenceMilliseconds = DrgbReadyFrameFactory.FrameCadenceMilliseconds;
    public const int LitAdvancePerFrame = DrgbReadyFrameFactory.LitAdvancePerFrame;

    /// <summary>Center hit cue — same green as Ready hold.</summary>
    public static readonly RgbColor DirectionCenterGreen = DrgbReadyFrameFactory.ReadyGreen;

    /// <summary>Left/Right hit cue — matches historical HTTP direction yellow.</summary>
    public static readonly RgbColor DirectionSideYellow = RgbColor.FromRgb(220, 180, 0);

    public static IReadOnlyList<LedAnimationFrame> CreateDirectionSequence(
        ShotDirection direction,
        int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var target = DrgbConcentrateBandGeometry.Resolve(direction, ledCount);
        var color = ResolveColor(direction);
        return direction == ShotDirection.Center
            ? CreateExpandIntoBand(ledCount, target, color)
            : CreateSlideIntoBand(ledCount, target, color);
    }

    public static RgbColor[] CreateHoldPixels(ShotDirection direction, int ledCount)
    {
        var band = DrgbConcentrateBandGeometry.Resolve(direction, ledCount);
        return DrgbReadyFrameFactory.CreateBand(
            ledCount,
            band.Start,
            band.LitCount,
            ResolveColor(direction));
    }

    public static RgbColor ResolveColor(ShotDirection direction) =>
        direction is ShotDirection.Left or ShotDirection.Right
            ? DirectionSideYellow
            : DirectionCenterGreen;

    private static IReadOnlyList<LedAnimationFrame> CreateExpandIntoBand(
        int ledCount,
        LedBandRange target,
        RgbColor color)
    {
        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var advance = DrgbReadyFrameFactory.ResolveLitAdvance(ledCount);
        var parity = ledCount % 2 == 0 ? 2 : 1;
        var frames = new List<LedAnimationFrame>();
        frames.Add(new LedAnimationFrame(DrgbReadyFrameFactory.CreateEmpty(ledCount), cadence));

        for (var lit = parity; lit < target.LitCount; lit += advance)
        {
            var start = (ledCount - lit) / 2;
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateBand(ledCount, start, lit, color),
                cadence));
        }

        frames.Add(new LedAnimationFrame(
            DrgbReadyFrameFactory.CreateBand(ledCount, target.Start, target.LitCount, color),
            TimeSpan.Zero));
        return frames;
    }

    private static IReadOnlyList<LedAnimationFrame> CreateSlideIntoBand(
        int ledCount,
        LedBandRange target,
        RgbColor color)
    {
        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var advance = DrgbReadyFrameFactory.ResolveLitAdvance(ledCount);
        var fromStart = DrgbConcentrateBandGeometry.ResolveCenter(ledCount).Start;
        var frames = new List<LedAnimationFrame>
        {
            new(DrgbReadyFrameFactory.CreateEmpty(ledCount), cadence)
        };

        if (fromStart == target.Start)
        {
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateBand(ledCount, target.Start, target.LitCount, color),
                TimeSpan.Zero));
            return frames;
        }

        var step = target.Start < fromStart ? -advance : advance;
        for (var start = fromStart; start != target.Start; )
        {
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateBand(ledCount, start, target.LitCount, color),
                cadence));
            var next = start + step;
            start = step < 0
                ? Math.Max(target.Start, next)
                : Math.Min(target.Start, next);
        }

        frames.Add(new LedAnimationFrame(
            DrgbReadyFrameFactory.CreateBand(ledCount, target.Start, target.LitCount, color),
            TimeSpan.Zero));
        return frames;
    }
}
