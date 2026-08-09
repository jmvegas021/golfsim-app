namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds detailed WLED HTTP failure text so logs include IP, URL, request JSON, response body,
/// and a short hint — never a bare "400 Bad Request".
/// </summary>
public static class WledHttpFailureFormatter
{
    public readonly record struct Details(
        string Ip,
        string Url,
        string ContentType,
        string Request,
        string Response,
        string Hint);

    public static string Format(
        string method,
        Uri endpoint,
        string? contentType,
        string requestJson,
        int statusCode,
        string? reasonPhrase,
        string? responseBody)
    {
        var response = string.IsNullOrWhiteSpace(responseBody)
            ? "(empty response body)"
            : responseBody.Trim();
        var hint = InferHint(statusCode, responseBody, contentType);
        return
            $"WLED {method} {endpoint} → {statusCode} ({reasonPhrase ?? "?"}); " +
            $"content-type={contentType ?? "(none)"}; " +
            $"request={Truncate(requestJson, 500)}; " +
            $"response={Truncate(response, 500)}; " +
            $"hint={hint}";
    }

    /// <summary>
    /// Pulls structured fields out of a message that contains a <see cref="Format"/> result
    /// (including when prefixed, e.g. "WLED effect failed: …").
    /// </summary>
    public static bool TryExtract(string message, out Details details)
    {
        details = default;
        if (string.IsNullOrEmpty(message))
            return false;

        // Work backwards from unique markers so a "WLED effect failed: …" prefix cannot
        // steal the method/URL slice away from the embedded Format() payload.
        const string contentTypeMark = "; content-type=";
        const string requestMark = "; request=";
        const string responseMark = "; response=";
        const string hintMark = "; hint=";

        var hintIdx = message.LastIndexOf(hintMark, StringComparison.Ordinal);
        if (hintIdx < 0)
            return false;

        var responseIdx = message.LastIndexOf(responseMark, hintIdx, StringComparison.Ordinal);
        var requestIdx = message.LastIndexOf(requestMark, responseIdx < 0 ? 0 : responseIdx, StringComparison.Ordinal);
        var contentTypeIdx = message.LastIndexOf(contentTypeMark, requestIdx < 0 ? 0 : requestIdx, StringComparison.Ordinal);
        if (contentTypeIdx < 0 || requestIdx < 0 || responseIdx < 0)
            return false;
        if (!(contentTypeIdx < requestIdx && requestIdx < responseIdx && responseIdx < hintIdx))
            return false;

        var arrow = message.LastIndexOf(" → ", contentTypeIdx, StringComparison.Ordinal);
        if (arrow < 0)
            return false;

        var start = message.LastIndexOf("WLED ", arrow, StringComparison.Ordinal);
        if (start < 0)
            return false;

        var methodAndUrl = message[(start + "WLED ".Length)..arrow].Trim();
        var space = methodAndUrl.IndexOf(' ');
        if (space <= 0)
            return false;

        var url = methodAndUrl[(space + 1)..].Trim();
        var ip = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
        var contentType = message[(contentTypeIdx + contentTypeMark.Length)..requestIdx];
        var request = message[(requestIdx + requestMark.Length)..responseIdx];
        var response = message[(responseIdx + responseMark.Length)..hintIdx];
        var hint = message[(hintIdx + hintMark.Length)..];

        details = new Details(ip, url, contentType, request, response, hint);
        return true;
    }

    public static string InferHint(int statusCode, string? responseBody, string? contentType)
    {
        if (statusCode == 400 && string.IsNullOrWhiteSpace(responseBody))
        {
            if (contentType is not null &&
                contentType.Contains("charset", StringComparison.OrdinalIgnoreCase))
                return "firmware likely rejected Content-Type charset — send application/json only";
            return "empty 400 usually means Content-Type/body rejected before JSON parse — verify application/json without charset and that the IP is the controller";
        }

        if (!string.IsNullOrWhiteSpace(responseBody) &&
            responseBody.Contains("\"error\"", StringComparison.OrdinalIgnoreCase))
        {
            if (responseBody.Contains(":9", StringComparison.Ordinal) ||
                responseBody.Contains(": 9", StringComparison.Ordinal))
                return "WLED error 9 = JSON rejected (invalid field/value, fx/pal id, or segment)";
            if (responseBody.Contains(":10", StringComparison.Ordinal) ||
                responseBody.Contains(": 10", StringComparison.Ordinal))
                return "WLED error 10 = could not deserialize requested state";
            return "WLED returned a JSON error code — check fx/palette/segment ids against this firmware";
        }

        if (statusCode is >= 500 and <= 599)
            return "controller HTTP error — power-cycle WLED if it keeps happening";

        if (statusCode == 404)
            return "path not found — confirm this host is WLED (try /json/info in a browser)";

        return "see request/response above";
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= maxChars ? value : value[..maxChars] + "…";
    }
}
