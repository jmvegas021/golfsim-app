using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

public sealed class NullWledOutput : IWledOutput
{
    public void Configure(WledConfig config)
    {
    }

    public Task SendSolidAsync(
        RgbColor color,
        byte? brightness = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendPixelsAsync(
        IReadOnlyList<RgbColor> pixels,
        byte? brightness = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
