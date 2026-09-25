using Microsoft.UI.Xaml.Controls;
using AColor = global::Android.Graphics.Color;
using AContext = global::Android.Content.Context;
using AGradientDrawable = global::Android.Graphics.Drawables.GradientDrawable;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AMaterialColors = Google.Android.Material.Color.MaterialColors;
using AShapeType = global::Android.Graphics.Drawables.ShapeType;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the Material parts the native paging controls (PipsPager, PagerControl, BreadcrumbBar) are made of:
/// icon buttons showing a Fluent symbol glyph (the glyphs WinUI's templates use), text buttons, pip dots, and the
/// Material 3 colour roles they are drawn in.
/// </summary>
internal static class PagingWidgets
{
    /// <summary>Fluent ChevronLeft.</summary>
    internal const string ChevronLeft = "";

    /// <summary>Fluent ChevronRight.</summary>
    internal const string ChevronRight = "";

    /// <summary>Fluent ChevronUp.</summary>
    internal const string ChevronUp = "";

    /// <summary>Fluent ChevronDown.</summary>
    internal const string ChevronDown = "";

    /// <summary>Fluent Previous (a pager's first-page glyph).</summary>
    internal const string First = "";

    /// <summary>Fluent Next (a pager's last-page glyph).</summary>
    internal const string Last = "";

    /// <summary>A Material 3 colour role of the context's theme.</summary>
    /// <param name="context">A Material 3 context.</param>
    /// <param name="attr">The attribute name (colorPrimary, colorOnSurfaceVariant, ...).</param>
    /// <param name="fallback">The ARGB colour when the theme has none.</param>
    /// <returns>The ARGB colour.</returns>
    internal static int Role(AContext context, string attr, int fallback) =>
        AMaterialColors.GetColor(context, MaterialWidgets.AttrId(context, attr), fallback);

    /// <summary>An icon-only Material button showing a Fluent symbol glyph.</summary>
    /// <param name="context">A Material 3 context.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="description">The content description (and tooltip).</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The button.</returns>
    internal static AMaterialButton IconButton(AContext context, string glyph, string description, double density)
    {
        var style = MaterialWidgets.AttrId(context, "materialIconButtonStyle");
        var button = style != 0 ? new AMaterialButton(context, null, style) : new AMaterialButton(context);
        Compact(button);
        var color = Role(context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F));
        button.Icon = IconDrawables.Create(new FontIconSource { Glyph = glyph }, context, density, color, 16);
        button.IconPadding = 0;
        button.IconGravity = AMaterialButton.IconGravityTextStart;
        button.ContentDescription = description;
        button.TooltipText = description;
        var pad = MaterialWidgets.Px(8, density);
        button.SetPadding(pad, pad, pad, pad);
        return button;
    }

    /// <summary>A Material text button.</summary>
    /// <param name="context">A Material 3 context.</param>
    /// <param name="text">The label.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The button.</returns>
    internal static AMaterialButton TextButton(AContext context, string text, double density)
    {
        var style = MaterialWidgets.AttrId(context, "borderlessButtonStyle");
        var button = style != 0 ? new AMaterialButton(context, null, style) : new AMaterialButton(context);
        Compact(button);
        button.Text = text;
        var h = MaterialWidgets.Px(8, density);
        var v = MaterialWidgets.Px(4, density);
        button.SetPadding(h, v, h, v);
        return button;
    }

    /// <summary>A round pip of the given diameter and colour.</summary>
    /// <param name="diameterPx">The diameter in pixels.</param>
    /// <param name="color">The ARGB colour.</param>
    /// <returns>The drawable.</returns>
    internal static AGradientDrawable Pip(int diameterPx, int color)
    {
        var dot = new AGradientDrawable();
        dot.SetShape(AShapeType.Oval);
        dot.SetColor(new AColor(color));
        dot.SetSize(diameterPx, diameterPx);
        return dot;
    }

    private static void Compact(AMaterialButton button)
    {
        button.InsetTop = 0;
        button.InsetBottom = 0;
        button.SetMinWidth(0);
        button.SetMinHeight(0);
        button.SetMinimumWidth(0);
        button.SetMinimumHeight(0);
        button.SetAllCaps(false);
        button.SoundEffectsEnabled = false;
        button.Focusable = false;
        button.FocusableInTouchMode = false;
        button.SetIncludeFontPadding(false);
    }
}
