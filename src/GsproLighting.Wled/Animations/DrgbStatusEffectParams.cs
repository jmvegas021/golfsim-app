using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Resolved DDP runtime parameters for intro + band shimmer, derived from
/// <see cref="StatusEffectStateTuning"/> multipliers (defaults preserve product look).
/// </summary>
public sealed class DrgbStatusEffectParams
{
    public static DrgbStatusEffectParams ProductDefaults { get; } = new();

    public double ShimmerBandWidthsPerSecond { get; init; } = DrgbBandShimmerEffect.BandWidthsPerSecond;

    public double BaseGain { get; init; } = DrgbBandShimmerEffect.BaseGain;

    public double PeakGain { get; init; } = DrgbBandShimmerEffect.PeakGain;

    public double HaloHalfWidthFraction { get; init; } = DrgbBandShimmerEffect.HaloHalfWidthFraction;

    public double WingHalfWidthFraction { get; init; } = DrgbBandShimmerEffect.WingHalfWidthFraction;

    public double CoreHalfWidthFraction { get; init; } = DrgbBandShimmerEffect.CoreHalfWidthFraction;

    /// <summary>Scales per-frame lit advance (higher = faster intro).</summary>
    public double IntroSpeedScale { get; init; } = 1.0;

    public double ConcentrateLitFraction { get; init; } =
        DrgbConcentrateBandGeometry.ConcentrateLitFraction;

    public static DrgbStatusEffectParams FromTuning(StatusEffectStateTuning? tuning)
    {
        var source = tuning?.Clone() ?? StatusEffectStateTuning.CreateDefaults();
        source.Clamp(includeBandSize: true);

        var intensity = source.Intensity;
        var layers = source.Layers;
        return new DrgbStatusEffectParams
        {
            ShimmerBandWidthsPerSecond =
                DrgbBandShimmerEffect.BandWidthsPerSecond * source.Speed,
            IntroSpeedScale = source.Speed,
            PeakGain = Math.Clamp(
                DrgbBandShimmerEffect.PeakGain * intensity,
                0.15,
                1.0),
            BaseGain = Math.Clamp(
                DrgbBandShimmerEffect.BaseGain * intensity,
                0.05,
                0.55),
            HaloHalfWidthFraction = Math.Clamp(
                DrgbBandShimmerEffect.HaloHalfWidthFraction * layers,
                0.12,
                0.75),
            WingHalfWidthFraction = Math.Clamp(
                DrgbBandShimmerEffect.WingHalfWidthFraction * layers,
                0.08,
                0.55),
            CoreHalfWidthFraction = Math.Clamp(
                DrgbBandShimmerEffect.CoreHalfWidthFraction * Math.Sqrt(layers),
                0.04,
                0.22),
            ConcentrateLitFraction = Math.Clamp(
                source.BandSizePercent / 100.0,
                StatusEffectStateTuning.MinBandSizePercent / 100.0,
                StatusEffectStateTuning.MaxBandSizePercent / 100.0)
        };
    }

    /// <summary>
    /// Maps Waiting multipliers onto Ripple <c>sx</c>/<c>ix</c> and brightness.
    /// </summary>
    public static (int Speed, int Intensity, byte Brightness) ResolveWaitingRipple(
        StatusEffectStateTuning? tuning,
        byte brightness)
    {
        var source = tuning?.Clone() ?? StatusEffectStateTuning.CreateDefaults();
        source.Clamp(includeBandSize: false);

        var speed = ScaleTimingByte(EffectConfig.RippleTimingByte, source.Speed);
        var intensity = ScaleTimingByte(EffectConfig.RippleTimingByte, source.Intensity);
        var scaledBrightness = (byte)Math.Clamp(
            (int)Math.Round(brightness * source.Layers),
            1,
            255);
        return (speed, intensity, scaledBrightness);
    }

    private static int ScaleTimingByte(int baseline, double multiplier) =>
        Math.Clamp((int)Math.Round(baseline * multiplier), 1, EffectConfig.MaxTimingByte);
}
