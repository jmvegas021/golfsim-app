namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Normalizes Garmin / Connect horizontal launch angles into degrees for
/// <see cref="Core.Services.ShotEffectMapper.ClassifyDirection"/>.
/// Open Connect HLA and launchDirection are degrees; R50
/// <c>carryDeviationAngle</c> is radians (atan(deviation/carry)).
/// </summary>
public static class GarminHlaDegrees
{
    /// <summary>
    /// Converts a Garmin <c>carryDeviationAngle</c> value (radians) to degrees.
    /// </summary>
    public static double FromCarryDeviationRadians(double radians) =>
        radians * (180.0 / Math.PI);

    /// <summary>
    /// True when the JSON/log key is Garmin's radian carry-deviation angle.
    /// </summary>
    public static bool IsCarryDeviationAngleKey(string key) =>
        string.Equals(key, "carryDeviationAngle", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns degrees for direction bucketing. Converts radians when
    /// <paramref name="sourceKey"/> is carryDeviationAngle.
    /// </summary>
    public static double Normalize(double value, string? sourceKey)
    {
        if (sourceKey is not null && IsCarryDeviationAngleKey(sourceKey))
            return FromCarryDeviationRadians(value);
        return value;
    }
}
