using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Parses Garmin Connect / GSPro Connect log lines into shots, ready events, or sparse raw lines.
/// Tuned for GarminR50Form: readyForShot + "Logging ball data IMMEDIATELY" ball JSON.
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
    private bool _awaitingBallJson;
    private string? _ballContextLine;

    public ConnectParseResult Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return ConnectParseResult.Ignore;

        var trimmed = line.Trim();
        if (IsNoise(trimmed))
            return ConnectParseResult.Ignore;

        if (ContainsAny(trimmed, NotReadyTokens))
        {
            _awaitingBallJson = false;
            return ConnectParseResult.Ignore;
        }

        if (ContainsAny(trimmed, BallLogMarkers))
        {
            _awaitingBallJson = true;
            _ballContextLine = trimmed;
            var sameLineShot = TryExtractShot(trimmed, trimmed);
            if (sameLineShot is not null)
            {
                _awaitingBallJson = false;
                _ballContextLine = null;
                return ConnectParseResult.ForShot(sameLineShot, trimmed);
            }

            return ConnectParseResult.Ignore;
        }

        if (_awaitingBallJson)
        {
            var pendingShot = TryExtractShot(trimmed, _ballContextLine);
            if (pendingShot is not null)
            {
                _awaitingBallJson = false;
                _ballContextLine = null;
                return ConnectParseResult.ForShot(pendingShot, trimmed);
            }

            if (trimmed.Contains('{'))
            {
                _awaitingBallJson = false;
                _ballContextLine = null;
            }
        }

        var shot = TryExtractShot(trimmed, trimmed);
        if (shot is not null)
            return ConnectParseResult.ForShot(shot, trimmed);

        if (IsReadyLine(trimmed))
            return ConnectParseResult.ForReady(CreateReadyPayload(), trimmed);

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

    private ShotPayload? TryExtractShot(string line, string? context)
    {
        foreach (Match match in JsonBlob.Matches(line))
        {
            var json = match.Value;
            if (!GarminConnectBallMapper.LooksLikeBallMetrics(json) &&
                !json.Contains("BallData", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("ShotNumber", StringComparison.OrdinalIgnoreCase))
                continue;

            var garminShot = _garminMapper.TryMapJson(json, context ?? line);
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
                    ApplyPuttingHints(openConnect, context ?? line);
                    return openConnect;
                }

                if (traffic.Shot is { IsBallDetected: true })
                    return null;
            }
            catch (JsonException)
            {
                // Fall through to key/value mapping.
            }
        }

        return TryMapKeyValues(line, context);
    }

    private static ShotPayload? TryMapKeyValues(string line, string? context)
    {
        var speed = FindDouble(line, "BallSpeed", "Ball Speed", "ballSpeed", "Speed");
        var hla = FindDouble(line, "HLA", "SideAngle", "Azimuth", "carryDeviationAngle", "launchDirection");
        var vla = FindDouble(line, "VLA", "LaunchAngle", "Launch Angle", "launchAngle");
        var spin = FindDouble(line, "TotalSpin", "Total Spin", "Spin", "spinRate");
        var sideSpin = FindDouble(line, "SideSpin", "sidespin", "sideSpin");
        var spinAxis = FindDouble(line, "SpinAxis", "Spin Axis", "spinAxis");
        var carry = FindDouble(line, "CarryDistance", "Carry Distance", "carryDistance", "Carry");
        var clubSpeed = FindDouble(line, "ClubSpeed", "Club Speed", "clubSpeed", "ClubHeadSpeed");
        var smash = FindDouble(line, "SmashFactor", "smashFactor", "smash");
        var shotNumber = FindInt(line, "ShotNumber", "Shot Number", "Shot#", "shotNumber");

        if (speed is null && hla is null && carry is null && clubSpeed is null &&
            shotNumber is null && sideSpin is null)
            return null;

        var shot = new ShotPayload
        {
            DeviceId = "GarminR50",
            Units = "Yards",
            ShotNumber = shotNumber,
            MeasuredSmashFactor = smash,
            BallData = new BallData
            {
                Speed = speed,
                Hla = hla,
                Vla = vla,
                TotalSpin = spin,
                SideSpin = sideSpin,
                SpinAxis = spinAxis,
                CarryDistance = carry
            },
            ClubData = clubSpeed is null ? null : new ClubData { Speed = clubSpeed },
            ShotDataOptions = new ShotDataOptions
            {
                ContainsBallData = speed is not null || hla is not null || carry is not null || sideSpin is not null,
                ContainsClubData = clubSpeed is not null,
                IsHeartBeat = false
            }
        };

        ApplyPuttingHints(shot, context ?? line);
        return shot.HasBallData || shot.ShotNumber is > 0 || clubSpeed is > 0 ? shot : null;
    }

    private static void ApplyPuttingHints(ShotPayload shot, string context)
    {
        if (shot.IsPutting == true)
            return;

        if (context.Contains("putting", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("sim.putting", StringComparison.OrdinalIgnoreCase) ||
            (shot.SpinType?.Contains("putt", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            shot.IsPutting = true;
            return;
        }

        var carry = shot.BallData?.CarryDistance;
        var speed = shot.BallData?.Speed;
        var vla = shot.BallData?.Vla;
        if (carry is double c && c <= 40 && (speed is null or <= 35) && (vla is null or <= 8))
            shot.IsPutting = true;
    }

    private static bool IsReadyLine(string line)
    {
        if (ContainsAny(line, NotReadyTokens))
            return false;

        if (ContainsAny(line, ReadyTokens))
            return true;

        // ballPlacement alone is noisy; only treat as ready with ready language nearby.
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
        if (line.Length < 4)
            return true;
        return NoiseTokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string line, string[] tokens) =>
        tokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static double? FindDouble(string line, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = Regex.Match(
                line,
                $@"\b{Regex.Escape(key)}\b\s*[=:]\s*(-?\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
        }

        return null;
    }

    private static int? FindInt(string line, params string[] keys)
    {
        var value = FindDouble(line, keys);
        return value is null ? null : (int)Math.Round(value.Value);
    }
}

public sealed class ConnectParseResult
{
    public static ConnectParseResult Ignore { get; } = new() { Kind = ConnectParseKind.Ignore };

    public ConnectParseKind Kind { get; private init; }
    public ShotPayload? Shot { get; private init; }
    public string? RawLine { get; private init; }

    public static ConnectParseResult ForShot(ShotPayload shot, string raw) => new()
    {
        Kind = ConnectParseKind.Shot,
        Shot = shot,
        RawLine = raw
    };

    public static ConnectParseResult ForReady(ShotPayload shot, string raw) => new()
    {
        Kind = ConnectParseKind.Ready,
        Shot = shot,
        RawLine = raw
    };

    public static ConnectParseResult ForRaw(string raw) => new()
    {
        Kind = ConnectParseKind.Raw,
        RawLine = raw
    };
}

public enum ConnectParseKind
{
    Ignore,
    Raw,
    Shot,
    Ready
}
