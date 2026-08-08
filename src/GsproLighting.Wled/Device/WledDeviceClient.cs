using System.Net;
using System.Net.Http.Json;
using System.Text;
using GsproLighting.Wled.Animations;

namespace GsproLighting.Wled.Device;

/// <summary>
/// HTTP JSON client for WLED catalogs and state. Safe to call alongside DRGB shot reactions;
/// after curated realtime holds, apply ambient with <c>live:false</c> to hand control back.
/// </summary>
public sealed class WledDeviceClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly TimeSpan _requestTimeout;

    public WledDeviceClient(HttpClient? httpClient = null, TimeSpan? requestTimeout = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(4);
    }

    public Task<IReadOnlyList<WledNamedEntry>> GetEffectsAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        GetCatalogAsync(controllerIp, "/json/eff", cancellationToken);

    public Task<IReadOnlyList<WledNamedEntry>> GetPalettesAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        GetCatalogAsync(controllerIp, "/json/pal", cancellationToken);

    public async Task<WledDeviceState> GetStateAsync(
        string controllerIp,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(controllerIp, "/json/state", cancellationToken)
            .ConfigureAwait(false);
        return WledJsonParsers.ParseState(json);
    }

    public async Task<WledDeviceInfo> GetInfoAsync(
        string controllerIp,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(controllerIp, "/json/info", cancellationToken)
            .ConfigureAwait(false);
        return WledJsonParsers.ParseInfo(json);
    }

    public async Task<IReadOnlyList<WledPresetListEntry>> GetPresetsAsync(
        string controllerIp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await GetStringAsync(controllerIp, "/presets.json", cancellationToken)
                .ConfigureAwait(false);
            return WledJsonParsers.ParsePresets(json);
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
    }

    public async Task ApplyStateAsync(
        string controllerIp,
        WledStatePatch patch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        await PostJsonAsync(
                controllerIp,
                "/json/state",
                WledJsonParsers.BuildPatchBody(patch),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task ApplyPresetRequestAsync(
        string controllerIp,
        WledPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync(
            controllerIp,
            "/json/state",
            WledHttpClient.BuildStateBody(request),
            cancellationToken);
    }

    public Task ApplySavedPresetAsync(
        string controllerIp,
        int presetId,
        CancellationToken cancellationToken = default) =>
        ApplyStateAsync(
            controllerIp,
            new WledStatePatch { On = true, Live = false, PresetId = presetId },
            cancellationToken);

    public Task ApplyAmbientRippleAsync(
        string controllerIp,
        byte? brightness = null,
        int segmentId = 0,
        CancellationToken cancellationToken = default) =>
        ApplyStateAsync(
            controllerIp,
            WledAmbientStateFactory.CreateRippleAmbientPatch(segmentId, brightness: brightness),
            cancellationToken);

    public static Uri BuildWebUiUri(string controllerIp)
    {
        var host = RequireHost(controllerIp);
        return new UriBuilder(Uri.UriSchemeHttp, host).Uri;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }

    private async Task<IReadOnlyList<WledNamedEntry>> GetCatalogAsync(
        string controllerIp,
        string path,
        CancellationToken cancellationToken)
    {
        var json = await GetStringAsync(controllerIp, path, cancellationToken).ConfigureAwait(false);
        return WledJsonParsers.ParseNameArray(json);
    }

    private async Task<string> GetStringAsync(
        string controllerIp,
        string path,
        CancellationToken cancellationToken)
    {
        var endpoint = BuildEndpoint(controllerIp, path);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
    }

    private Task PostJsonAsync(
        string controllerIp,
        string path,
        object body,
        CancellationToken cancellationToken) =>
        PostJsonAsync(controllerIp, path, body, cancellationToken, isRetry: false);

    /// <summary>
    /// Posts a JSON body, retrying once on 413 — memory-constrained WLED controllers
    /// (e.g. ESP8266 boards) can transiently reject even small requests under heap pressure.
    /// </summary>
    private async Task PostJsonAsync(
        string controllerIp,
        string path,
        object body,
        CancellationToken cancellationToken,
        bool isRetry)
    {
        var endpoint = BuildEndpoint(controllerIp, path);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body)
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            if (!isRetry)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                await PostJsonAsync(controllerIp, path, body, cancellationToken, isRetry: true)
                    .ConfigureAwait(false);
                return;
            }

            throw new HttpRequestException(
                "WLED controller is low on memory and rejected the request twice (413). " +
                "Try again, or power-cycle the controller if this keeps happening.",
                inner: null,
                HttpStatusCode.RequestEntityTooLarge);
        }

        response.EnsureSuccessStatusCode();
    }

    private static Uri BuildEndpoint(string controllerIp, string path)
    {
        var host = RequireHost(controllerIp);
        return new UriBuilder(Uri.UriSchemeHttp, host) { Path = path }.Uri;
    }

    private static string RequireHost(string controllerIp)
    {
        var host = controllerIp?.Trim();
        if (string.IsNullOrEmpty(host) ||
            Uri.CheckHostName(host) == UriHostNameType.Unknown &&
            !IPAddress.TryParse(host, out _))
            throw new ArgumentException("A valid WLED IP address or host name is required.", nameof(controllerIp));
        return host;
    }
}
