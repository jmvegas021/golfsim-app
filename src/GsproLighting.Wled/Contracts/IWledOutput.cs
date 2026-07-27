using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Contracts;

public interface IWledOutput : IAsyncDisposable
{
    void Configure(WledConfig config);
    Task SendSolidAsync(RgbColor color, byte? brightness = null, CancellationToken cancellationToken = default);
    Task SendPixelsAsync(IReadOnlyList<RgbColor> pixels, byte? brightness = null, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
