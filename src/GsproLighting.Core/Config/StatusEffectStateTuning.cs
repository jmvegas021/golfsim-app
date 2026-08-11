namespace GsproLighting.Core.Config;

/// <summary>
/// Moderate per-state tweaks relative to product defaults (1.0 = current look).
/// Waiting maps Speed/Intensity to WLED Ripple <c>sx</c>/<c>ix</c>; DDP states
/// scale shimmer travel, peak gain, and wing richness.
/// </summary>
public sealed class StatusEffectStateTuning
{
    public const double DefaultMultiplier = 1.0;
    public const double DefaultBandSizePercent = 28;

    public const double MinMultiplier = 0.25;
    public const double MaxMultiplier = 3.0;
    public const double MinBandSizePercent = 15;
    public const double MaxBandSizePercent = 45;

    /// <summary>1.0 = product shimmer / intro / Ripple sx default.</summary>
    public double Speed { get; set; } = DefaultMultiplier;

    /// <summary>1.0 = product peak gain / Ripple ix default.</summary>
    public double Intensity { get; set; } = DefaultMultiplier;

    /// <summary>1.0 = product wing/halo richness (or Waiting brightness scale).</summary>
    public double Layers { get; set; } = DefaultMultiplier;

    /// <summary>
    /// Concentrate band size as percent of strip (Ready / Direction only).
    /// Default 28. Clamped to 15–45. Ignored for Not Ready / Waiting.
    /// </summary>
    public double BandSizePercent { get; set; } = DefaultBandSizePercent;

    public static StatusEffectStateTuning CreateDefaults() => new();

    public StatusEffectStateTuning Clone() => new()
    {
        Speed = Speed,
        Intensity = Intensity,
        Layers = Layers,
        BandSizePercent = BandSizePercent
    };

    public void Clamp(bool includeBandSize = true)
    {
        Speed = ClampMultiplier(Speed);
        Intensity = ClampMultiplier(Intensity);
        Layers = ClampMultiplier(Layers);
        if (includeBandSize)
            BandSizePercent = Math.Clamp(BandSizePercent, MinBandSizePercent, MaxBandSizePercent);
    }

    public static double ClampMultiplier(double value) =>
        Math.Clamp(value, MinMultiplier, MaxMultiplier);

    public bool IsProductDefault() =>
        Math.Abs(Speed - DefaultMultiplier) < 0.0001 &&
        Math.Abs(Intensity - DefaultMultiplier) < 0.0001 &&
        Math.Abs(Layers - DefaultMultiplier) < 0.0001 &&
        Math.Abs(BandSizePercent - DefaultBandSizePercent) < 0.0001;
}
