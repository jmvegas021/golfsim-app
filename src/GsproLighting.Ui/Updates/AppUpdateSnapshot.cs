namespace GsproLighting.Ui.Updates;

public sealed class AppUpdateSnapshot
{
    public required string CurrentVersion { get; init; }
    public UpdatePhase Phase { get; init; }
    public string StatusText { get; init; } = "";
    public string? AvailableVersion { get; init; }
    public bool CanInstall { get; init; }
    public bool IsVelopackInstall { get; init; }
}
