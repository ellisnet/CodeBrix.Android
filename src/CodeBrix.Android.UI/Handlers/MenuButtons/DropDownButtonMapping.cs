using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: DropDownButton (tsv row DropDownButton: "MaterialButton with trailing chevron + Flyout"). A DropDownButton
/// is a Button (Core's Click, Command and its Flyout opening, the flyout shown by the overlay presenter as a Material
/// menu or bottom sheet) shown by the Button handler's Material tonal button (<see cref="ButtonHandler"/>) with a
/// trailing chevron icon: the customisation is an append to the Button handler's Content mapping (MAUI's
/// AppendToMapping) that only touches DropDownButtons.
/// </summary>
internal static class DropDownButtonMapping
{
    /// <summary>The Fluent ChevronDown glyph (the DropDownButton template's own).</summary>
    internal const string Chevron = "";

    private static bool _installed;

    /// <summary>The DropDownButton handler (the Button handler), or the templated fallback for a re-templated one.</summary>
    /// <param name="element">The DropDownButton.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element)
    {
        EnsureInstalled();
        return element is DropDownButton button && NativeControlPolicy.IsNative(button, typeof(DropDownButton), new[] { "DefaultDropDownButtonStyle" }, out _)
            ? new ButtonHandler(element, ButtonFamily.Button)
            : new TemplatedFallbackHandler();
    }

    /// <summary>Installs the chevron mapping once (before the first DropDownButton handler is created).</summary>
    internal static void EnsureInstalled()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        ButtonHandler.Mapper.AppendToMapping(ContentControl.ContentProperty, MapChevron);
        ButtonHandler.Mapper.AppendToMapping(Control.ForegroundProperty, MapChevron);
        ButtonHandler.Mapper.AppendToMapping(ToggleButton.IsCheckedProperty, MapChevron);
    }

    /// <summary>Gives a DropDownButton's Material button its trailing chevron (no-op for every other button).</summary>
    /// <param name="handler">The Button handler.</param>
    /// <param name="element">The element.</param>
    internal static void MapChevron(ButtonHandler handler, ButtonBase element)
    {
        if (element is not DropDownButton || handler.MaterialButton is not { } button)
        {
            return;
        }

        var color = button.TextColors?.DefaultColor ?? unchecked((int)0xFF1D1B20);
        button.Icon = IconDrawables.Create(new FontIconSource { Glyph = Chevron }, button.Context, handler.Density, color, 12);
        button.IconGravity = AMaterialButton.IconGravityTextEnd;
        button.IconPadding = MaterialWidgets.Px(8, handler.Density);
        button.IconSize = MaterialWidgets.Px(12, handler.Density);
        button.IconTint = button.TextColors;
        element.InvalidateMeasure();
    }
}
