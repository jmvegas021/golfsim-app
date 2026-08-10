using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// One-shot HTTP solid-color (FX 0) / off posts to WLED <c>/json/state</c>.
/// Uses authoritative full-strip bodies so prior WLED UI state cannot linger.
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

    /// <summary>Authoritative solid FX 0 covering the full strip.</summary>
    public static object CreateSolidBody(int ledCount, RgbColor color, byte brightness) =>
        WledAuthoritativeStateFactory.CreateSolidBody(ledCount, color, brightness);

    /// <summary>Powers the strip off and stops presets/playlists / realtime ownership.</summary>
    public static object CreateOffBody() =>
        WledAuthoritativeStateFactory.CreateOffBody();

    public Task ApplySolidAsync(
        string controllerIp,
        RgbColor color,
        byte brightness,
        int ledCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(color);
        return _client.ApplyStateBodyAsync(
            controllerIp,
            CreateSolidBody(ledCount, color, brightness),
            cancellationToken);
    }

    public Task ApplyOffAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        _client.ApplyStateBodyAsync(controllerIp, CreateOffBody(), cancellationToken);

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
