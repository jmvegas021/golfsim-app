using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Wled;

/// <summary>
/// Loads WLED catalogs/state and applies control-surface patches.
/// Golf DDP reactions remain separate; ambient apply sets <c>live:false</c>.
/// </summary>
public sealed class WledTabManager : IDisposable
{
    private readonly WledDeviceClient _client;
    private readonly bool _ownsClient;

    public WledTabManager(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient();
        _ownsClient = client is null;
    }

    public IReadOnlyList<WledNamedEntry> Effects { get; private set; } = [];
    public IReadOnlyList<WledNamedEntry> Palettes { get; private set; } = [];
    public IReadOnlyList<WledPresetListEntry> Presets { get; private set; } = [];
    public WledDeviceInfo? Info { get; private set; }
    public WledDeviceState? State { get; private set; }
    public WledDeviceState? Snapshot { get; private set; }
    public IReadOnlyList<string> CatalogWarnings { get; private set; } = [];

    public async Task RefreshAsync(string controllerIp, CancellationToken cancellationToken = default)
    {
        var ip = RequireIp(controllerIp);
        var effectsTask = _client.GetEffectsAsync(ip, cancellationToken);
        var palettesTask = _client.GetPalettesAsync(ip, cancellationToken);
        var stateTask = _client.GetStateAsync(ip, cancellationToken);
        var infoTask = _client.GetInfoAsync(ip, cancellationToken);
        var presetsTask = _client.GetPresetsAsync(ip, cancellationToken);

        await Task.WhenAll(effectsTask, palettesTask, stateTask, infoTask, presetsTask)
            .ConfigureAwait(false);

        Effects = await effectsTask.ConfigureAwait(false);
        Palettes = await palettesTask.ConfigureAwait(false);
        State = await stateTask.ConfigureAwait(false);
        Info = await infoTask.ConfigureAwait(false);
        Presets = await presetsTask.ConfigureAwait(false);
        Snapshot = State.Clone();
        CatalogWarnings = WledCatalogValidator.ValidateConfiguredIds(Effects, Palettes);
    }

    public Task ApplyAsync(
        string controllerIp,
        WledStatePatch patch,
        CancellationToken cancellationToken = default) =>
        _client.ApplyStateAsync(RequireIp(controllerIp), patch, cancellationToken);

    public Task ApplySavedPresetAsync(
        string controllerIp,
        int presetId,
        CancellationToken cancellationToken = default) =>
        _client.ApplySavedPresetAsync(RequireIp(controllerIp), presetId, cancellationToken);

    public Task ApplyAmbientAsync(
        string controllerIp,
        byte? brightness = null,
        int segmentId = 0,
        CancellationToken cancellationToken = default) =>
        _client.ApplyAmbientRippleAsync(
            RequireIp(controllerIp),
            brightness,
            segmentId,
            cancellationToken);

    public Task RevertAsync(string controllerIp, CancellationToken cancellationToken = default)
    {
        if (Snapshot is null)
            return Task.CompletedTask;

        var seg = Snapshot.MainSegment;
        var patch = new WledStatePatch
        {
            On = Snapshot.On,
            Brightness = Snapshot.Brightness,
            Live = false,
            SegmentId = seg.Id,
            FxId = seg.FxId,
            Speed = seg.Speed,
            Intensity = seg.Intensity,
            PaletteId = seg.PaletteId,
            Overlay = seg.Overlay,
            Option2 = seg.Option2,
            Option3 = seg.Option3,
            Primary = seg.Primary,
            Secondary = seg.Secondary,
            Tertiary = seg.Tertiary
        };
        return ApplyAsync(controllerIp, patch, cancellationToken);
    }

    public static string BuildOpenWledUrl(string controllerIp) =>
        WledDeviceClient.BuildWebUiUri(RequireIp(controllerIp)).ToString();

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private static string RequireIp(string controllerIp)
    {
        var ip = controllerIp?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(ip) || ip is "0.0.0.0")
            throw new InvalidOperationException("Set a WLED controller IP on the Connection tab first.");
        return ip;
    }
}
