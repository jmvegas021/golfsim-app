using System.Net;
using System.Net.Http.Headers;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledJsonPostContentTests
{
    [Fact]
    public void Create_UsesApplicationJsonWithoutCharset()
    {
        using var content = WledJsonPostContent.Create(new Dictionary<string, object?> { ["on"] = true }, out var json);

        Assert.Equal("""{"on":true}""", json);
        Assert.Equal("application/json", content.Headers.ContentType?.MediaType);
        Assert.Null(content.Headers.ContentType?.CharSet);
    }

    [Fact]
    public async Task ApplyPresetAsync_SendsApplicationJsonWithoutCharset()
    {
        MediaTypeHeaderValue? contentType = null;
        var handler = new CaptureHandler(ct => contentType = ct);
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        await http.ApplyPresetAsync("192.168.86.89", new WledPresetRequest { FxId = 79 });

        Assert.NotNull(contentType);
        Assert.Equal("application/json", contentType!.MediaType);
        Assert.True(
            string.IsNullOrEmpty(contentType.CharSet),
            $"Expected no charset, got '{contentType.CharSet}'");
    }

    [Fact]
    public async Task ApplyStateAsync_SendsApplicationJsonWithoutCharset()
    {
        MediaTypeHeaderValue? contentType = null;
        var handler = new CaptureHandler(ct => contentType = ct);
        using var client = new WledDeviceClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        await client.ApplyStateAsync("192.168.86.89", new WledStatePatch { On = true, Live = false });

        Assert.NotNull(contentType);
        Assert.Equal("application/json", contentType!.MediaType);
        Assert.True(string.IsNullOrEmpty(contentType.CharSet));
    }

    [Fact]
    public async Task ApplyPresetAsync_Empty400_IncludesHostAndSentBody()
    {
        var handler = new CaptureHandler(_ => { }, status: HttpStatusCode.BadRequest, responseBody: "");
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => http.ApplyPresetAsync("192.168.86.89", new WledPresetRequest { FxId = 79 }));

        Assert.Contains("192.168.86.89", ex.Message, StringComparison.Ordinal);
        Assert.Contains("empty response body", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"fx\":79", ex.Message, StringComparison.Ordinal);
        Assert.Contains("hint=", ex.Message, StringComparison.Ordinal);
        Assert.Contains("content-type=application/json", ex.Message, StringComparison.Ordinal);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Action<MediaTypeHeaderValue?> _capture;
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public CaptureHandler(
            Action<MediaTypeHeaderValue?> capture,
            HttpStatusCode status = HttpStatusCode.OK,
            string responseBody = "{}")
        {
            _capture = capture;
            _status = status;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _capture(request.Content?.Headers.ContentType);
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody)
            });
        }
    }
}
