using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// One-shot HTTP solid-color (FX 0) / off posts to WLED <c>/json/state</c>.
/// Skeleton path: no DRGB, no keepalive, no ambient Ripple.
/// </summary>
public sealed class WledSolidHttpApplier : IDisposable
{
    private readonly WledDeviceClient _client;
    private readonly bool _ownsClient;

    public WledSolidHttpApplier(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient();
        _ownsClient = client is null;
    }

    /// <summary>Solid FX 0 with RGB, <c>live:false</c> so HTTP owns the strip.</summary>
    public static WledStatePatch CreateSolidPatch(
        RgbColor color,
        byte brightness,
        int segmentId = 0)
    {
        ArgumentNullException.ThrowIfNull(color);
        return new WledStatePatch
        {
            On = true,
            Brightness = brightness,
            Live = false,
            SegmentId = segmentId,
            FxId = 0,
            Primary = color,
            Secondary = RgbColor.FromRgb(0, 0, 0),
            Tertiary = RgbColor.FromRgb(0, 0, 0)
        };
    }

    /// <summary>Powers the strip off and clears realtime ownership.</summary>
    public static WledStatePatch CreateOffPatch() =>
        new() { On = false, Live = false };

    public Task ApplySolidAsync(
        string controllerIp,
        RgbColor color,
        byte brightness,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(color);
        return _client.ApplyStateAsync(
            controllerIp,
            CreateSolidPatch(color, brightness),
            cancellationToken);
    }

    public Task ApplyOffAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        _client.ApplyStateAsync(controllerIp, CreateOffPatch(), cancellationToken);

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
