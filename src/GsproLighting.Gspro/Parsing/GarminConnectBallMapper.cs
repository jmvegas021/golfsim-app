using System.Globalization;
using System.Text.Json;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Maps Garmin Connect / GarminR50Form ball-metrics JSON (camelCase) into ShotPayload.
/// </summary>
public sealed class GarminConnectBallMapper
{
    private static readonly string[] SpeedKeys =
        { "ballSpeed", "BallSpeed", "speed", "Speed" };

    private static readonly string[] CarryKeys =
        { "carryDistance", "CarryDistance", "carry", "Carry" };

    private static readonly string[] HlaKeys =
    {
        "hla", "HLA", "launchDirection", "LaunchDirection",
        "horizontalLaunchAngle", "HorizontalLaunchAngle", "carryDeviationAngle"
    };

    private static readonly string[] VlaKeys =
    {
        "vla", "VLA", "launchAngle", "LaunchAngle",
        "verticalLaunchAngle", "VerticalLaunchAngle"
    };

    private static readonly string[] SideSpinKeys =
        { "sidespin", "sideSpin", "SideSpin" };

    private static readonly string[] BackSpinKeys =
        { "backspin", "backSpin", "BackSpin" };

    private static readonly string[] TotalSpinKeys =
        { "totalSpin", "TotalSpin", "spin", "spinRate", "SpinRate" };

    private static readonly string[] SpinAxisKeys =
        { "spinAxis", "SpinAxis" };

    private static readonly string[] SmashKeys =
        { "smashFactor", "SmashFactor", "smash" };

    private static readonly string[] ClubSpeedKeys =
        { "clubSpeed", "ClubSpeed", "clubHeadSpeed", "ClubHeadSpeed" };

    private static readonly string[] ShotNumberKeys =
        { "shotNumber", "ShotNumber", "shotId", "ShotId" };

    private static readonly string[] SpinTypeKeys =
        { "spinType", "SpinType" };

    public ShotPayload? TryMapJson(string json, string? contextLine = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return TryMap(document.RootElement, contextLine);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ShotPayload? TryMap(JsonElement root, string? contextLine = null)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var metrics = ResolveMetricsObject(root);
        var speed = ReadDouble(metrics, SpeedKeys);
        var carry = ReadDouble(metrics, CarryKeys);
        var hla = ReadDouble(metrics, HlaKeys);
        var vla = ReadDouble(metrics, VlaKeys);
        var sideSpin = ReadDouble(metrics, SideSpinKeys);
        var backSpin = ReadDouble(metrics, BackSpinKeys);
        var totalSpin = ReadDouble(metrics, TotalSpinKeys);
        var spinAxis = ReadDouble(metrics, SpinAxisKeys);
        var smash = ReadDouble(metrics, SmashKeys);
        var clubSpeed = ReadDouble(metrics, ClubSpeedKeys);
        var shotNumber = ReadInt(metrics, ShotNumberKeys);
        var spinType = ReadString(metrics, SpinTypeKeys);

        if (speed is null && carry is null && sideSpin is null && hla is null &&
            totalSpin is null && clubSpeed is null && smash is null)
            return null;

        if (totalSpin is null && (backSpin is not null || sideSpin is not null))
        {
            var back = backSpin ?? 0;
            var side = sideSpin ?? 0;
            totalSpin = Math.Sqrt(back * back + side * side);
        }

        if (spinAxis is null && sideSpin is not null && totalSpin is > 0)
            spinAxis = Math.Asin(Math.Clamp(sideSpin.Value / totalSpin.Value, -1, 1)) * 180.0 / Math.PI;

        var putting = DetectPutting(spinType, contextLine, speed, carry, vla);
        var ball = new BallData
        {
            Speed = speed,
            Hla = hla,
            Vla = vla,
            SideSpin = sideSpin,
            BackSpin = backSpin,
            TotalSpin = totalSpin,
            SpinAxis = spinAxis,
            CarryDistance = carry
        };

        return new ShotPayload
        {
            DeviceId = "GarminR50",
            Units = "Yards",
            ShotNumber = shotNumber,
            SpinType = spinType,
            IsPutting = putting,
            MeasuredSmashFactor = smash,
            BallData = ball,
            ClubData = clubSpeed is null ? null : new ClubData { Speed = clubSpeed },
            ShotDataOptions = new ShotDataOptions
            {
                ContainsBallData = true,
                ContainsClubData = clubSpeed is not null,
                IsHeartBeat = false,
                LaunchMonitorIsReady = true
            }
        };
    }

    public static bool LooksLikeBallMetrics(string json) =>
        json.Contains("carryDistance", StringComparison.OrdinalIgnoreCase) ||
        json.Contains("carryDeviation", StringComparison.OrdinalIgnoreCase) ||
        json.Contains("sidespin", StringComparison.OrdinalIgnoreCase) ||
        json.Contains("spinType", StringComparison.OrdinalIgnoreCase) ||
        json.Contains("ballSpeed", StringComparison.OrdinalIgnoreCase) ||
        json.Contains("\"BallData\"", StringComparison.OrdinalIgnoreCase) ||
        json.Contains("BallSpeed", StringComparison.OrdinalIgnoreCase);

    private static JsonElement ResolveMetricsObject(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "BallData", out var nested) &&
            nested.ValueKind == JsonValueKind.Object)
            return nested;

        if (TryGetPropertyIgnoreCase(root, "ballData", out nested) &&
            nested.ValueKind == JsonValueKind.Object)
            return nested;

        if (TryGetPropertyIgnoreCase(root, "metrics", out nested) &&
            nested.ValueKind == JsonValueKind.Object)
            return nested;

        return root;
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

        if (carry is double c && c <= 40 &&
            (speed is null or <= 35) &&
            (vla is null or <= 8))
            return true;

        return false;
    }

    private static double? ReadDouble(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetPropertyIgnoreCase(element, key, out var property))
                continue;
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
                return number;
            if (property.ValueKind == JsonValueKind.String &&
                double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }

    private static int? ReadInt(JsonElement element, params string[] keys)
    {
        var value = ReadDouble(element, keys);
        return value is null ? null : (int)Math.Round(value.Value);
    }

    private static string? ReadString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetPropertyIgnoreCase(element, key, out var property))
                continue;
            if (property.ValueKind == JsonValueKind.String)
                return property.GetString();
            if (property.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return property.ToString();
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }
}
