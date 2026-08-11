namespace GsproLighting.Core.Config;

/// <summary>
/// Moderate WLED-facing tweaks for Ready / Not Ready / Direction / Waiting.
/// Colors stay product-authored; these only scale speed, intensity, layers, and
/// (for concentrate states) band size.
/// </summary>
public sealed class StatusEffectTuning
{
    public StatusEffectStateTuning Ready { get; set; } = StatusEffectStateTuning.CreateDefaults();
    public StatusEffectStateTuning NotReady { get; set; } = StatusEffectStateTuning.CreateDefaults();
    public StatusEffectStateTuning Direction { get; set; } = StatusEffectStateTuning.CreateDefaults();
    public StatusEffectStateTuning Waiting { get; set; } = StatusEffectStateTuning.CreateDefaults();

    public StatusEffectTuning Clone() => new()
    {
        Ready = Ready.Clone(),
        NotReady = NotReady.Clone(),
        Direction = Direction.Clone(),
        Waiting = Waiting.Clone()
    };

    public void ClampAll()
    {
        Ready ??= StatusEffectStateTuning.CreateDefaults();
        NotReady ??= StatusEffectStateTuning.CreateDefaults();
        Direction ??= StatusEffectStateTuning.CreateDefaults();
        Waiting ??= StatusEffectStateTuning.CreateDefaults();

        Ready.Clamp(includeBandSize: true);
        NotReady.Clamp(includeBandSize: false);
        Direction.Clamp(includeBandSize: true);
        Waiting.Clamp(includeBandSize: false);
    }
}
