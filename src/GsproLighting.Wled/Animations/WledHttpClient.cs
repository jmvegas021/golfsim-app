using System.Net;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;

namespace GsproLighting.Wled.Animations;

public sealed class WledHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly TimeSpan _requestTimeout;

    public WledHttpClient(HttpClient? httpClient = null, TimeSpan? requestTimeout = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5);
    }

    public Task ApplyPresetAsync(
        string controllerIp,
        int fxId,
        CancellationToken cancellationToken = default) =>
        ApplyPresetAsync(controllerIp, new WledPresetRequest { FxId = fxId }, cancellationToken);

    public async Task ApplyPresetAsync(
        string controllerIp,
        WledPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FxId is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request), "WLED effect id must be between 0 and 255.");

        var endpoint = BuildStateEndpoint(controllerIp);
        var body = BuildStateBody(request);
        await PostStateAsync(endpoint, body, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Posts a state body, retrying once on 413 (memory-constrained WLED controllers can
    /// transiently reject even small requests under heap pressure) and once on a genuine
    /// timeout (the controller just didn't respond in time — common over flaky Wi-Fi).
    /// </summary>
    private async Task PostStateAsync(
        Uri endpoint,
        object body,
        CancellationToken cancellationToken,
        bool isRetry = false)
    {
        using var content = WledJsonPostContent.Create(body, out var requestJson);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own timeout fired, not the caller's cancellation (e.g. superseded by a
            // newer shot event) — the controller just didn't answer within _requestTimeout.
            if (!isRetry)
            {
                await PostStateAsync(endpoint, body, cancellationToken, isRetry: true).ConfigureAwait(false);
                return;
            }

            throw new HttpRequestException(
                $"WLED controller at {endpoint.Host} didn't respond within " +
                $"{_requestTimeout.TotalSeconds:0.#}s (twice) — check it's powered on and " +
                "reachable at that IP.",
                inner: null);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                if (!isRetry)
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    await PostStateAsync(endpoint, body, cancellationToken, isRetry: true)
                        .ConfigureAwait(false);
                    return;
                }

                throw new HttpRequestException(
                    "WLED controller is low on memory and rejected the request twice (413). " +
                    "Try again, or power-cycle the controller if this keeps happening.",
                    inner: null,
                    HttpStatusCode.RequestEntityTooLarge);
            }

            await EnsureSuccessWithBodyAsync(
                    response,
                    HttpMethod.Post.Method,
                    endpoint,
                    WledJsonPostContent.MediaType,
                    requestJson,
                    timeout.Token)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Like EnsureSuccessStatusCode, but formats IP/URL/request/response/hint into the exception.
    /// </summary>
    internal static async Task EnsureSuccessWithBodyAsync(
        HttpResponseMessage response,
        string method,
        Uri endpoint,
        string? contentType,
        string requestJson,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort — fall back to empty-body messaging below.
        }

        throw new HttpRequestException(
            WledHttpFailureFormatter.Format(
                method,
                endpoint,
                contentType,
                requestJson,
                (int)response.StatusCode,
                response.ReasonPhrase,
                body),
            inner: null,
            response.StatusCode);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }

    public static object BuildStateBody(WledPresetRequest request)
    {
        var segment = new Dictionary<string, object?> { ["fx"] = request.FxId };
        if (request.Speed is int speed)
            segment["sx"] = ClampByte(speed);
        if (request.Intensity is int intensity)
            segment["ix"] = ClampByte(intensity);
        if (request.PaletteId is int paletteId)
            segment["pal"] = paletteId;
        if (request.Overlay is bool overlay)
            segment["o1"] = overlay;
        if (request.Primary is RgbColor primary)
        {
            segment["col"] = new[]
            {
                ToRgbArray(primary),
                ToRgbArray(request.Secondary ?? primary),
                ToRgbArray(request.Tertiary ?? RgbColor.FromRgb(255, 255, 255))
            };
        }

        var body = new Dictionary<string, object?>
        {
            ["on"] = true,
            ["seg"] = new[] { segment }
        };
        if (request.ExitRealtime)
            body["live"] = false;
        if (request.Brightness is byte brightness)
            body["bri"] = brightness;
        return body;
    }

    private static int[] ToRgbArray(RgbColor color) => [color.R, color.G, color.B];

    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);


    private static Uri BuildStateEndpoint(string controllerIp)
    {
        var host = controllerIp?.Trim();
        if (string.IsNullOrEmpty(host) ||
            Uri.CheckHostName(host) == UriHostNameType.Unknown &&
            !IPAddress.TryParse(host, out _))
            throw new ArgumentException("A valid WLED IP address or host name is required.", nameof(controllerIp));

        return new UriBuilder(Uri.UriSchemeHttp, host)
        {
            Path = "/json/state"
        }.Uri;
    }
}
