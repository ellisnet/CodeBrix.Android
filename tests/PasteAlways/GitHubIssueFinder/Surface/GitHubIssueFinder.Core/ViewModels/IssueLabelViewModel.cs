using CodeBrix.Platform.Simple;
using GitHubIssueFinder.GitHub;
using GitHubIssueFinder.Theming;
using Microsoft.UI.Xaml.Media;

namespace GitHubIssueFinder.ViewModels;

/// <summary>
/// One label pill beside an issue title. The pill wears the label's own colour from GitHub, laid
/// over the scheme's page ground, so the labels look the way they look on the website whichever
/// scheme is showing.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class IssueLabelViewModel : SimpleViewModel
{
    private readonly uint _labelArgb;
    private readonly bool _hasOwnColor;

    /// <summary>
    /// Builds the pill for one label.
    /// </summary>
    /// <param name="label">The label as GitHub reported it.</param>
    /// <param name="palette">The scheme in force when the row was built.</param>
    public IssueLabelViewModel(IssueLabel label, ColorSchemePalette palette)
    {
        Name = label == null ? string.Empty : (label.Name ?? string.Empty);
        _hasOwnColor = label != null && LabelColorMath.TryParseHex(label.ColorHex, out _labelArgb);

        Background = new SolidColorBrush();
        BorderBrush = new SolidColorBrush();
        Foreground = new SolidColorBrush();
        ApplyPalette(palette);
    }

    /// <summary>The label's text.</summary>
    public string Name { get; }

    /// <summary>The pill's fill.</summary>
    public Brush Background { get; }

    /// <summary>The pill's outline.</summary>
    public Brush BorderBrush { get; }

    /// <summary>The pill's text colour.</summary>
    public Brush Foreground { get; }

    /// <summary>
    /// Re-tints the pill for another scheme. The label keeps its own hue; what changes is the
    /// ground it is blended over and how far its text is pushed for readability.
    /// </summary>
    /// <param name="palette">The scheme now in force.</param>
    public void ApplyPalette(ColorSchemePalette palette)
    {
        if (palette == null) { return; }

        var source = _hasOwnColor ? _labelArgb : palette.Neutral;
        var (background, border, text) = LabelColorMath.PillColors(source, palette.Canvas, palette.BaseIsDark);

        PaletteBrushes.Repoint(Background as SolidColorBrush, background);
        PaletteBrushes.Repoint(BorderBrush as SolidColorBrush, border);
        PaletteBrushes.Repoint(Foreground as SolidColorBrush, text);
    }
}
