namespace GsproLighting.Core.Models;

/// <summary>
/// GSPro → launch-monitor response (documented 200/201/5xx; spike may find more).
/// </summary>
public sealed class GsproResponse
{
    public int? Code { get; set; }
    public string? Message { get; set; }
    public PlayerInfo? Player { get; set; }

    /// <summary>
    /// Any extra JSON properties not mapped above — used by the spike to surface
    /// undocumented outcome fields (made putt, water, OB, etc.).
    /// </summary>
    public Dictionary<string, object?> Extensions { get; set; } = new();
}
