using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Diagnostics;

/// <summary>
/// A text dump of a CodeBrix visual tree: one line per element with its type, x:Name,
/// text (TextBlock.Text, TextBox.Text, string content of a ContentControl or
/// ContentPresenter), layout slot and actual size in DIPs. Used for logcat diagnostics
/// and by tests; it reads the tree through the public VisualTreeHelper API only.
/// </summary>
public static class VisualTreeDump
{
    /// <summary>The maximum depth walked (guards against pathological trees).</summary>
    public const int MaxDepth = 128;

    /// <summary>Returns the dump lines of the tree rooted at <paramref name="root"/>.</summary>
    /// <param name="root">The root element; null returns an empty list.</param>
    public static IReadOnlyList<string> Dump(DependencyObject root)
    {
        var lines = new List<string>();
        if (root != null)
        {
            Walk(root, 0, lines);
        }

        return lines;
    }

    /// <summary>Returns one line describing <paramref name="element"/> (no indentation).</summary>
    /// <param name="element">The element to describe.</param>
    public static string Describe(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var line = new StringBuilder(element.GetType().Name);

        if (element is FrameworkElement fe)
        {
            if (!string.IsNullOrEmpty(fe.Name))
            {
                line.Append(" #").Append(fe.Name);
            }

            var text = GetText(element);
            if (text != null)
            {
                line.Append(" text=\"").Append(Escape(text)).Append('"');
            }

            var slot = LayoutInformation.GetLayoutSlot(fe);
            line.Append(" slot=")
                .Append(Format(slot.X)).Append(',').Append(Format(slot.Y)).Append(',')
                .Append(Format(slot.Width)).Append('x').Append(Format(slot.Height));
            line.Append(" actual=").Append(Format(fe.ActualWidth)).Append('x').Append(Format(fe.ActualHeight));

            if (fe.Visibility != Visibility.Visible)
            {
                line.Append(" collapsed");
            }
        }

        return line.ToString();
    }

    private static void Walk(DependencyObject element, int depth, List<string> lines)
    {
        lines.Add(new string(' ', depth * 2) + Describe(element));
        if (depth >= MaxDepth)
        {
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child != null)
            {
                Walk(child, depth + 1, lines);
            }
        }
    }

    private static string GetText(DependencyObject element) => element switch
    {
        TextBlock textBlock => textBlock.Text,
        TextBox textBox => textBox.Text,
        ContentPresenter { Content: string s } => s,
        ContentControl { Content: string s } => s,
        _ => null,
    };

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);

    private static string Format(double value) =>
        double.IsNaN(value) ? "NaN" : Math.Round(value, 1).ToString(CultureInfo.InvariantCulture);
}
