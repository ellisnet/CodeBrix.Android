using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Android.UI.Platform;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Diagnostics;

/// <summary>
/// A text dump of a native view hierarchy (type, element name, pixel rectangle relative to
/// the parent, visibility) for logcat: the native side of <see cref="VisualTreeDump"/>.
/// </summary>
internal static class NativeViewDump
{
    /// <summary>Dumps <paramref name="root"/> and its descendants, one line each.</summary>
    internal static IReadOnlyList<string> Dump(AView root, int maxLines = 2000)
    {
        var lines = new List<string>();
        Walk(root, 0, lines, maxLines);
        return lines;
    }

    private static void Walk(AView view, int depth, List<string> lines, int maxLines)
    {
        if (view == null || lines.Count >= maxLines)
        {
            return;
        }

        var element = (view as CodeBrixViewGroup)?.ElementHandler?.Element;
        var name = element is Microsoft.UI.Xaml.FrameworkElement { Name: { Length: > 0 } n } ? " \"" + n + "\"" : string.Empty;
        var type = element != null ? element.GetType().Name : view.GetType().Name;
        var text = view is global::Android.Widget.TextView tv ? " text=\"" + tv.Text + "\"" : string.Empty;
        lines.Add(string.Create(CultureInfo.InvariantCulture,
            $"{new string(' ', depth * 2)}{type}{name}{text} [{view.Left},{view.Top} {view.Width}x{view.Height}]{(view.Visibility != global::Android.Views.ViewStates.Visible ? " " + view.Visibility : string.Empty)}"));
        if (view is AViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                Walk(group.GetChildAt(i), depth + 1, lines, maxLines);
            }
        }
    }
}
