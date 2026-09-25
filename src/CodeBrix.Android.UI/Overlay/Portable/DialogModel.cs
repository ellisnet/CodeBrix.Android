using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// Which WinUI ContentDialog button sits in which Material dialog slot (positive / negative / neutral).
/// </summary>
/// <remarks>
/// Primary is the positive (confirming) button. Close is the negative (dismissing) one; with no Close button
/// the Secondary button takes the negative slot (a Yes / No dialog reads "No  Yes"), otherwise Secondary is
/// the neutral button, which Material places apart on the leading side. A button without text is absent,
/// as in WinUI.
/// </remarks>
/// <param name="Positive">The WinUI button in the positive slot (None = no positive button).</param>
/// <param name="Negative">The WinUI button in the negative slot.</param>
/// <param name="Neutral">The WinUI button in the neutral slot.</param>
internal readonly record struct DialogButtonSlots(ContentDialogButton Positive, ContentDialogButton Negative, ContentDialogButton Neutral)
{
    /// <summary>Places the buttons a dialog has (non-empty text) into the Material slots.</summary>
    /// <param name="primaryText">PrimaryButtonText.</param>
    /// <param name="secondaryText">SecondaryButtonText.</param>
    /// <param name="closeText">CloseButtonText.</param>
    /// <returns>The slots.</returns>
    internal static DialogButtonSlots For(string primaryText, string secondaryText, string closeText)
    {
        var hasPrimary = !string.IsNullOrEmpty(primaryText);
        var hasSecondary = !string.IsNullOrEmpty(secondaryText);
        var hasClose = !string.IsNullOrEmpty(closeText);

        var positive = hasPrimary ? ContentDialogButton.Primary : ContentDialogButton.None;
        if (hasClose)
        {
            return new DialogButtonSlots(positive, ContentDialogButton.Close, hasSecondary ? ContentDialogButton.Secondary : ContentDialogButton.None);
        }

        return new DialogButtonSlots(positive, hasSecondary ? ContentDialogButton.Secondary : ContentDialogButton.None, ContentDialogButton.None);
    }
}

/// <summary>
/// Reads the text of a ContentDialog's Title or Content when it IS text: a string, a plain object without a
/// template (shown through ToString, as a ContentPresenter shows it), or a TextBlock made only of Runs and
/// LineBreaks (SimpleViewModel's ShowInfo / ShowError / ConfirmDialog put their message in such a TextBlock).
/// Anything else is XAML content that only Core can present (tier 1).
/// </summary>
internal static class DialogText
{
    /// <summary>Gets the text of a dialog's title or content, when it is text.</summary>
    /// <param name="value">Title or Content.</param>
    /// <param name="template">TitleTemplate or ContentTemplate (a template means XAML presentation).</param>
    /// <param name="text">The text ("" for null).</param>
    /// <returns>True when the value is text.</returns>
    internal static bool TryGetText(object value, DataTemplate template, out string text)
    {
        text = string.Empty;
        if (value == null)
        {
            return true;
        }

        if (template != null)
        {
            return false;
        }

        switch (value)
        {
            case string s:
                text = s;
                return true;
            case TextBlock textBlock:
                return TryGetTextBlockText(textBlock, out text);
            case UIElement:
            case DependencyObject:
                return false;
            default:
                text = value.ToString() ?? string.Empty;
                return true;
        }
    }

    private static bool TryGetTextBlockText(TextBlock textBlock, out string text)
    {
        text = string.Empty;
        var inlines = textBlock.Inlines;
        if (inlines == null || inlines.Count == 0)
        {
            text = textBlock.Text ?? string.Empty;
            return true;
        }

        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Text);
                    break;
                case LineBreak:
                    builder.Append('\n');
                    break;
                default:
                    return false;
            }
        }

        text = builder.ToString();
        return true;
    }
}
