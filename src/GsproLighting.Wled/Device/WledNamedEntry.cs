namespace GsproLighting.Wled.Device;

/// <summary>Indexed WLED catalog entry (effect or palette name).</summary>
public sealed class WledNamedEntry
{
    public required int Id { get; init; }
    public required string Name { get; init; }

    public override string ToString() => $"{Id}: {Name}";
}
