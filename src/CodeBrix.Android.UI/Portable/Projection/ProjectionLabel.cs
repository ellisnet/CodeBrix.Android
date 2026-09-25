using System;
using System.Globalization;
using System.Text;

namespace CodeBrix.Android.UI.Portable.Projection;

/// <summary>Which key properties a placeholder label shows.</summary>
internal enum ProjectionLabelFormat
{
    /// <summary>Only the type name (and x:Name).</summary>
    TypeOnly,

    /// <summary>Content: <see cref="ProjectionLabelKey.Text"/> is the string content or the content's type name.</summary>
    Content,

    /// <summary>Text entry: <see cref="ProjectionLabelKey.Text"/> is the text, <see cref="ProjectionLabelKey.Detail"/> the placeholder text.</summary>
    Text,

    /// <summary>Selection: <see cref="ProjectionLabelKey.Index"/> is the selected index, <see cref="ProjectionLabelKey.Value"/> the item count.</summary>
    Selection,

    /// <summary>Range: <see cref="ProjectionLabelKey.Value"/> of <see cref="ProjectionLabelKey.Maximum"/> (<see cref="ProjectionLabelKey.Flag"/> = indeterminate).</summary>
    Range,

    /// <summary>Toggle: <see cref="ProjectionLabelKey.Flag"/> is the checked / on state.</summary>
    Toggle,

    /// <summary>Source: <see cref="ProjectionLabelKey.Text"/> describes the image or animation source.</summary>
    Source,
}

/// <summary>
/// The key property values a placeholder label is built from. The projection viewer keeps
/// the last key of every element and rebuilds the label string only when the key changed,
/// so a layout tick allocates no label strings for unchanged elements.
/// </summary>
/// <param name="Format">What the values mean.</param>
/// <param name="Text">A text value (content, text, source); compared by value.</param>
/// <param name="Detail">A second text value (placeholder text).</param>
/// <param name="Value">A number (value, item count).</param>
/// <param name="Maximum">A second number (maximum).</param>
/// <param name="Index">An integer (selected index).</param>
/// <param name="Flag">A flag (checked, indeterminate).</param>
/// <param name="IsDisabled">True when the element is a disabled control.</param>
internal readonly record struct ProjectionLabelKey(
    ProjectionLabelFormat Format,
    string Text = null,
    string Detail = null,
    double Value = 0,
    double Maximum = 0,
    int Index = 0,
    bool Flag = false,
    bool IsDisabled = false);

/// <summary>Builds the one-line label of a placeholder box: <c>TypeName #name key=value</c>.</summary>
internal static class ProjectionLabel
{
    /// <summary>The longest text value shown in a label (longer text is cut with an ellipsis).</summary>
    internal const int MaxTextLength = 48;

    /// <summary>Builds the label.</summary>
    /// <param name="typeName">The element's type name.</param>
    /// <param name="name">The element's x:Name (may be null).</param>
    /// <param name="key">The key property values.</param>
    internal static string Build(string typeName, string name, in ProjectionLabelKey key)
    {
        var label = new StringBuilder(typeName ?? string.Empty);
        if (!string.IsNullOrEmpty(name))
        {
            label.Append(" #").Append(name);
        }

        switch (key.Format)
        {
            case ProjectionLabelFormat.Content when !string.IsNullOrEmpty(key.Text):
                label.Append(' ').Append(Quote(key.Text));
                break;

            case ProjectionLabelFormat.Text:
                if (!string.IsNullOrEmpty(key.Text))
                {
                    label.Append(' ').Append(Quote(key.Text));
                }
                else if (!string.IsNullOrEmpty(key.Detail))
                {
                    label.Append(" placeholder=").Append(Quote(key.Detail));
                }

                break;

            case ProjectionLabelFormat.Selection:
                label.Append(" items=").Append(Number(key.Value)).Append(" selected=").Append(key.Index.ToString(CultureInfo.InvariantCulture));
                break;

            case ProjectionLabelFormat.Range:
                if (key.Flag)
                {
                    label.Append(" indeterminate");
                }
                else
                {
                    label.Append(' ').Append(Number(key.Value)).Append('/').Append(Number(key.Maximum));
                }

                break;

            case ProjectionLabelFormat.Toggle:
                label.Append(key.Flag ? " on" : " off");
                break;

            case ProjectionLabelFormat.Source:
                label.Append(" source=").Append(string.IsNullOrEmpty(key.Text) ? "(none)" : Quote(key.Text));
                break;
        }

        if (key.IsDisabled)
        {
            label.Append(" (disabled)");
        }

        return label.ToString();
    }

    private static string Quote(string text)
    {
        var singleLine = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        if (singleLine.Length > MaxTextLength)
        {
            singleLine = string.Concat(singleLine.AsSpan(0, MaxTextLength - 1), "…");
        }

        return "\"" + singleLine + "\"";
    }

    private static string Number(double value) =>
        double.IsNaN(value) ? "NaN" : Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
