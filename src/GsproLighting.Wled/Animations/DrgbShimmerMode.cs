namespace GsproLighting.Wled.Animations;

/// <summary>
/// Travel direction for concentrate-band (or full-strip) hold shimmer gradients.
/// </summary>
public enum DrgbShimmerMode
{
    /// <summary>Highlight expands from band center toward both edges.</summary>
    CenterOut,

    /// <summary>Highlight flows from the strip-center side of the band toward the left edge.</summary>
    TowardLeft,

    /// <summary>Highlight flows from the strip-center side of the band toward the right edge.</summary>
    TowardRight
}
