using System.Text.Json;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Logging;

public sealed class FileRawMessageLogger : IRawMessageLogger
{
    private readonly string _directory;
    private readonly bool _logHeartbeats;
    private readonly object _gate = new();

    public FileRawMessageLogger(string directory, bool logHeartbeats = false)
    {
        _directory = directory;
        _logHeartbeats = logHeartbeats;
        Directory.CreateDirectory(_directory);
    }

    public Task LogAsync(RawTrafficMessage message, CancellationToken cancellationToken = default)
    {
        if (!_logHeartbeats && message.Shot?.IsHeartBeat == true)
            return Task.CompletedTask;

        var path = Path.Combine(_directory, $"gspro-raw-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var line = JsonSerializer.Serialize(new
        {
            ts = message.Timestamp,
            dir = message.Direction,
            json = message.RawJson,
            unknown = message.UnknownFields,
            code = message.Response?.Code,
            shotNumber = message.Shot?.ShotNumber,
            hasBall = message.Shot?.HasBallData,
            ballDetected = message.Shot?.IsBallDetected
        });

        lock (_gate)
            File.AppendAllText(path, line + Environment.NewLine);

        return Task.CompletedTask;
    }
}
