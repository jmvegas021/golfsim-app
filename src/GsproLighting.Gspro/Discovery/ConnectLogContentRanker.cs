namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Scores Connect log tails so watchers prefer files with Garmin ball metrics
/// over ready-only / keepalive streams.
/// </summary>
public static class ConnectLogContentRanker
{
    private static readonly string[] HighValueMarkers =
    {
        "Logging ball data IMMEDIATELY",
        "Logging ball data",
        "before sending to GSPro",
        "carryDistance",
        "carryDeviationAngle",
        "launchDirection",
        "GarminR50Form",
        "sidespin",
        "ballSpeed",
        "spinType",
        "READY_TO_HIT",
        "NOT_READY_TO_HIT"
    };

    private static readonly string[] MediumMarkers =
    {
        "readyForShot",
        "BallData",
        "ShotNumber",
        "LaunchMonitor"
    };

    public static int ScoreFile(string path)
    {
        try
        {
            var tail = ReadTail(path, 96_000);
            if (string.IsNullOrEmpty(tail))
                return 0;

            var score = 0;
            foreach (var marker in HighValueMarkers)
            {
                if (tail.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    score += 100;
            }

            foreach (var marker in MediumMarkers)
            {
                if (tail.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    score += 15;
            }

            return score;
        }
        catch
        {
            return 0;
        }
    }

    private static string ReadTail(string path, int maxBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var length = stream.Length;
        if (length <= 0)
            return string.Empty;

        var take = (int)Math.Min(length, maxBytes);
        stream.Seek(-take, SeekOrigin.End);
        var buffer = new byte[take];
        var read = stream.Read(buffer, 0, take);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }
}
