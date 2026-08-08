namespace GsproLighting.Core.Preview;

/// <summary>Play-all progress reported to the Preview tab status label.</summary>
public sealed class PreviewSequenceProgress
{
    public required int Index { get; init; }
    public required int Total { get; init; }
    public required string StateTitle { get; init; }
    public bool IsComplete { get; init; }

    public string FormatLabel() =>
        IsComplete
            ? "Play all complete · last state holding"
            : $"Play all · {Index}/{Total} · {StateTitle}";
}
