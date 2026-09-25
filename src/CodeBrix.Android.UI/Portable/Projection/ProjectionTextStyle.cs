using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Portable.Projection;

/// <summary>Horizontal placement of projected text (maps to Android gravity).</summary>
internal enum ProjectionTextAlignment
{
    /// <summary>Start (left in left-to-right text).</summary>
    Start,

    /// <summary>Centre.</summary>
    Center,

    /// <summary>End (right in left-to-right text).</summary>
    End,
}

/// <summary>
/// The text-view settings a TextBlock maps to, chosen so the native view lays the text out
/// exactly as the Core measure (StaticLayout) did: same line count, same ellipsis.
/// </summary>
/// <param name="Alignment">The horizontal alignment.</param>
/// <param name="MaxLines">The maximum line count (1 for NoWrap; int.MaxValue for unbounded wrapping).</param>
/// <param name="SingleLine">True for NoWrap (the view scrolls horizontally instead of wrapping).</param>
/// <param name="Ellipsize">True when trimmed text ends with an ellipsis.</param>
internal readonly record struct ProjectionTextStyle(ProjectionTextAlignment Alignment, int MaxLines, bool SingleLine, bool Ellipsize)
{
    /// <summary>Maps a TextBlock's alignment, wrapping, trimming and MaxLines.</summary>
    internal static ProjectionTextStyle From(TextAlignment alignment, TextWrapping wrapping, TextTrimming trimming, int maxLines)
    {
        var singleLine = wrapping == TextWrapping.NoWrap;
        var lines = singleLine ? 1 : (maxLines > 0 ? maxLines : int.MaxValue);
        var placement = alignment switch
        {
            TextAlignment.Center => ProjectionTextAlignment.Center,
            TextAlignment.Right or TextAlignment.End => ProjectionTextAlignment.End,
            _ => ProjectionTextAlignment.Start,
        };

        return new ProjectionTextStyle(placement, lines, singleLine, trimming != TextTrimming.None);
    }
}
