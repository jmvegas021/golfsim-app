using System.Text.Json;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

public sealed class GsproMessageParser
{
    private static readonly HashSet<string> KnownShotKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeviceID", "Units", "ShotNumber", "APIversion",
        "BallData", "ClubData", "ShotDataOptions"
    };

    private static readonly HashSet<string> KnownResponseKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "Message", "Player"
    };

    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public RawTrafficMessage Parse(string direction, string rawJson)
    {
        var trimmed = rawJson.Trim();
        var unknown = ExtractUnknownFields(trimmed);
        ShotPayload? shot = null;
        GsproResponse? response = null;

        if (LooksLikeResponse(trimmed))
        {
            var dto = JsonSerializer.Deserialize<GsproResponseDto>(trimmed, _options);
            unknown = FilterUnknown(unknown, KnownResponseKeys);
            response = dto?.ToModel(new Dictionary<string, object?>(unknown, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            var dto = JsonSerializer.Deserialize<ShotPayloadDto>(trimmed, _options);
            shot = dto?.ToModel();
            unknown = FilterUnknown(unknown, KnownShotKeys);
        }

        return new RawTrafficMessage
        {
            Timestamp = DateTimeOffset.UtcNow,
            Direction = direction,
            RawJson = trimmed,
            Shot = shot,
            Response = response,
            UnknownFields = unknown.Count > 0 ? unknown : null
        };
    }

    private static bool LooksLikeResponse(string json) =>
        json.Contains("\"Code\"", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object?> ExtractUnknownFields(string json)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in document.RootElement.EnumerateObject())
            result[property.Name] = ConvertElement(property.Value);

        return result;
    }

    private static Dictionary<string, object?> FilterUnknown(
        Dictionary<string, object?> all,
        HashSet<string> known)
    {
        return all
            .Where(pair => !known.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ConvertElement(p.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
