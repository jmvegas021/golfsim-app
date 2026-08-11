using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// GSPro loading / start-screen cue: quick center-out aqua expand, then full-strip
/// center→out shimmer (distinct from Ready green).
/// </summary>
public static class DrgbWaitingFrameFactory
{
    public const int FrameCadenceMilliseconds = DrgbReadyFrameFactory.FrameCadenceMilliseconds;
    public const int LitAdvancePerFrame = DrgbReadyFrameFactory.LitAdvancePerFrame;

    /// <summary>Blue/aqua loading tint — not Ready green.</summary>
    public static readonly RgbColor WaitingAqua = RgbColor.FromRgb(0, 200, 220);

    public static IReadOnlyList<LedAnimationFrame> CreateWaitingSequence(
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var parity = ledCount % 2 == 0 ? 2 : 1;
        var step = DrgbReadyFrameFactory.ResolveLitAdvance(ledCount, p.IntroSpeedScale);
        var advance = Math.Max(2, step * 2);
        var frames = new List<LedAnimationFrame>
        {
            new(DrgbReadyFrameFactory.CreateEmpty(ledCount), cadence)
        };

        for (var lit = parity; lit < ledCount; lit = Math.Min(ledCount, lit + advance))
        {
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateCenterBand(ledCount, lit, WaitingAqua),
                cadence));
        }

        if (frames[^1].Duration != TimeSpan.Zero)
        {
            frames.Add(new LedAnimationFrame(
                AnimationPixels.Solid(ledCount, WaitingAqua),
                TimeSpan.Zero));
        }

        return frames;
    }

    public static RgbColor[] CreateHoldPixels(int ledCount) =>
        AnimationPixels.Solid(ledCount, WaitingAqua);
}
