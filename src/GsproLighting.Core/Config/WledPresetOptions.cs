namespace GsproLighting.Core.Config;

/// <summary>
/// Optional WLED segment parameters for <see cref="EffectMode.WledPreset"/> slots.
/// Null members are omitted from the JSON state payload.
/// </summary>
public sealed class WledPresetOptions
{
    public int? Speed { get; set; }
    public int? Intensity { get; set; }
    public int? PaletteId { get; set; }
    public bool? Overlay { get; set; }

    public WledPresetOptions Clone() => new()
    {
        Speed = Speed,
        Intensity = Intensity,
        PaletteId = PaletteId,
        Overlay = Overlay
    };
}
