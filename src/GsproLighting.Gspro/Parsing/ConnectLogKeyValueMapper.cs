using System.Globalization;
using System.Text.RegularExpressions;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Maps non-JSON key/value Connect log fragments into ShotPayload.
/// Supports Open Connect style <c>Key: value</c> and R50 Force lines
/// (<c>BallSpeed 24.50|| …</c>).
/// </summary>
public static class ConnectLogKeyValueMapper
{
    public static ShotPayload? TryMap(string line, string? context)
    {
        var speed = FindDouble(line, "BallSpeed", "Ball Speed", "ballSpeed", "Speed");
        var hla = FindHlaDegrees(line);
        var vla = FindDouble(line, "VLA", "LaunchAngle", "Launch Angle", "launchAngle", "ExitAngle");
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

    public static void ApplyPuttingHints(ShotPayload shot, string context)
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
        if (vla is > 8)
            return;

        if (carry is double c && c <= 40 && (speed is null or <= 35) && (vla is null or <= 8))
            shot.IsPutting = true;
    }

    /// <summary>
    /// Reads the first matching numeric key from a Force / key-value log line.
    /// Accepts <c>Key: 1.2</c>, <c>Key=1.2</c>, and <c>Key 1.2</c>.
    /// </summary>
    public static double? FindDouble(string line, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = Regex.Match(
                line,
                $@"\b{Regex.Escape(key)}\b\s*[=:]?\s*(-?\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
        }

        return null;
    }

    private static double? FindHlaDegrees(string line)
    {
        foreach (var key in new[]
                 {
                     "HLA", "SideAngle", "Azimuth", "launchDirection", "LaunchDirection",
                     "carryDeviationAngle"
                 })
        {
            var value = FindDouble(line, key);
            if (value is null)
                continue;
            return GarminHlaDegrees.Normalize(value.Value, key);
        }

        return null;
    }

    private static int? FindInt(string line, params string[] keys)
    {
        var value = FindDouble(line, keys);
        return value is null ? null : (int)Math.Round(value.Value);
    }
}
