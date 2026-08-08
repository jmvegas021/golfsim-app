namespace GsproLighting.Core.Config;

public sealed class EffectConfig
{
    // WLED FX ids: Fireworks ≈ 89, Strobe ≈ 23 (device catalogs vary slightly by version).
    private const int DefaultCelebrateFxId = 89;
    private const int DefaultHazardFxId = 23;

    public EffectSlot Idle { get; set; } =
        EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.CenterToOutside);

    public EffectSlot NotReady { get; set; } =
        EffectSlot.Curated(RgbColor.FromRgb(220, 20, 20), EffectAnimations.OutsideToCenter);

    public EffectSlot PureStrike { get; set; } =
        EffectSlot.Curated(RgbColor.FromRgb(0, 220, 80), EffectAnimations.DirectionAuto);

    public EffectSlot Mishit { get; set; } =
        EffectSlot.Curated(RgbColor.FromRgb(180, 40, 30), EffectAnimations.DirectionAuto);

    public EffectSlot Putt { get; set; } =
        EffectSlot.Curated(RgbColor.FromRgb(80, 140, 220), EffectAnimations.DirectionAuto);

    public EffectSlot Celebrate { get; set; } =
        EffectSlot.WledPreset(RgbColor.FromRgb(255, 210, 40), DefaultCelebrateFxId);

    public EffectSlot Hazard { get; set; } =
        EffectSlot.WledPreset(RgbColor.FromRgb(220, 20, 20), DefaultHazardFxId);

    public EffectSlot Player { get; set; } =
        EffectSlot.Curated(RgbColor.FromRgb(40, 160, 255), EffectAnimations.Pulse);

    public double PuttMaxBallSpeedMph { get; set; } = 20;
    public double PureMinSmashFactor { get; set; } = 1.45;
    public double MishitMaxSmashFactor { get; set; } = 1.25;

    /// <summary>
    /// Absolute HLA degrees within which a shot counts as center (left / right otherwise).
    /// </summary>
    public double CenterHlaAbsDegrees { get; set; } = 1.5;
}
