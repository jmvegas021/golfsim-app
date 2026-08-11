namespace GsproLighting.Core.Config;

/// <summary>
/// Authored bay lighting slots and shot thresholds.
/// Lighting colors/animations are product defaults — not end-user customization.
/// <see cref="ConfigStore"/> Load/Save call <see cref="ResetLightingSlotsToProductDefaults"/>
/// so disk custom RGB cannot diverge from the product palette.
/// </summary>
public sealed class EffectConfig
{
    // WLED FX ids: Ripple = 79, Chase = 28, Fireworks Starburst ≈ 89, Sparkle ≈ 20
    // (device catalogs vary; ids match stock WLED defaults).
    public const int RippleFxId = 79;
    public const int ChaseFxId = 28;
    public const int CelebrateFxId = 89;
    public const int SparkleFxId = 20;

    /// <summary>WLED built-in palette “Red Reaf” (commonly typed Red Reef).</summary>
    public const int RedReefPaletteId = 62;

    /// <summary>WLED built-in palette Aurora (greens on dark blue). Not palette id 9.</summary>
    public const int AuroraPaletteId = 50;

    /// <summary>~15% of the 0–255 WLED speed/intensity range.</summary>
    public const int RippleTimingByte = 38;

    /// <summary>Maximum WLED speed/intensity (sx/ix).</summary>
    public const int MaxTimingByte = 255;

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
    /// Absolute HLA degrees within which a shot counts as center (green).
    /// Default 1.5°.
    /// </summary>
    public double CenterHlaAbsDegrees { get; set; } = 1.5;

    /// <summary>
    /// Legacy mid-band threshold kept for config round-trip compatibility.
    /// Live mapping is Left / Center / Right using <see cref="CenterHlaAbsDegrees"/> only.
    /// </summary>
    public double MidHlaAbsDegrees { get; set; } = 4.0;

    /// <summary>
    /// Moderate per-state speed / intensity / layers (and concentrate band %) tweaks.
    /// Persisted across Load/Save; not rewritten by <see cref="ResetLightingSlotsToProductDefaults"/>.
    /// </summary>
    public StatusEffectTuning StatusTuning { get; set; } = new();

    public EffectConfig() => ResetLightingSlotsToProductDefaults();

    /// <summary>
    /// Locks researched product lighting (colors + animations + WLED FX ids).
    /// Does not change smash/putt/HLA thresholds.
    /// </summary>
    public void ResetLightingSlotsToProductDefaults()
    {
        // Basic ambient — WLED Ripple (layered, Red Reef, colors max, timing 15%).
        Idle = CreateRippleAmbient(RgbColor.FromRgb(61, 220, 132));
        // Not ready — red Chase + Red Reef at max sx/ix (not Aurora).
        NotReady = CreateRedChase(RgbColor.FromRgb(229, 83, 61));
        // Waiting — same Ripple ambient with amber tint while Connect state is unknown.
        Waiting = CreateRippleAmbient(RgbColor.FromRgb(212, 160, 23));
        PureStrike = EffectSlot.Curated(RgbColor.FromRgb(0, 224, 90), EffectAnimations.DirectionAuto);
        Mishit = EffectSlot.Curated(RgbColor.FromRgb(198, 40, 40), EffectAnimations.DirectionAuto);
        Putt = EffectSlot.Curated(RgbColor.FromRgb(79, 159, 224), EffectAnimations.DirectionAuto);
        Celebrate = EffectSlot.WledPreset(RgbColor.FromRgb(255, 213, 74), CelebrateFxId);
        Hazard = EffectSlot.WledPreset(RgbColor.FromRgb(229, 83, 61), SparkleFxId);
        WaterHazard = EffectSlot.Curated(RgbColor.FromRgb(0, 168, 200), EffectAnimations.Flash);
        OutOfBounds = EffectSlot.Curated(RgbColor.FromRgb(255, 42, 42), EffectAnimations.Flash);
        Player = EffectSlot.Curated(RgbColor.FromRgb(47, 160, 255), EffectAnimations.Pulse);
    }

    public static EffectSlot CreateRippleAmbient(RgbColor color) =>
        EffectSlot.WledPreset(
            color.WithMaxIntensity(),
            RippleFxId,
            new WledPresetOptions
            {
                Speed = RippleTimingByte,
                Intensity = RippleTimingByte,
                PaletteId = RedReefPaletteId,
                Overlay = true
            });

    /// <summary>Red Chase + Red Reef at max speed/intensity (Not Ready live path).</summary>
    public static EffectSlot CreateRedChase(RgbColor color) =>
        EffectSlot.WledPreset(
            color.WithMaxIntensity(),
            ChaseFxId,
            new WledPresetOptions
            {
                Speed = MaxTimingByte,
                Intensity = MaxTimingByte,
                PaletteId = RedReefPaletteId
            });

    /// <summary>Chase + Aurora at max speed/intensity (Ready live path).</summary>
    public static EffectSlot CreateChaseAurora(RgbColor color) =>
        EffectSlot.WledPreset(
            color.WithMaxIntensity(),
            ChaseFxId,
            new WledPresetOptions
            {
                Speed = MaxTimingByte,
                Intensity = MaxTimingByte,
                PaletteId = AuroraPaletteId
            });

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
        CenterHlaAbsDegrees = CenterHlaAbsDegrees,
        MidHlaAbsDegrees = MidHlaAbsDegrees,
        StatusTuning = StatusTuning.Clone()
    };
}
