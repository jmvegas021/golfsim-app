using System.Text.Json;
using System.Text.RegularExpressions;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Parses Garmin Connect / GSPro Connect log lines into shots, ready events, or sparse raw lines.
/// Tuned for GarminR50Form: readyForShot + "Logging ball data IMMEDIATELY" ball JSON
/// (including multiline / pretty-printed payloads).
/// Ready is edge-triggered — Connect re-logs readyForShot as keepalive.
/// </summary>
public sealed class ConnectLogLineParser
{
    private static readonly Regex JsonBlob = new(
        @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
        RegexOptions.Compiled);

    private static readonly string[] BallLogMarkers =
    {
        "Logging ball data IMMEDIATELY",
        "Logging ball data",
        "before sending to GSPro",
        "sending to GSPro"
    };

    private static readonly string[] ReadyTokens =
    {
        "readyForShot",
        "READY_TO_HIT",
        "LaunchMonitorBallDetected",
        "ball detected",
        "BallDetected"
    };

    private static readonly string[] NotReadyTokens =
    {
        "NOT_READY_TO_HIT",
        "notReadyToHit",
        "not ready to hit"
    };

    private static readonly string[] HighSignalRawTokens =
    {
        "error",
        "exception",
        "disconnected",
        "disconnect",
        "failed",
        "timeout",
        "looking for garmin",
        "paired",
        "connected to"
    };

    private static readonly string[] NoiseTokens =
    {
        "unityengine.",
        "fallback handler",
        "dontdestroyonload",
        "shader",
        "mesh.",
        "texture.",
        "audio.",
        "input.",
        "render",
        "fps:",
        "gc.alloc"
    };

    private readonly GsproMessageParser _jsonParser = new();
    private readonly GarminConnectBallMapper _garminMapper = new();
    private readonly MultilineJsonAccumulator _jsonBuffer = new();
    private bool _awaitingBallJson;
    private string? _ballContextLine;
    private int _awaitingIdleLines;
    /// <summary>
    /// Edge-trigger gate: Connect re-logs readyForShot / READY_TO_HIT as keepalive.
    /// Only the transition into ready should emit a Ready event.
    /// </summary>
    private bool _isReady;
    private string? _lastEmittedReadyLine;

    public ConnectParseResult Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return ConnectParseResult.Ignore;

        var trimmed = line.Trim();

        // Multiline JSON path — skip noise filter so "{" / "}" lines are kept.
        if (_jsonBuffer.IsBuffering || _awaitingBallJson)
        {
            if (ContainsAny(trimmed, BallLogMarkers))
                return HandleBallMarkerLine(trimmed);

            var buffered = TryFinishBufferedJson(trimmed);
            if (buffered is not null)
                return buffered;

            if (_jsonBuffer.IsBuffering)
                return ConnectParseResult.Ignore;

            _awaitingIdleLines++;
            if (_awaitingIdleLines > 80)
                ClearBallWait();
            // Fall through for ready / metrics on non-buffer lines while still awaiting.
        }

        if (IsNoise(trimmed))
            return ConnectParseResult.Ignore;

        if (IsNotReadyLine(trimmed))
        {
            ClearReadyState();
            ClearBallWait();
            return ConnectParseResult.Ignore;
        }

        if (ContainsAny(trimmed, BallLogMarkers))
            return HandleBallMarkerLine(trimmed);

        var shot = TryExtractShot(trimmed, trimmed);
        if (shot is not null)
            return EmitShot(shot, trimmed);

        if (GarminConnectBallMapper.LooksLikeBallMetrics(trimmed))
        {
            if (trimmed.Contains('{') && !trimmed.Contains('}'))
            {
                _awaitingBallJson = true;
                _ballContextLine = trimmed;
                _awaitingIdleLines = 0;
                var partial = _jsonBuffer.AppendFromFirstBrace(trimmed);
                if (partial is not null)
                {
                    var fromPartial = MapCompleteJson(partial, trimmed);
                    if (fromPartial is not null)
                        return EmitShot(fromPartial, trimmed);
                }

                return ConnectParseResult.ForRaw(trimmed);
            }

            return ConnectParseResult.ForRaw(trimmed);
        }

        if (IsReadyLine(trimmed))
            return EmitReadyEdge(trimmed);

