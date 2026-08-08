using System.Net;
using System.Net.Http.Json;

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

    public async Task ApplyPresetAsync(
        string controllerIp,
        int fxId,
        CancellationToken cancellationToken = default)
    {
        if (fxId is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(fxId), "WLED effect id must be between 0 and 255.");

        var endpoint = BuildStateEndpoint(controllerIp);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                on = true,
                seg = new[] { new { fx = fxId } }
            })
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }

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
