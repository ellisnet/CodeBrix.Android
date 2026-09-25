using System;
using System.Collections.Concurrent;
using CodeBrix.Android.UI.Portable.Layout;
using AContext = global::Android.Content.Context;
using AContextThemeWrapper = global::Android.Views.ContextThemeWrapper;
using ATypedValue = global::Android.Util.TypedValue;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Small helpers for creating Material Components widgets from C# without Android resource files of
/// our own: theme attribute and style ids by name (resolved once per name at run time: the Material
/// resources are merged into every app), a Material 3 themed context for hosts whose activity theme is
/// not a Material 3 theme (MAUI's MauiMaterialContextThemeWrapper technique), and DIP-to-pixel rounding.
/// </summary>
internal static class MaterialWidgets
{
    private static readonly ConcurrentDictionary<string, int> _ids = new(StringComparer.Ordinal);

    /// <summary>The id of a theme attribute by name (0 when the app has no such attribute).</summary>
    /// <param name="context">A context of the app.</param>
    /// <param name="name">The attribute name (e.g. "materialButtonStyle").</param>
    /// <returns>The id, or 0.</returns>
    internal static int AttrId(AContext context, string name) => Id(context, name, "attr");

    /// <summary>The id of a drawable by name (0 when the app has none of that name).</summary>
    /// <param name="context">A context of the app.</param>
    /// <param name="name">The drawable name (e.g. "design_password_eye").</param>
    /// <returns>The id, or 0.</returns>
    internal static int DrawableId(AContext context, string name) => Id(context, name, "drawable");

    /// <summary>The id of a style by name (dots as in XML, e.g. "Theme.Material3.DayNight.NoActionBar"), or 0.</summary>
    /// <param name="context">A context of the app.</param>
    /// <param name="name">The style name.</param>
    /// <returns>The id, or 0.</returns>
    internal static int StyleId(AContext context, string name)
    {
        var id = Id(context, name, "style");
        return id != 0 ? id : Id(context, name.Replace('.', '_'), "style");
    }

    /// <summary>
    /// A context whose theme is a Material 3 theme: <paramref name="context"/> itself when its theme
    /// already is one (the app's colours win), else a wrapper with Theme.Material3.DayNight.NoActionBar.
    /// </summary>
    /// <param name="context">The activity context.</param>
    /// <returns>A Material 3 context.</returns>
    internal static AContext Material3(AContext context)
    {
        if (context == null)
        {
            return null;
        }

        var marker = AttrId(context, "isMaterial3Theme");
        if (marker != 0)
        {
            using var value = new ATypedValue();
            if (context.Theme != null && context.Theme.ResolveAttribute(marker, value, true) && value.Data != 0)
            {
                return context;
            }
        }

        var theme = StyleId(context, "Theme.Material3.DayNight.NoActionBar");
        return theme != 0 ? new AContextThemeWrapper(context, theme) : context;
    }

    /// <summary>DIPs to whole pixels (rounded, as the layout replay rounds sizes).</summary>
    /// <param name="dips">The length in DIPs.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The pixels.</returns>
    internal static int Px(double dips, double density) => LayoutReplayMath.ToPixels(dips, density);

    private static int Id(AContext context, string name, string type)
    {
        if (context?.Resources == null)
        {
            return 0;
        }

        return _ids.GetOrAdd(type + "/" + name, _ =>
        {
#pragma warning disable CA1422 // GetIdentifier: the only way to reach library resources by name without resource files of our own.
            var id = context.Resources.GetIdentifier(name, type, context.PackageName);
#pragma warning restore CA1422
            return id;
        });
    }
}
