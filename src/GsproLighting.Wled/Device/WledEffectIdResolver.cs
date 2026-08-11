using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Resolves stock WLED effect ids from a live <c>/json/eff</c> catalog when available.
/// </summary>
public static class WledEffectIdResolver
{
    public const string RippleEffectName = "Ripple";

    /// <summary>
    /// Prefers an exact catalog name match for Ripple; falls back to
    /// <see cref="EffectConfig.RippleFxId"/> (stock WLED id 79).
    /// </summary>
    public static int ResolveRippleFxId(IReadOnlyList<WledNamedEntry>? effects)
    {
        if (effects is null || effects.Count == 0)
            return EffectConfig.RippleFxId;

        foreach (var entry in effects)
        {
            if (string.Equals(entry.Name, RippleEffectName, StringComparison.OrdinalIgnoreCase))
                return entry.Id;
        }

        foreach (var entry in effects)
        {
            if (entry.Name.Contains(RippleEffectName, StringComparison.OrdinalIgnoreCase) &&
                !entry.Name.Contains("rainbow", StringComparison.OrdinalIgnoreCase))
                return entry.Id;
        }

        return EffectConfig.RippleFxId;
    }
}
