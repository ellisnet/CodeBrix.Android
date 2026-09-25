namespace CodeBrixVideoTool.Processing.Formats;

/// <summary>
/// How hard a conversion tries: the one quality choice this application offers, from the smallest
/// file to the best picture.
/// </summary>
/// <remarks>
/// The choice moves the encoder's constant rate factor and nothing else. The speed preset stays
/// pinned, so an encode takes about as long whichever stop is chosen, and the sound is settled by the
/// destination alone and never by this.
/// </remarks>
public enum QualityLevel
{
    /// <summary>Visibly softer than the source, and much the smallest file.</summary>
    Fair = 0,

    /// <summary>The default: a good picture at a sensible size.</summary>
    Good = 1,

    /// <summary>Better than the default, at a noticeably larger size.</summary>
    Better = 2,

    /// <summary>As close to the source as this application asks for, and the largest file.</summary>
    Best = 3,
}
