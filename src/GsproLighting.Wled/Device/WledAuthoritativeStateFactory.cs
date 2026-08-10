using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds complete <c>/json/state</c> bodies that override prior WLED UI state
/// (presets, playlists, palettes, multi-segment leftovers) instead of sparse patches.
/// </summary>
public static class WledAuthoritativeStateFactory
{
    /// <summary>Clear segment ids 1..N so UI multi-segment layouts cannot linger.</summary>
    public const int ExtraSegmentsToClear = 9;

    public const int SolidFxId = 0;
    public const int DefaultPaletteId = 0;
    public const int DefaultTimingByte = 128;

    private static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);

    public static Dictionary<string, object?> CreateRoot(bool on, byte brightness) =>
        new()
        {
            ["on"] = on,
            ["bri"] = brightness,
            ["live"] = false,
            ["ps"] = -1,
            ["pl"] = -1,
            ["mainseg"] = 0
        };

    public static Dictionary<string, object?> CreateClearedSegment(int id) =>
        new() { ["id"] = id, ["stop"] = 0 };

    public static Dictionary<string, object?> CreateSegment(
        int id,
        int start,
        int stop,
        int fxId,
        int speed,
        int intensity,
        int paletteId,
        RgbColor primary,
        RgbColor? secondary = null,
        RgbColor? tertiary = null,
        bool overlay = false,
        bool option2 = false,
        bool option3 = false)
    {
        ArgumentNullException.ThrowIfNull(primary);
        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["start"] = start,
            ["stop"] = stop,
            ["fx"] = fxId,
            ["sx"] = Math.Clamp(speed, 0, 255),
            ["ix"] = Math.Clamp(intensity, 0, 255),
            ["pal"] = paletteId,
            ["col"] = new[]
            {
                ToRgb(primary),
                ToRgb(secondary ?? Black),
                ToRgb(tertiary ?? Black)
            },
            ["o1"] = overlay,
            ["o2"] = option2,
            ["o3"] = option3
        };
    }

    /// <summary>Full-strip effect with preset/playlist stop and extra segments cleared.</summary>
    public static object CreateFullStripBody(
        int ledCount,
        byte brightness,
        int fxId,
        int speed,
        int intensity,
        int paletteId,
        RgbColor primary,
        RgbColor? secondary = null,
        RgbColor? tertiary = null)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        return CreateBody(
            brightness,
            [
                CreateSegment(
                    id: 0,
                    start: 0,
                    stop: ledCount,
                    fxId,
                    speed,
                    intensity,
                    paletteId,
                    primary,
                    secondary,
                    tertiary)
            ]);
    }

    public static object CreateSolidBody(int ledCount, RgbColor color, byte brightness) =>
        CreateFullStripBody(
            ledCount,
            brightness,
            SolidFxId,
            DefaultTimingByte,
            DefaultTimingByte,
            DefaultPaletteId,
            color);

    public static object CreateOffBody() =>
        CreateRoot(on: false, brightness: 0);

    /// <summary>
    /// Authoritative root plus active segments, clearing any unused ids in 1..ExtraSegmentsToClear.
    /// </summary>
    public static object CreateBody(
        byte brightness,
        IReadOnlyList<object> activeSegments,
        int mainSegmentId = 0)
    {
        ArgumentNullException.ThrowIfNull(activeSegments);
        var body = CreateRoot(on: true, brightness);
        body["mainseg"] = Math.Max(0, mainSegmentId);
        body["seg"] = AppendClearedExtras(activeSegments);
        return body;
    }

    public static object[] AppendClearedExtras(IReadOnlyList<object> activeSegments)
    {
        ArgumentNullException.ThrowIfNull(activeSegments);
        var usedIds = new HashSet<int>();
        var result = new List<object>(activeSegments.Count + ExtraSegmentsToClear);
        foreach (var segment in activeSegments)
        {
            result.Add(segment);
            if (segment is Dictionary<string, object?> dictionary &&
                dictionary.TryGetValue("id", out var idValue) &&
                idValue is int id)
                usedIds.Add(id);
        }

        for (var id = 1; id <= ExtraSegmentsToClear; id++)
        {
            if (!usedIds.Contains(id))
                result.Add(CreateClearedSegment(id));
        }

        return result.ToArray();
    }

    private static int[] ToRgb(RgbColor color) => [color.R, color.G, color.B];
}
