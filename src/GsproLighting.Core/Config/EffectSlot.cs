using System.Text.Json.Serialization;

namespace GsproLighting.Core.Config;

/// <summary>
/// One lighting effect: color plus curated animation or WLED FX preset.
/// </summary>
[JsonConverter(typeof(EffectSlotJsonConverter))]
public sealed class EffectSlot
{
    public RgbColor Color { get; set; } = RgbColor.FromRgb(0, 0, 0);
    public EffectMode Mode { get; set; } = EffectMode.Curated;
    public string Animation { get; set; } = EffectAnimations.Solid;
    public int? WledFxId { get; set; }

    public static EffectSlot Curated(RgbColor color, string animation) => new()
    {
        Color = color,
        Mode = EffectMode.Curated,
        Animation = animation
    };

    public static EffectSlot WledPreset(RgbColor color, int fxId) => new()
    {
        Color = color,
        Mode = EffectMode.WledPreset,
        Animation = EffectAnimations.Solid,
        WledFxId = fxId
    };
}
