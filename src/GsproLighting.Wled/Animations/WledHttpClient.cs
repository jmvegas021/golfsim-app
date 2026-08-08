using System.Net;
using System.Net.Http.Json;
using GsproLighting.Core.Config;

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
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3);
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
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(BuildStateBody(request))
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
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
