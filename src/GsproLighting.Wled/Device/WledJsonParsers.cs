using System.Text.Json;
using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Parses WLED JSON HTTP responses into device models.</summary>
public static class WledJsonParsers
{
    public static IReadOnlyList<WledNamedEntry> ParseNameArray(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<WledNamedEntry>();
        var index = 0;
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var name = element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? $"#{index}"
                : $"#{index}";
            if (!string.Equals(name, "RSVD", StringComparison.OrdinalIgnoreCase) &&
                name != "-")
                list.Add(new WledNamedEntry { Id = index, Name = name });
            index++;
        }

        return list;
    }

    public static WledDeviceInfo ParseInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var leds = GetObject(root, "leds");
        return new WledDeviceInfo
        {
            Name = GetString(root, "name") ?? "WLED",
            Version = GetString(root, "ver") ?? "",
            LedCount = GetInt(leds, "count"),
            EffectCount = GetInt(root, "fxcount"),
            PaletteCount = GetInt(root, "palcount"),
            MaxSegments = GetInt(leds, "maxseg", 1),
            Mac = GetString(root, "mac") ?? ""
        };
    }

    public static WledDeviceState ParseState(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var segments = new List<WledSegmentState>();
        if (TryGetProperty(root, "seg", out var segElement) &&
            segElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var seg in segElement.EnumerateArray())
                segments.Add(ParseSegment(seg));
        }

        return new WledDeviceState
        {
            On = GetBool(root, "on", true),
            Brightness = (byte)Math.Clamp(GetInt(root, "bri", 128), 1, 255),
            PresetId = GetInt(root, "ps", -1),
            PlaylistId = GetInt(root, "pl", -1),
            Live = GetBool(root, "live", false),
            MainSegmentId = GetInt(root, "mainseg", 0),
            Segments = segments
        };
    }

    public static IReadOnlyList<WledPresetListEntry> ParsePresets(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return [];

        var list = new List<WledPresetListEntry>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (!int.TryParse(property.Name, out var id) || id <= 0)
                continue;
            if (property.Value.ValueKind != JsonValueKind.Object)
                continue;

            var name = GetString(property.Value, "n") ?? $"Preset {id}";
            list.Add(new WledPresetListEntry { Id = id, Name = name });
        }

        return list.OrderBy(p => p.Id).ToArray();
    }

    public static object BuildPatchBody(WledStatePatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var body = new Dictionary<string, object?>();
        if (patch.On is bool on)
            body["on"] = on;
        if (patch.Brightness is byte bri)
            body["bri"] = bri;
        if (patch.PresetId is int ps)
            body["ps"] = ps;
        if (patch.PlaylistId is int pl)
            body["pl"] = pl;
        if (patch.NextPlaylist is true)
            body["np"] = true;
        if (patch.Live is bool live)
            body["live"] = live;

        var hasSegment =
            patch.FxId is not null ||
            patch.Speed is not null ||
            patch.Intensity is not null ||
            patch.PaletteId is not null ||
            patch.Overlay is not null ||
            patch.Option2 is not null ||
            patch.Option3 is not null ||
            patch.Primary is not null ||
            patch.Secondary is not null ||
            patch.Tertiary is not null ||
            patch.SegmentId is not null;

        if (!hasSegment)
            return body;

        var seg = new Dictionary<string, object?>();
        if (patch.SegmentId is int segId)
            seg["id"] = segId;
        if (patch.FxId is int fx)
            seg["fx"] = fx;
        if (patch.Speed is int sx)
            seg["sx"] = ClampByte(sx);
        if (patch.Intensity is int ix)
            seg["ix"] = ClampByte(ix);
        if (patch.PaletteId is int pal)
            seg["pal"] = pal;
        if (patch.Overlay is bool o1)
            seg["o1"] = o1;
        if (patch.Option2 is bool o2)
            seg["o2"] = o2;
        if (patch.Option3 is bool o3)
            seg["o3"] = o3;
        if (patch.Primary is RgbColor primary)
        {
            seg["col"] = new[]
            {
                ToRgb(primary),
                ToRgb(patch.Secondary ?? RgbColor.FromRgb(0, 0, 0)),
                ToRgb(patch.Tertiary ?? RgbColor.FromRgb(0, 0, 0))
            };
        }

        body["seg"] = new[] { seg };
        return body;
    }

    private static WledSegmentState ParseSegment(JsonElement seg)
    {
        var colors = ParseColors(seg);
        return new WledSegmentState
        {
            Id = GetInt(seg, "id"),
            Start = GetInt(seg, "start"),
            Stop = GetInt(seg, "stop"),
            FxId = GetInt(seg, "fx"),
            Speed = GetInt(seg, "sx", 128),
            Intensity = GetInt(seg, "ix", 128),
            PaletteId = GetInt(seg, "pal"),
            Overlay = GetBool(seg, "o1", false),
            Option2 = GetBool(seg, "o2", false),
            Option3 = GetBool(seg, "o3", false),
            On = GetBool(seg, "on", true),
            Brightness = (byte)Math.Clamp(GetInt(seg, "bri", 255), 0, 255),
            Primary = colors[0],
            Secondary = colors[1],
            Tertiary = colors[2]
        };
    }

    private static RgbColor[] ParseColors(JsonElement seg)
    {
        var colors = new[]
        {
            RgbColor.FromRgb(255, 255, 255),
            RgbColor.FromRgb(0, 0, 0),
            RgbColor.FromRgb(0, 0, 0)
        };
        if (!TryGetProperty(seg, "col", out var col) || col.ValueKind != JsonValueKind.Array)
            return colors;

        var i = 0;
        foreach (var entry in col.EnumerateArray())
        {
            if (i >= colors.Length)
                break;
            colors[i] = ParseColorEntry(entry);
            i++;
        }

        return colors;
    }

    private static RgbColor ParseColorEntry(JsonElement entry)
    {
        if (entry.ValueKind == JsonValueKind.Array && entry.GetArrayLength() >= 3)
        {
            return RgbColor.FromRgb(
                (byte)Math.Clamp(entry[0].GetInt32(), 0, 255),
                (byte)Math.Clamp(entry[1].GetInt32(), 0, 255),
                (byte)Math.Clamp(entry[2].GetInt32(), 0, 255));
        }

        return RgbColor.FromRgb(0, 0, 0);
    }

    private static int[] ToRgb(RgbColor color) => [color.R, color.G, color.B];
    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);

    private static JsonElement GetObject(JsonElement parent, string name) =>
        TryGetProperty(parent, name, out var element) && element.ValueKind == JsonValueKind.Object
            ? element
            : default;

    private static string? GetString(JsonElement parent, string name) =>
        TryGetProperty(parent, name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static int GetInt(JsonElement parent, string name, int fallback = 0)
    {
        if (!TryGetProperty(parent, name, out var element) || element.ValueKind != JsonValueKind.Number)
            return fallback;
        return element.TryGetInt32(out var value) ? value : fallback;
    }

    private static bool GetBool(JsonElement parent, string name, bool fallback)
    {
        if (!TryGetProperty(parent, name, out var element))
            return fallback;
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

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