        if (ContainsAny(trimmed, HighSignalRawTokens) &&
            (trimmed.Contains("garmin", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("connect", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("r50", StringComparison.OrdinalIgnoreCase)))
            return ConnectParseResult.ForRaw(trimmed);

        return ConnectParseResult.Ignore;
    }

    public static bool IsInteresting(string line) =>
        !string.IsNullOrWhiteSpace(line) &&
        !IsNoise(line) &&
        (ContainsAny(line, BallLogMarkers) ||
         ContainsAny(line, ReadyTokens) ||
         GarminConnectBallMapper.LooksLikeBallMetrics(line) ||
         ContainsAny(line, HighSignalRawTokens));

    private ConnectParseResult HandleBallMarkerLine(string trimmed)
    {
        _awaitingBallJson = true;
        _ballContextLine = trimmed;
        _awaitingIdleLines = 0;
        _jsonBuffer.Reset();

        var sameLineShot = TryExtractShot(trimmed, trimmed);
        if (sameLineShot is not null)
            return EmitShot(sameLineShot, trimmed);

        if (trimmed.Contains('{'))
        {
            var complete = _jsonBuffer.AppendFromFirstBrace(trimmed);
            if (complete is not null)
            {
                var shot = MapCompleteJson(complete, trimmed);
                if (shot is not null)
                    return EmitShot(shot, trimmed);
            }
        }

        // Restore v0.3.1 visibility: show the marker even before JSON completes.
        return ConnectParseResult.ForRaw(trimmed);
    }

    private ConnectParseResult? TryFinishBufferedJson(string trimmed)
    {
        string? complete = null;

        if (_jsonBuffer.IsBuffering)
            complete = _jsonBuffer.Append(trimmed);
        else if (trimmed.Contains('{'))
            complete = _jsonBuffer.AppendFromFirstBrace(trimmed);
        else
        {
            var pendingShot = TryExtractShot(trimmed, _ballContextLine);
            if (pendingShot is not null)
                return EmitShot(pendingShot, trimmed);

            return null;
        }

        if (complete is null)
            return null;

        var shot = MapCompleteJson(complete, _ballContextLine ?? trimmed);
        if (shot is not null)
            return EmitShot(shot, trimmed);

        ClearBallWait();
        if (GarminConnectBallMapper.LooksLikeBallMetrics(complete))
            return ConnectParseResult.ForRaw(complete);

        return null;
    }

    private ShotPayload? MapCompleteJson(string json, string? context)
    {
        if (!GarminConnectBallMapper.LooksLikeBallMetrics(json) &&
            !json.Contains("BallData", StringComparison.OrdinalIgnoreCase) &&
            !json.Contains("ShotNumber", StringComparison.OrdinalIgnoreCase))
            return null;

        var garminShot = _garminMapper.TryMapJson(json, context);
        if (garminShot is not null)
        {
            EnsureShotOptions(garminShot);
            return garminShot;
        }

        try
        {
            var traffic = _jsonParser.Parse("ConnectLog", json);
            if (traffic.Shot is { IsHeartBeat: false } openConnect &&
                (openConnect.HasBallData || openConnect.BallData?.Speed is > 0 || openConnect.ShotNumber is > 0))
            {
                EnsureShotOptions(openConnect);
                ConnectLogKeyValueMapper.ApplyPuttingHints(openConnect, context ?? json);
                return openConnect;
            }
        }
        catch (JsonException)
        {
            // Fall through.
        }

        return ConnectLogKeyValueMapper.TryMap(json, context);
    }

    private ShotPayload? TryExtractShot(string line, string? context)
    {
        foreach (Match match in JsonBlob.Matches(line))
        {
            var mapped = MapCompleteJson(match.Value, context ?? line);
            if (mapped is not null)
                return mapped;
        }

        if (line.Contains('{') && line.Contains('}') &&
            GarminConnectBallMapper.LooksLikeBallMetrics(line))
        {
            var start = line.IndexOf('{');
            var end = line.LastIndexOf('}');
            if (end > start)
            {
                var mapped = MapCompleteJson(line[start..(end + 1)], context ?? line);
                if (mapped is not null)
                    return mapped;
            }
        }

        return ConnectLogKeyValueMapper.TryMap(line, context);
    }

    private ConnectParseResult EmitShot(ShotPayload shot, string raw)
    {
        // After a shot the monitor leaves ready; next readyForShot should fire once.
        ClearReadyState();
        ClearBallWait();
        return ConnectParseResult.ForShot(shot, raw);
    }

    private ConnectParseResult EmitReadyEdge(string trimmed)
    {
        ClearBallWait();

        // Identical keepalive line while already ready — ignore.
        if (_isReady &&
            _lastEmittedReadyLine is not null &&
            string.Equals(_lastEmittedReadyLine, trimmed, StringComparison.Ordinal))
            return ConnectParseResult.Ignore;

        if (_isReady)
            return ConnectParseResult.Ignore;

        _isReady = true;
        _lastEmittedReadyLine = trimmed;
        return ConnectParseResult.ForReady(CreateReadyPayload(), trimmed);
    }

    private void ClearReadyState()
    {
        _isReady = false;
        _lastEmittedReadyLine = null;
    }

    private void ClearBallWait()
    {
        _awaitingBallJson = false;
        _ballContextLine = null;
        _awaitingIdleLines = 0;
        _jsonBuffer.Reset();
    }

    private static bool IsNotReadyLine(string line)
    {
        if (ContainsAny(line, NotReadyTokens))
            return true;

        // readyForShot=false / readyForShot: false — clear, do not treat as Ready.
        if (!line.Contains("readyForShot", StringComparison.OrdinalIgnoreCase))
            return false;

        return line.Contains("readyForShot=false", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("readyForShot = false", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("readyForShot:false", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("readyForShot: false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadyLine(string line)
    {
        if (IsNotReadyLine(line))
            return false;

        if (ContainsAny(line, ReadyTokens))
            return true;

        return line.Contains("ballPlacement", StringComparison.OrdinalIgnoreCase) &&
               line.Contains("ready", StringComparison.OrdinalIgnoreCase);
    }

    private static ShotPayload CreateReadyPayload() => new()
    {
        DeviceId = "GarminR50",
        ShotDataOptions = new ShotDataOptions
        {
            LaunchMonitorBallDetected = true,
            LaunchMonitorIsReady = true,
            IsHeartBeat = false
        }
    };

    private static void EnsureShotOptions(ShotPayload shot)
    {
        shot.ShotDataOptions ??= new ShotDataOptions();
        if (shot.BallData is not null)
            shot.ShotDataOptions.ContainsBallData = true;
        if (shot.ClubData is not null)
            shot.ShotDataOptions.ContainsClubData = true;
    }

    private static bool IsNoise(string line)
    {
        // Keep short JSON structural lines ("{", "}") — Unity pretty-prints across lines.
        if (line.Length < 4)
            return !(line.Contains('{') || line.Contains('}'));
        return NoiseTokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string line, string[] tokens) =>
        tokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
}
