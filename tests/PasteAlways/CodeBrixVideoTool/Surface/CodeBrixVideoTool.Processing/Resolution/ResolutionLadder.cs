using System;
using System.Collections.Generic;

namespace CodeBrixVideoTool.Processing.Resolution;

/// <summary>
/// Builds the preset resolution ladder a conversion offers: the source's own size, then the
/// standard rungs that are strictly smaller than it, each scaled proportionally to even dimensions.
/// </summary>
/// <remarks>
/// <para>
/// A rung's number names the SHORT side of the picture, which is the industry convention: landscape
/// 1440p is 2560 x 1440 and portrait 1440p is 1440 x 2560. A landscape source therefore behaves
/// exactly as height keying did, and a portrait one reads the way a person expects - a 1080 x 1920
/// phone clip is offered "720p (720 x 1280)", not a 720-tall rung 406 pixels wide.
/// </para>
/// <para>
/// Dimensions are kept even because every one of the four supported formats carries AV1 in 4:2:0,
/// whose chroma planes are half-size in each direction; an odd dimension has no 4:2:0 representation
/// and the encoder refuses it.
/// </para>
/// </remarks>
public static class ResolutionLadder
{
    /// <summary>The standard short sides the ladder offers, largest first.</summary>
    public static IReadOnlyList<int> StandardShortSides { get; } = [1440, 1080, 720, 480];

    /// <summary>
    /// Builds the ladder for one source size. The first rung is always "Original"; the rest are the
    /// standard short sides strictly below the source's own short side, in descending order, with the
    /// long side following from the source's aspect ratio.
    /// </summary>
    /// <param name="sourceWidth">The source's coded width, in pixels.</param>
    /// <param name="sourceHeight">The source's coded height, in pixels.</param>
    /// <returns>The rungs to offer, in the order they should be listed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
    public static IReadOnlyList<ResolutionOption> Build(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), sourceWidth, "A source width must be positive.");
        }

        if (sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceHeight), sourceHeight, "A source height must be positive.");
        }

        var rungs = new List<ResolutionOption>
        {
            ResolutionOption.Original(MakeEven(sourceWidth), MakeEven(sourceHeight)),
        };

        //The rung names the SHORT side, so a portrait source is measured across its width and a
        //landscape one across its height - which is what height keying did for every landscape source,
        //and is why landscape ladders are unchanged.
        var sourceShortSide = Math.Min(sourceWidth, sourceHeight);
        var sourceLongSide = Math.Max(sourceWidth, sourceHeight);
        var isPortrait = sourceWidth < sourceHeight;

        foreach (var shortSide in StandardShortSides)
        {
            //Strictly below: a source whose short side is already 1080 is not offered "1080p".
            if (shortSide >= sourceShortSide)
            {
                continue;
            }

            var keyed = MakeEven(shortSide);
            var other = ProportionalOtherSide(sourceShortSide, sourceLongSide, shortSide);

            rungs.Add(ResolutionOption.Reduced(
                shortSide + "p",
                isPortrait ? keyed : other,
                isPortrait ? other : keyed));
        }

        return rungs;
    }

    /// <summary>
    /// The side that keeps the source's aspect ratio when the side a rung NAMES is scaled to it,
    /// rounded to the nearest even number of pixels.
    /// </summary>
    /// <param name="sourceKeyedSide">The source's own length of the side the rung names, in pixels.</param>
    /// <param name="sourceOtherSide">The source's own length of the side that follows, in pixels.</param>
    /// <param name="targetKeyedSide">The length the rung asks the keyed side for, in pixels.</param>
    /// <returns>An even length for the side that follows, never smaller than 2.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Any argument is not positive.</exception>
    /// <remarks>
    /// The ladder keys on the short side, so the keyed side is the source's height for a landscape
    /// picture and its width for a portrait one, and the side that follows is the other of the two.
    /// </remarks>
    public static int ProportionalOtherSide(int sourceKeyedSide, int sourceOtherSide, int targetKeyedSide)
    {
        if (sourceKeyedSide <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKeyedSide), sourceKeyedSide, "A source dimension must be positive.");
        }

        if (sourceOtherSide <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOtherSide), sourceOtherSide, "A source dimension must be positive.");
        }

        if (targetKeyedSide <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetKeyedSide), targetKeyedSide, "A target dimension must be positive.");
        }

        var exact = sourceOtherSide * (double)targetKeyedSide / sourceKeyedSide;
        return MakeEven((int)Math.Round(exact, MidpointRounding.AwayFromZero));
    }

    /// <summary>Rounds a dimension to the nearest even number of pixels, never below 2.</summary>
    /// <param name="value">The dimension to round.</param>
    /// <returns>An even value of at least 2.</returns>
    public static int MakeEven(int value)
    {
        if (value <= 2)
        {
            return 2;
        }

        return (value % 2 == 0) ? value : value + 1;
    }
}
