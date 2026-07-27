using GsproLighting.Core.Models;

namespace GsproLighting.Core.Contracts;

public interface IRawMessageLogger
{
    Task LogAsync(RawTrafficMessage message, CancellationToken cancellationToken = default);
}
