using System.Net;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Tests;

internal sealed class RecordingWledOutput : IWledOutput
{
    private readonly object _gate = new();
    private readonly List<IReadOnlyList<RgbColor>> _pixelFrames = [];

    public int FrameCount
    {
        get
        {
            lock (_gate)
                return _pixelFrames.Count;
        }
    }

    public IReadOnlyList<RgbColor>[] SnapshotFrames()
    {
        lock (_gate)
            return _pixelFrames.ToArray();
    }

    public void Configure(WledConfig config)
    {
    }

    public Task SendSolidAsync(
        RgbColor color,
        byte? brightness = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendPixelsAsync(
        IReadOnlyList<RgbColor> pixels,
        byte? brightness = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _pixelFrames.Add(pixels.ToArray());
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class RecordingHttpHandler : HttpMessageHandler
{
    private readonly TaskCompletionSource _twoPosts =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int PostCount { get; private set; }
    public int GetCount { get; private set; }
    public string LastBody { get; private set; } = "";
    public List<string> Bodies { get; } = [];

    public Task WaitForPostsAsync(int count)
    {
        if (PostCount >= count)
            return Task.CompletedTask;
        return _twoPosts.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            GetCount++;
            var path = request.RequestUri?.AbsolutePath ?? "";
            var payload = path.Contains("/json/eff", StringComparison.OrdinalIgnoreCase)
                ? """["Solid","Blink","Ripple","Rainbow"]"""
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            };
        }

        PostCount++;
        LastBody = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Bodies.Add(LastBody);
        if (PostCount >= 2)
            _twoPosts.TrySetResult();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };
    }
}

internal sealed class FailingHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new HttpRequestException("Simulated WLED controller unreachable");
}
