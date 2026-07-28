namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Classifies GarminR50Form / Connect log lines as R50 green (ready) vs red (not ready).
/// Bare "readyForShot" substrings are not enough — Connect logs that word in both states.
/// </summary>
public static class ConnectReadySignalClassifier
{
    private static readonly string[] NotReadyTokens =
    {
        "NOT_READY_TO_HIT",
        "notReadyToHit",
        "not ready to hit",
        "\"status\": \"NOT_READY",
        "\"status\":\"NOT_READY",
        "status\": \"NOT_READY",
        "status\":\"NOT_READY"
    };

    public static bool IsNotReady(string line)
    {
        if (ContainsAny(line, NotReadyTokens))
            return true;

        if (HasFalseFlag(line, "readyForShot") || HasFalseFlag(line, "ReadyForShot"))
            return true;

        if (HasFalseFlag(line, "LaunchMonitorIsReady"))
            return true;

        // GarminR50Form ballPlacement status red / not ready (without READY_TO_HIT).
        if (line.Contains("GarminR50Form", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("ballPlacement", StringComparison.OrdinalIgnoreCase) &&
            (line.Contains("not ready", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("\"status\":\"red\"", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("\"status\": \"red\"", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("status=red", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    public static bool IsReady(string line)
    {
        if (IsNotReady(line))
            return false;

        // Explicit green: READY_TO_HIT (NOT_READY already excluded above).
        if (line.Contains("READY_TO_HIT", StringComparison.OrdinalIgnoreCase))
            return true;

        if (HasTrueFlag(line, "readyForShot") || HasTrueFlag(line, "ReadyForShot"))
            return true;

        if (HasTrueFlag(line, "LaunchMonitorIsReady"))
            return true;

        // "Sent readyForShot" alone is ambiguous (appears near keepalives).
        // Only accept when paired with an explicit true / READY_TO_HIT (handled above).
        return false;
    }

    public static bool MentionsReadySignal(string line) =>
        IsNotReady(line) ||
        IsReady(line) ||
        line.Contains("readyForShot", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("READY_TO_HIT", StringComparison.OrdinalIgnoreCase);

    private static bool HasTrueFlag(string line, string key) =>
        ContainsFlag(line, key, true);

    private static bool HasFalseFlag(string line, string key) =>
        ContainsFlag(line, key, false);

    private static bool ContainsFlag(string line, string key, bool value)
    {
        var needle = value ? "true" : "false";
        return line.Contains($"{key}={needle}", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($"{key} = {needle}", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($"{key}:{needle}", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($"{key}: {needle}", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($"{key}\":{needle}", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($"{key}\": {needle}", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($"{key}\" : {needle}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string line, string[] tokens) =>
        tokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
}
