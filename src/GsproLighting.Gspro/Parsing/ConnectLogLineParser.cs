using System.Text.Json;
using System.Text.RegularExpressions;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Parses Garmin Connect / GSPro Connect log lines into shots, ready events, or sparse raw lines.
/// Tuned for GarminR50Form: readyForShot + "Logging ball data IMMEDIATELY" ball JSON
/// (including multiline / pretty-printed payloads and pre-marker property fragments).
/// Ready / not-ready are edge-triggered to match the R50 green / red light.
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

    private static readonly string[] WaitingTokens =
    {
        "Connected to LM",
        "Telling it we're ready for shot",
        "Looking for Garmin"
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
    private readonly GarminSparseMetricsAccumulator _sparseMetrics = new();
    private readonly ConnectReadyWaitingEdgeState _statusEdges = new();
    private bool _awaitingBallJson;
    private string? _ballContextLine;
    private int _awaitingIdleLines;

    public ConnectParseResult Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return ConnectParseResult.Ignore;

        var trimmed = line.Trim();

        // Ready / Not Ready must win over sparse property ingestion (status JSON
        // also contains "key": "value" fragments).
        if (!IsNoise(trimmed))
        {
            if (ConnectReadySignalClassifier.IsNotReady(trimmed))
            {
                ClearBallWait();
                return _statusEdges.EmitNotReady(trimmed);
            }

            if (ConnectReadySignalClassifier.IsReady(trimmed))
            {
                ClearBallWait();
                return _statusEdges.EmitReady(trimmed);
            }
        }

        // Sparse property lines can arrive before the ball marker — keep them.
        if (_sparseMetrics.TryIngestPropertyLine(trimmed))
        {
            _awaitingBallJson = true;
            _awaitingIdleLines = 0;
            // Capture HLA keys in r50-log exports so direction bugs are diagnosable.
            if (ContainsHlaPropertyKey(trimmed))
                return ConnectParseResult.ForRaw(trimmed);
            return ConnectParseResult.Ignore;
        }

        // Multiline JSON path — skip noise filter so "{" / "}" lines are kept.
        if (_jsonBuffer.IsBuffering || _awaitingBallJson)
        {
            if (ContainsAny(trimmed, BallLogMarkers))
                return HandleBallMarkerLine(trimmed);

            var forceShot = TryFlushForceOrSparse(trimmed);
            if (forceShot is not null)
                return forceShot;

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

        if (ContainsAny(trimmed, BallLogMarkers))
            return HandleBallMarkerLine(trimmed);

        var forceOrSparse = TryFlushForceOrSparse(trimmed);
        if (forceOrSparse is not null)
            return forceOrSparse;

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

        var waiting = _statusEdges.TryEmitWaiting(trimmed);
        if (waiting is not null)
            return waiting;

        if (ContainsAny(trimmed, HighSignalRawTokens) &&
            (trimmed.Contains("garmin", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("connect", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("r50", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("lm", StringComparison.OrdinalIgnoreCase)))
            return ConnectParseResult.ForRaw(trimmed);

        return ConnectParseResult.Ignore;
    }

    public static bool IsInteresting(string line) =>
        !string.IsNullOrWhiteSpace(line) &&
        !IsNoise(line) &&
        (ContainsAny(line, BallLogMarkers) ||
         ConnectReadySignalClassifier.MentionsReadySignal(line) ||
         GarminConnectBallMapper.LooksLikeBallMetrics(line) ||
         ContainsAny(line, WaitingTokens) ||
         ContainsAny(line, HighSignalRawTokens));

    private ConnectParseResult HandleBallMarkerLine(string trimmed)
    {
        _awaitingBallJson = true;
        _ballContextLine = trimmed;
        _awaitingIdleLines = 0;
        // Keep sparse pre-marker metrics; only reset brace JSON buffering.
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

    private ConnectParseResult? TryFlushForceOrSparse(string trimmed)
    {
        var looksLikeForce = trimmed.Contains("BallSpeed", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.Contains("Force ", StringComparison.OrdinalIgnoreCase);
        if (!looksLikeForce && !_sparseMetrics.HasMetrics)
            return null;

        if (looksLikeForce)
            _sparseMetrics.MergeForceLine(trimmed);

        if (!_sparseMetrics.HasMetrics && !looksLikeForce)
            return null;

        // Need a playable cue — BallSpeed/Force or already-collected carry/sidespin/HLA.
        var shot = _sparseMetrics.TryBuildShot(_ballContextLine ?? trimmed);
        if (shot is null)
            return null;

        // Prefer flushing when we have speed (Force line) or were awaiting a ball marker.
        if (!looksLikeForce && !_awaitingBallJson)
            return null;

        return EmitShot(shot, trimmed);
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

    private static bool ContainsHlaPropertyKey(string line) =>
        line.Contains("carryDeviationAngle", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("launchDirection", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("\"hla\"", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("\"HLA\"", StringComparison.Ordinal) ||
        line.Contains("horizontalLaunchAngle", StringComparison.OrdinalIgnoreCase);

    private ConnectParseResult EmitShot(ShotPayload shot, string raw)
    {
        // After a shot the monitor leaves ready; next green signal should fire once.
        _statusEdges.ClearAfterShot();
        ClearBallWait();
        return ConnectParseResult.ForShot(shot, raw);
    }

    private void ClearBallWait()
    {
        _awaitingBallJson = false;
        _ballContextLine = null;
        _awaitingIdleLines = 0;
        _jsonBuffer.Reset();
        _sparseMetrics.Reset();
    }

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
