using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GitHubIssueFinder.Theming;

/// <summary>
/// Turns the plain ARGB numbers a scheme is made of into the drawing types the view layer binds
/// to. A brush created here is kept and re-pointed rather than replaced, so a scheme change
/// repaints every consumer without a single binding being raised again.
/// </summary>
public static class PaletteBrushes
{
    /// <summary>
    /// Turns an ARGB value into a colour.
    /// </summary>
    /// <param name="argb">The colour, for example 0xFF0969DA.</param>
    /// <returns>The same colour as a drawing colour.</returns>
    public static Color ToColor(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFFu),
        (byte)((argb >> 16) & 0xFFu),
        (byte)((argb >> 8) & 0xFFu),
        (byte)(argb & 0xFFu));

    /// <summary>
    /// Creates a brush of one colour.
    /// </summary>
    /// <param name="argb">The colour, for example 0xFF0969DA.</param>
    /// <returns>A new brush.</returns>
    public static SolidColorBrush Create(uint argb) => new SolidColorBrush(ToColor(argb));

    /// <summary>
    /// Re-points an existing brush at another colour, which repaints everything drawn with it.
    /// </summary>
    /// <param name="brush">The brush to re-point; null is ignored.</param>
    /// <param name="argb">The new colour.</param>
    public static void Repoint(SolidColorBrush brush, uint argb)
    {
        if (brush == null) { return; }
        brush.Color = ToColor(argb);
    }
}
