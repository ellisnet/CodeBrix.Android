using System;
using CodeBrix.Imaging;
using CodeBrix.Platform.Simple;
using WebcamPainter.Painting;

namespace WebcamPainter.ViewModels;

/// <summary>
/// One highlighter button in the Paint Mode side panel: a palette entry's name, its ink color
/// and the caption color that reads on it, plus the command the button invokes. Each item
/// carries the owning view model's own <c>SelectColorCommand</c> and its name as the command
/// parameter, so the template binds to its item only, the palette decides how many buttons
/// there are, and one <c>[AffectsCommands]</c> refresh still reaches every button at once.
/// </summary>
#if HAS_CODEBRIX
[Microsoft.UI.Xaml.Data.Bindable]
#endif
public sealed class HighlighterColorViewModel
{
    /// <summary>
    /// Creates the button item for one palette entry.
    /// </summary>
    /// <param name="color">The palette entry this button paints with.</param>
    /// <param name="selectColorCommand">The owning view model's select-a-color command.</param>
    public HighlighterColorViewModel(HighlighterColor color, SimpleCommand selectColorCommand)
    {
        if (color == null) { throw new ArgumentNullException(nameof(color)); }

        Name = color.Name;
        Color = color.Color;
        TextColor = color.TextColor;
        SelectColorCommand = selectColorCommand;
    }

    /// <summary>The color's display name - the button's caption, and the command parameter.</summary>
    public string Name { get; }

    /// <summary>The ink color; the button's background, through the color-to-brush converter.</summary>
    public Color Color { get; }

    /// <summary>The caption color that reads on <see cref="Color"/>; the button's foreground.</summary>
    public Color TextColor { get; }

    /// <summary>The owning view model's command, invoked with <see cref="Name"/> as its parameter.</summary>
    public SimpleCommand SelectColorCommand { get; }
}
