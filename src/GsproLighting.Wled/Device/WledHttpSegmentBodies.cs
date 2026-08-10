using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Shared WLED Solid FX 0 JSON bodies for HTTP strip animations.</summary>
public static class WledHttpSegmentBodies
{
    private static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);

    public static object CreateSolid(RgbColor color, byte brightness, int ledCount = 1) =>
        WledAuthoritativeStateFactory.CreateSolidBody(ledCount, color, brightness);

    public static object CreateFullStrip(int ledCount, RgbColor color, byte brightness) =>
        WledAuthoritativeStateFactory.CreateSolidBody(ledCount, color, brightness);

    public static object CreateCenterBand(
        int ledCount,
        int litCount,
        RgbColor color,
        byte brightness)
    {
        litCount = Math.Clamp(litCount, 0, ledCount);
        var start = (ledCount - litCount) / 2;
        var stop = start + litCount;
        return CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, start, Black),
                CreateRangeSegment(1, start, stop, color),
                CreateRangeSegment(2, stop, ledCount, Black)
            ]);
    }

    public static object CreateEdgesIn(
        int ledCount,
        int leftStop,
        int rightStart,
        RgbColor color,
        byte brightness)
    {
        leftStop = Math.Clamp(leftStop, 0, ledCount);
        rightStart = Math.Clamp(rightStart, leftStop, ledCount);
        return CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, leftStop, color),
                CreateRangeSegment(1, leftStop, rightStart, Black),
                CreateRangeSegment(2, rightStart, ledCount, color)
            ]);
    }

    public static object CreateRangeFill(
        int ledCount,
        int litStart,
        int litStop,
        RgbColor color,
        byte brightness)
    {
        litStart = Math.Clamp(litStart, 0, ledCount);
        litStop = Math.Clamp(litStop, litStart, ledCount);
        return CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, litStart, Black),
                CreateRangeSegment(1, litStart, litStop, color),
                CreateRangeSegment(2, litStop, ledCount, Black)
            ]);
    }

    private static object CreateBody(byte brightness, object[] segments) =>
        WledAuthoritativeStateFactory.CreateBody(brightness, segments);

    private static Dictionary<string, object?> CreateRangeSegment(
        int id,
        int start,
        int stop,
        RgbColor color) =>
        WledAuthoritativeStateFactory.CreateSegment(
            id,
            start,
            stop,
            WledAuthoritativeStateFactory.SolidFxId,
            WledAuthoritativeStateFactory.DefaultTimingByte,
            WledAuthoritativeStateFactory.DefaultTimingByte,
            WledAuthoritativeStateFactory.DefaultPaletteId,
            color);
}
