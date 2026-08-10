namespace GsproLighting.Wled.Device;

/// <summary>A single WLED JSON state request and its post-frame display duration.</summary>
public sealed record WledHttpAnimationFrame(object Body, TimeSpan Duration);
