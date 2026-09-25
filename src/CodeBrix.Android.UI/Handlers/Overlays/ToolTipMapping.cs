using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ATooltipCompat = global::AndroidX.AppCompat.Widget.TooltipCompat;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// ToolTipService.ToolTip on Android (plan 2.9): a TEXT tooltip (a string, or a ToolTip whose Content is a string)
/// on an element whose native widget takes its own input (<see cref="ElementHandlerCapabilities.OwnsInput"/> - a
/// Material button, a slider) becomes the widget's platform tooltip (TooltipCompat: long-press on touch, hover with a
/// mouse), because Core never sees the pointer over such a widget. Everywhere else - and for rich tooltip content -
/// Core's own ToolTipService shows the tooltip in its popup layer (tier 1; FeatureConfiguration.ToolTip.UseToolTips
/// is on, D-P15), so an element never gets both.
/// </summary>
internal static class ToolTipMapping
{
    /// <summary>The text of a text tooltip value, or null for none / rich content.</summary>
    internal static string TextOf(object toolTip) => toolTip switch
    {
        string text => text,
        ToolTip { Content: string text } => text,
        _ => null,
    };

    /// <summary>Maps ToolTipService.ToolTip (for the view mapper: <c>[ToolTipService.ToolTipProperty] = ToolTipMapping.Map</c>).</summary>
    /// <param name="handler">The element's handler.</param>
    /// <param name="element">The element.</param>
    public static void Map(IViewHandler handler, UIElement element)
    {
        if (handler?.NativeView is not { } view)
        {
            return;
        }

        var ownsInput = (handler as IAndroidElementHandler)?.Capabilities.HasFlag(ElementHandlerCapabilities.OwnsInput) == true;
        var text = ownsInput ? TextOf(ToolTipService.GetToolTip(element)) : null;
        ATooltipCompat.SetTooltipText(view, string.IsNullOrEmpty(text) ? null : new global::Java.Lang.String(text));
    }
}
