using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Parses Garmin Connect / GSPro Connect log lines into shots or interesting raw events.
/// Filters are intentionally loose so the live feed shows activity whenever Connect logs.
/// </summary>
public sealed class ConnectLogLineParser
{
    private static readonly Regex JsonBlob = new(
        @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
        RegexOptions.Compiled);

    private static readonly string[] ShotTokens =
    {
        "[FROM GARMIN]",
        "FROM GARMIN",
        "GarminR50",
        "Garmin R50",
        "LmIn",
        "LM In",
        "BallData",
        "BallSpeed",
        "ClubSpeed",
        "ShotNumber",
        "CarryDistance",
        "TotalSpin",
        "LaunchAngle",
        "SpinAxis",
        "SmashFactor",
        "shot data",
        "Shot Data",
        "LaunchMonitor",
        "ball speed",
        "club speed",
        "carry"
    };

    private static readonly string[] ActivityTokens =
    {
        "garmin",
        "r50",
        "gspro",
        "gspconnect",
        "connect",
        "launch",
        "monitor",
        "ball",
        "club",
        "shot",
        "spin",
        "impact",
        "ready",
        "mph",
        "yards",
        "hla",
        "vla",
        "tcp",
        "udp",
        "socket",
        "peer",
        "device",
        "paired",
        "connected",
        "disconnected",
        "heartbeat",
        "lm ",
        " lm"
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

    public ConnectParseResult Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return ConnectParseResult.Ignore;

        var trimmed = line.Trim();
        if (IsNoise(trimmed))
            return ConnectParseResult.Ignore;

        var looksLikeShot = ContainsAny(trimmed, ShotTokens);
        var looksLikeActivity = looksLikeShot || ContainsAny(trimmed, ActivityTokens);
        if (!looksLikeActivity)
            return ConnectParseResult.Ignore;

        if (looksLikeShot)
        {
            foreach (Match match in JsonBlob.Matches(trimmed))
            {
                var json = match.Value;
                if (!json.Contains("BallData", StringComparison.OrdinalIgnoreCase) &&
                    !json.Contains("ShotNumber", StringComparison.OrdinalIgnoreCase) &&
                    !json.Contains("BallSpeed", StringComparison.OrdinalIgnoreCase) &&
                    !json.Contains("Speed", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var traffic = _jsonParser.Parse("ConnectLog", json);
                    if (traffic.Shot is { IsHeartBeat: false } shot &&
                        (shot.HasBallData || shot.BallData?.Speed is > 0 || shot.ShotNumber is > 0))
                    {
                        EnsureShotOptions(shot);
                        return ConnectParseResult.ForShot(shot, trimmed);
                    }

                    if (traffic.Shot is { IsBallDetected: true } ready)
                        return ConnectParseResult.ForReady(ready, trimmed);
                }
                catch (JsonException)
                {
                    // Fall through to key/value mapping.
                }
            }

            var mapped = TryMapKeyValues(trimmed);
            if (mapped is not null)
                return ConnectParseResult.ForShot(mapped, trimmed);
        }

        return ConnectParseResult.ForRaw(trimmed);
    }

    public static bool IsInteresting(string line) =>
        !string.IsNullOrWhiteSpace(line) &&
        !IsNoise(line) &&
        (ContainsAny(line, ShotTokens) || ContainsAny(line, ActivityTokens));

    private static bool IsNoise(string line)
    {
        if (line.Length < 4)
            return true;
        return NoiseTokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string line, string[] tokens) =>
        tokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static ShotPayload? TryMapKeyValues(string line)
    {
        var speed = FindDouble(line, "BallSpeed", "Ball Speed", "Speed");
        var hla = FindDouble(line, "HLA", "HLA", "SideAngle", "Azimuth");
        var vla = FindDouble(line, "VLA", "VLA", "LaunchAngle", "Launch Angle");
        var spin = FindDouble(line, "TotalSpin", "Total Spin", "Spin");
        var spinAxis = FindDouble(line, "SpinAxis", "Spin Axis");
        var carry = FindDouble(line, "CarryDistance", "Carry Distance", "Carry");
        var clubSpeed = FindDouble(line, "ClubSpeed", "Club Speed", "ClubHeadSpeed");
        var shotNumber = FindInt(line, "ShotNumber", "Shot Number", "Shot#");

        if (speed is null && hla is null && carry is null && clubSpeed is null && shotNumber is null)
            return null;

        var shot = new ShotPayload
        {
            DeviceId = "GarminR50",
            Units = "Yards",
            ShotNumber = shotNumber,
            BallData = new BallData
            {
                Speed = speed,
                Hla = hla,
                Vla = vla,
                TotalSpin = spin,
                SpinAxis = spinAxis,
                CarryDistance = carry
            },
            ClubData = clubSpeed is null ? null : new ClubData { Speed = clubSpeed },
            ShotDataOptions = new ShotDataOptions
            {
                ContainsBallData = speed is not null || hla is not null || carry is not null,
                ContainsClubData = clubSpeed is not null,
                IsHeartBeat = false
            }
        };

        return shot.HasBallData || shot.ShotNumber is > 0 || clubSpeed is > 0 ? shot : null;
    }

    private static void EnsureShotOptions(ShotPayload shot)
    {
        shot.ShotDataOptions ??= new ShotDataOptions();
        if (shot.BallData is not null)
            shot.ShotDataOptions.ContainsBallData = true;
        if (shot.ClubData is not null)
            shot.ShotDataOptions.ContainsClubData = true;
    }

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
