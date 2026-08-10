namespace GsproLighting.Wled.Device;

/// <summary>
/// LED count + brightness snapshot pulled from a live controller for Connection settings —
/// these belong to the device firmware, not hand-edited app fields.
/// </summary>
public sealed record WledConnectionSnapshot(
    int LedCount,
    byte Brightness,
    string DeviceName,
    string Version);

/// <summary>Loads <see cref="WledConnectionSnapshot"/> from <c>/json/info</c> + <c>/json/state</c>.</summary>
public sealed class WledConnectionSnapshotReader : IDisposable
{
    private readonly WledDeviceClient _client;
    private readonly bool _ownsClient;

    public WledConnectionSnapshotReader(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient();
        _ownsClient = client is null;
    }

    public async Task<WledConnectionSnapshot> ReadAsync(
        string controllerIp,
        CancellationToken cancellationToken = default)
    {
        var infoTask = _client.GetInfoAsync(controllerIp, cancellationToken);
        var stateTask = _client.GetStateAsync(controllerIp, cancellationToken);
        await Task.WhenAll(infoTask, stateTask).ConfigureAwait(false);

        var info = await infoTask.ConfigureAwait(false);
        var state = await stateTask.ConfigureAwait(false);
        var ledCount = Math.Max(1, info.LedCount);
        // Floor at 1 so DDP golf flashes aren't silently black if the strip is powered down.
        var brightness = state.Brightness == 0 ? (byte)1 : state.Brightness;
        return new WledConnectionSnapshot(ledCount, brightness, info.Name, info.Version);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
