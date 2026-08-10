using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

public sealed class ConnectParseResult
{
    public static ConnectParseResult Ignore { get; } = new() { Kind = ConnectParseKind.Ignore };

    public ConnectParseKind Kind { get; private init; }
    public ShotPayload? Shot { get; private init; }
    public string? RawLine { get; private init; }

    public static ConnectParseResult ForShot(ShotPayload shot, string raw) => new()
    {
        Kind = ConnectParseKind.Shot,
        Shot = shot,
        RawLine = raw
    };

    public static ConnectParseResult ForReady(ShotPayload shot, string raw) => new()
    {
        Kind = ConnectParseKind.Ready,
        Shot = shot,
        RawLine = raw
    };

    public static ConnectParseResult ForNotReady(string raw) => new()
    {
        Kind = ConnectParseKind.NotReady,
        RawLine = raw
    };

    public static ConnectParseResult ForWaiting(string raw) => new()
    {
        Kind = ConnectParseKind.Waiting,
        RawLine = raw
    };

    public static ConnectParseResult ForRaw(string raw) => new()
    {
        Kind = ConnectParseKind.Raw,
        RawLine = raw
    };
}

public enum ConnectParseKind
{
    Ignore,
    Raw,
    Shot,
    Ready,
    NotReady,
    /// <summary>GSPro / Connect loading before first Ready (aqua Waiting hold).</summary>
    Waiting
}
