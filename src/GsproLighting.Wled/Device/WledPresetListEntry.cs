namespace GsproLighting.Wled.Device;

/// <summary>Named preset slot from <c>/presets.json</c>.</summary>
public sealed class WledPresetListEntry
{
    public required int Id { get; init; }
    public required string Name { get; init; }

    public override string ToString() => Id <= 0 ? Name : $"{Id}: {Name}";
}
