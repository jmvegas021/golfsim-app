using GsproLighting.Core.Config;

namespace GsproLighting.Core.Preview;

/// <summary>
/// Builds the Preview-tab state list from in-memory <see cref="EffectConfig"/> defaults.
/// </summary>
public sealed class LightingPreviewCatalog
{
    public IReadOnlyList<LightingPreviewItem> Create(EffectConfig effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        return
        [
            Item(
                LightingPreviewIds.Waiting,
                "Waiting / unknown",
                "WLED Ripple ambient (Red Reef, layered, timing 15%) while Connect state is unknown",
                effects.Waiting,
                holdBrightnessFactor: 0.33,
                holdAsSolid: false),
            Item(
                LightingPreviewIds.NotReady,
                "Not ready",
                "Red outside→center, then hold dim red",
                effects.NotReady,
                holdBrightnessFactor: 0.33),
            Item(
                LightingPreviewIds.Ready,
                "Ready / idle",
                "WLED Ripple basic ambient (Red Reef, layered, colors max, timing 15%)",
                effects.Idle,
                holdAsSolid: false),
            Item(
                LightingPreviewIds.Pure,
                "Pure strike",
                "Green direction marker — use Left / Center / Right",
                effects.PureStrike,
                supportsDirection: true,
                holdAsSolid: false),
            Item(
                LightingPreviewIds.Mishit,
                "Mishit",
                "Dull red direction marker — use Left / Center / Right",
                effects.Mishit,
                supportsDirection: true,
                holdAsSolid: false),
            Item(
                LightingPreviewIds.Putt,
                "Putt",
                "Blue center marker, then hold",
                effects.Putt,
                holdAsSolid: false),
            Item(
                LightingPreviewIds.Player,
                "Player / club",
                "Blue pulse, then hold blue",
                effects.Player),
            Item(
                LightingPreviewIds.Celebrate,
                "Celebrate",
                "Gold fireworks (WLED preset) with on-screen sparkle approx",
                effects.Celebrate),
            Item(
                LightingPreviewIds.Hazard,
                "Hazard",
                "Red sparkle (WLED preset)",
                effects.Hazard),
            Item(
                LightingPreviewIds.Water,
                "Water hazard",
                "Teal flash, then hold",
                effects.WaterHazard),
            Item(
                LightingPreviewIds.OutOfBounds,
                "OB",
                "Hard red short flash, then hold",
                effects.OutOfBounds),
            Item(
                LightingPreviewIds.TestSweep,
                "Test sweep",
                "Full-strip sweep using pure-strike color",
                EffectSlot.Curated(effects.PureStrike.Color, EffectAnimations.Sweep))
        ];
    }

    private static LightingPreviewItem Item(
        string id,
        string title,
        string description,
        EffectSlot slot,
        bool supportsDirection = false,
        double holdBrightnessFactor = 1,
        bool holdAsSolid = true) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Slot = slot.Clone(),
            SupportsDirection = supportsDirection,
            HoldBrightnessFactor = holdBrightnessFactor,
            HoldAsSolid = holdAsSolid
        };
}
