using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// HTTP <c>/json/state</c> payload for applying a WLED effect preset to segment 0.
/// </summary>
public sealed class WledPresetRequest
{
    public required int FxId { get; init; }
    public int? Speed { get; init; }
    public int? Intensity { get; init; }
    public int? PaletteId { get; init; }
    public bool? Overlay { get; init; }
    public RgbColor? Primary { get; init; }
    public RgbColor? Secondary { get; init; }
    public RgbColor? Tertiary { get; init; }
    public byte? Brightness { get; init; }
    public bool ExitRealtime { get; init; } = true;

    public static WledPresetRequest FromSlot(EffectSlot slot, byte? brightness = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.WledFxId is not int fxId)
            throw new ArgumentException("A WLED preset effect id is required.", nameof(slot));

        var options = slot.WledOptions;
        var color = slot.Color ?? RgbColor.FromRgb(255, 255, 255);
        return new WledPresetRequest
        {
            FxId = fxId,
            Speed = options?.Speed,
            Intensity = options?.Intensity,
            PaletteId = options?.PaletteId,
            Overlay = options?.Overlay,
            Primary = color,
            Secondary = color,
            Tertiary = RgbColor.FromRgb(255, 255, 255),
            Brightness = brightness,
            ExitRealtime = true
        };
    }
}
