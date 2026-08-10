using System.Globalization;
using System.Text.RegularExpressions;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Collects pretty-printed Garmin ball metric property lines that appear without a
/// wrapping JSON object (common in R50 Connect exports) and merges them with Force
/// summary lines into a <see cref="ShotPayload"/>.
/// </summary>
public sealed class GarminSparseMetricsAccumulator
{
    private static readonly Regex JsonProperty = new(
        @"^\s*""(?<key>[^""]+)""\s*:\s*(?<value>-?\d+(?:\.\d+)?|""[^""]*"")\s*,?\s*$",
        RegexOptions.Compiled);

    private readonly Dictionary<string, string> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public bool HasMetrics => _values.Count > 0;

    public void Reset() => _values.Clear();

    /// <summary>
    /// Tries to ingest a single <c>"key": value</c> property line.
    /// </summary>
    public bool TryIngestPropertyLine(string line)
    {
        var match = JsonProperty.Match(line);
        if (!match.Success)
            return false;

        var key = match.Groups["key"].Value;
        var raw = match.Groups["value"].Value.Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            raw = raw[1..^1];
        _values[key] = raw;
        return true;
    }

    /// <summary>
    /// Merges Force / key-value metrics into the bag (does not clear existing fields).
    /// </summary>
    public void MergeForceLine(string line)
    {
        AbsorbDouble(line, "BallSpeed", "ballSpeed", "Speed");
        AbsorbDouble(line, "ExitAngle", "launchAngle", "LaunchAngle");
        AbsorbDouble(line, "HLA", "launchDirection", "carryDeviationAngle");
        AbsorbDouble(line, "sidespin", "SideSpin", "sideSpin");
        AbsorbDouble(line, "carryDistance", "CarryDistance", "Carry");
        AbsorbDouble(line, "ClubSpeed", "clubSpeed");
        AbsorbDouble(line, "SmashFactor", "smashFactor");
    }

    public ShotPayload? TryBuildShot(string? contextLine = null)
    {
        if (!HasMetrics)
            return null;

        var speed = ReadDouble("ballSpeed", "BallSpeed", "speed", "Speed");
        var carry = ReadDouble("carryDistance", "CarryDistance", "carry", "Carry");
        var hla = ReadHlaDegrees();
        var vla = ReadDouble(
            "vla", "VLA", "launchAngle", "LaunchAngle", "ExitAngle",
            "verticalLaunchAngle", "VerticalLaunchAngle");
        var sideSpin = ReadDouble("sidespin", "sideSpin", "SideSpin");
        var backSpin = ReadDouble("backspin", "backSpin", "BackSpin");
        var totalSpin = ReadDouble("totalSpin", "TotalSpin", "spin", "spinRate");
        var spinAxis = ReadDouble("spinAxis", "SpinAxis");
        var smash = ReadDouble("smashFactor", "SmashFactor", "smash");
        var clubSpeed = ReadDouble("clubSpeed", "ClubSpeed", "clubHeadSpeed");
        var shotNumber = ReadInt("shotNumber", "ShotNumber", "shotId");
        var spinType = ReadString("spinType", "SpinType");

        if (speed is null && carry is null && sideSpin is null && hla is null &&
            totalSpin is null && clubSpeed is null && smash is null)
            return null;

        if (totalSpin is null && (backSpin is not null || sideSpin is not null))
        {
            var back = backSpin ?? 0;
            var side = sideSpin ?? 0;
            totalSpin = Math.Sqrt((back * back) + (side * side));
        }

        var putting = DetectPutting(spinType, contextLine, speed, carry, vla);
        var shot = new ShotPayload
        {
            DeviceId = "GarminR50",
            Units = "Yards",
            ShotNumber = shotNumber,
            SpinType = spinType,
            IsPutting = putting,
            MeasuredSmashFactor = smash,
            BallData = new BallData
            {
                Speed = speed,
                Hla = hla,
                Vla = vla,
                SideSpin = sideSpin,
                BackSpin = backSpin,
                TotalSpin = totalSpin,
                SpinAxis = spinAxis,
                CarryDistance = carry
            },
            ClubData = clubSpeed is null ? null : new ClubData { Speed = clubSpeed },
            ShotDataOptions = new ShotDataOptions
            {
                ContainsBallData = true,
                ContainsClubData = clubSpeed is not null,
                IsHeartBeat = false,
                LaunchMonitorIsReady = true
            }
        };

        return shot;
    }

    private double? ReadHlaDegrees()
    {
        foreach (var key in new[]
                 {
                     "hla", "HLA", "launchDirection", "LaunchDirection",
                     "horizontalLaunchAngle", "HorizontalLaunchAngle",
                     "carryDeviationAngle"
                 })
        {
            if (!_values.TryGetValue(key, out var raw) ||
                !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                continue;
            return GarminHlaDegrees.Normalize(value, key);
        }

        return null;
    }

    private void AbsorbDouble(string line, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = ConnectLogKeyValueMapper.FindDouble(line, key);
            if (value is null)
                continue;
            _values[key] = value.Value.ToString(CultureInfo.InvariantCulture);
            return;
        }
    }

    private double? ReadDouble(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!_values.TryGetValue(key, out var raw))
                continue;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
        }

        return null;
    }

    private int? ReadInt(params string[] keys)
    {
        var value = ReadDouble(keys);
        return value is null ? null : (int)Math.Round(value.Value);
    }

    private string? ReadString(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (_values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                return raw;
        }

        return null;
    }

    private static bool DetectPutting(
        string? spinType,
        string? contextLine,
        double? speed,
        double? carry,
        double? vla)
    {
        if (spinType is not null &&
            (spinType.Contains("putt", StringComparison.OrdinalIgnoreCase) ||
             spinType.Contains("roll", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (contextLine is not null &&
            (contextLine.Contains("putting", StringComparison.OrdinalIgnoreCase) ||
             contextLine.Contains("sim.putting", StringComparison.OrdinalIgnoreCase) ||
             contextLine.Contains("putter", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Short chips with meaningful launch angle are not putts.
        if (vla is > 8)
            return false;

        if (carry is double c && c <= 40 &&
            (speed is null or <= 35) &&
            (vla is null or <= 8))
            return true;

        return false;
    }
}
