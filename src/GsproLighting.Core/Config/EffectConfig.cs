namespace GsproLighting.Core.Config;

/// <summary>
/// Authored bay lighting slots and shot thresholds.
/// Lighting colors/animations are product defaults — not end-user customization.
/// <see cref="ConfigStore"/> Load/Save call <see cref="ResetLightingSlotsToProductDefaults"/>
/// so disk custom RGB cannot diverge from the product palette.
/// </summary>
public sealed class EffectConfig
{
    // WLED FX ids: Fireworks Starburst ≈ 89, Sparkle ≈ 20 (device catalogs vary).
    public const int CelebrateFxId = 89;
    public const int SparkleFxId = 20;

    public EffectSlot Idle { get; set; } = null!;
    public EffectSlot NotReady { get; set; } = null!;
    public EffectSlot Waiting { get; set; } = null!;
    public EffectSlot PureStrike { get; set; } = null!;
    public EffectSlot Mishit { get; set; } = null!;
    public EffectSlot Putt { get; set; } = null!;
    public EffectSlot Celebrate { get; set; } = null!;
    public EffectSlot Hazard { get; set; } = null!;
    public EffectSlot WaterHazard { get; set; } = null!;
    public EffectSlot OutOfBounds { get; set; } = null!;
    public EffectSlot Player { get; set; } = null!;

    public double PuttMaxBallSpeedMph { get; set; } = 20;
    public double PureMinSmashFactor { get; set; } = 1.45;
    public double MishitMaxSmashFactor { get; set; } = 1.25;

    /// <summary>
    /// Absolute HLA degrees within which a shot counts as center (left / right otherwise).
    /// </summary>
    public double CenterHlaAbsDegrees { get; set; } = 1.5;

    public EffectConfig() => ResetLightingSlotsToProductDefaults();

    /// <summary>
    /// Locks researched product lighting (colors + animations + WLED FX ids).
    /// Does not change smash/putt/HLA thresholds.
    /// </summary>
    public void ResetLightingSlotsToProductDefaults()
    {
        // Ready — fairway green center→out, then solid hold.
        Idle = EffectSlot.Curated(RgbColor.FromRgb(61, 220, 132), EffectAnimations.CenterToOutside);
        // Not ready — alert red outside→center, then dim hold.
        NotReady = EffectSlot.Curated(RgbColor.FromRgb(229, 83, 61), EffectAnimations.OutsideToCenter);
        // Waiting — amber solid (no pulse anxiety).
        Waiting = EffectSlot.Curated(RgbColor.FromRgb(212, 160, 23), EffectAnimations.Solid);
        PureStrike = EffectSlot.Curated(RgbColor.FromRgb(0, 224, 90), EffectAnimations.DirectionAuto);
        Mishit = EffectSlot.Curated(RgbColor.FromRgb(198, 40, 40), EffectAnimations.DirectionAuto);
        Putt = EffectSlot.Curated(RgbColor.FromRgb(79, 159, 224), EffectAnimations.DirectionAuto);
        Celebrate = EffectSlot.WledPreset(RgbColor.FromRgb(255, 213, 74), CelebrateFxId);
        Hazard = EffectSlot.WledPreset(RgbColor.FromRgb(229, 83, 61), SparkleFxId);
        WaterHazard = EffectSlot.Curated(RgbColor.FromRgb(0, 168, 200), EffectAnimations.Flash);
        OutOfBounds = EffectSlot.Curated(RgbColor.FromRgb(255, 42, 42), EffectAnimations.Flash);
        Player = EffectSlot.Curated(RgbColor.FromRgb(47, 160, 255), EffectAnimations.Pulse);
    }

    public EffectConfig Clone() => new()
    {
        Idle = Idle.Clone(),
        NotReady = NotReady.Clone(),
        Waiting = Waiting.Clone(),
        PureStrike = PureStrike.Clone(),
        Mishit = Mishit.Clone(),
        Putt = Putt.Clone(),
        Celebrate = Celebrate.Clone(),
        Hazard = Hazard.Clone(),
        WaterHazard = WaterHazard.Clone(),
        OutOfBounds = OutOfBounds.Clone(),
        Player = Player.Clone(),
        PuttMaxBallSpeedMph = PuttMaxBallSpeedMph,
        PureMinSmashFactor = PureMinSmashFactor,
        MishitMaxSmashFactor = MishitMaxSmashFactor,
        CenterHlaAbsDegrees = CenterHlaAbsDegrees
    };
}
