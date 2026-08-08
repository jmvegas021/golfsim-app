using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds the product basic ambient patch: Ripple + Red Reef + layered + max colors + 15% timing.
/// </summary>
public static class WledAmbientStateFactory
{
    public static WledPresetRequest CreateRippleAmbientRequest(
        RgbColor? color = null,
        byte? brightness = null)
    {
        var slot = EffectConfig.CreateRippleAmbient(
            color ?? RgbColor.FromRgb(61, 220, 132));
        return WledPresetRequest.FromSlot(slot, brightness);
    }

    public static WledStatePatch CreateRippleAmbientPatch(
        int segmentId = 0,
        RgbColor? color = null,
        byte? brightness = null)
    {
        var request = CreateRippleAmbientRequest(color, brightness);
        return new WledStatePatch
        {
            On = true,
            Brightness = brightness ?? request.Brightness,
            Live = false,
            SegmentId = segmentId,
            FxId = request.FxId,
            Speed = request.Speed,
            Intensity = request.Intensity,
            PaletteId = request.PaletteId,
            Overlay = request.Overlay,
            Primary = request.Primary,
            Secondary = request.Secondary,
            Tertiary = request.Tertiary
        };
    }
}
