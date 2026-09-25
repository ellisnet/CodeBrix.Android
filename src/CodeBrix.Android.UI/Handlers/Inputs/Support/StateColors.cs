using System.Collections.Generic;
using Microsoft.UI.Xaml;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AResource = global::Android.Resource;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// One colour per WinUI visual state of a control (plan 2.11 D-O4 map: Normal, PointerOver, Pressed,
/// Disabled, and the Checked variants), turned into an Android <see cref="AColorStateList"/> whose
/// states are enabled, pressed, hovered and checked. A state without a colour of its own falls back to
/// the nearest one (checked states to Checked, the rest to Normal).
/// </summary>
internal sealed class StateColors
{
    /// <summary>The colour of the Normal state (ARGB).</summary>
    public int Normal { get; set; }

    /// <summary>PointerOver (hovered).</summary>
    public int? PointerOver { get; set; }

    /// <summary>Pressed.</summary>
    public int? Pressed { get; set; }

    /// <summary>Disabled.</summary>
    public int? Disabled { get; set; }

    /// <summary>Checked (on, selected).</summary>
    public int? Checked { get; set; }

    /// <summary>Checked and hovered.</summary>
    public int? CheckedPointerOver { get; set; }

    /// <summary>Checked and pressed.</summary>
    public int? CheckedPressed { get; set; }

    /// <summary>Checked and disabled.</summary>
    public int? CheckedDisabled { get; set; }

    /// <summary>
    /// Reads the colours of one control family from theme keys: <c>{prefix}</c>, <c>{prefix}PointerOver</c>,
    /// <c>{prefix}Pressed</c>, <c>{prefix}Disabled</c>, and with <paramref name="checkedPrefix"/> the same
    /// four for the checked states.
    /// </summary>
    /// <param name="element">The element whose resource scope is searched.</param>
    /// <param name="prefix">The key of the Normal state (e.g. "ButtonBackground").</param>
    /// <param name="fallback">The Normal colour when the key does not resolve.</param>
    /// <param name="checkedPrefix">The key of the Checked state (e.g. "ToggleButtonBackgroundChecked"), or null.</param>
    /// <returns>The colours.</returns>
    internal static StateColors FromKeys(DependencyObject element, string prefix, int fallback, string checkedPrefix = null)
    {
        var colors = new StateColors
        {
            Normal = ThemeResources.FindColor(element, prefix) ?? fallback,
            PointerOver = ThemeResources.FindColor(element, prefix + "PointerOver"),
            Pressed = ThemeResources.FindColor(element, prefix + "Pressed"),
            Disabled = ThemeResources.FindColor(element, prefix + "Disabled"),
        };

        if (checkedPrefix != null)
        {
            colors.Checked = ThemeResources.FindColor(element, checkedPrefix);
            colors.CheckedPointerOver = ThemeResources.FindColor(element, checkedPrefix + "PointerOver");
            colors.CheckedPressed = ThemeResources.FindColor(element, checkedPrefix + "Pressed");
            colors.CheckedDisabled = ThemeResources.FindColor(element, checkedPrefix + "Disabled");
        }

        return colors;
    }

    /// <summary>A copy where every unchecked interactive state shows <paramref name="color"/> (an app colour that wins over the theme's hover/pressed keys).</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The copy.</returns>
    internal StateColors WithNormalEverywhere(int color) => new()
    {
        Normal = color,
        PointerOver = color,
        Pressed = color,
        Disabled = Disabled,
        Checked = Checked,
        CheckedPointerOver = CheckedPointerOver,
        CheckedPressed = CheckedPressed,
        CheckedDisabled = CheckedDisabled,
    };

    /// <summary>The checked colours as a list without a checked state (for views that have no checked state of their own).</summary>
    /// <returns>The colours.</returns>
    internal StateColors AsChecked() => Checked is { } on
        ? new StateColors
        {
            Normal = on,
            PointerOver = CheckedPointerOver ?? on,
            Pressed = CheckedPressed ?? on,
            Disabled = CheckedDisabled ?? Disabled,
        }
        : AsUnchecked();

    /// <summary>The unchecked colours only.</summary>
    /// <returns>The colours.</returns>
    internal StateColors AsUnchecked() => new()
    {
        Normal = Normal,
        PointerOver = PointerOver,
        Pressed = Pressed,
        Disabled = Disabled,
    };

    /// <summary>The Android colour state list.</summary>
    /// <returns>The list.</returns>
    internal AColorStateList ToColorStateList()
    {
        var states = new List<int[]>(8);
        var colors = new List<int>(8);
        void Add(int color, params int[] state)
        {
            states.Add(state);
            colors.Add(color);
        }

        var enabled = AResource.Attribute.StateEnabled;
        var pressed = AResource.Attribute.StatePressed;
        var hovered = AResource.Attribute.StateHovered;
        var @checked = AResource.Attribute.StateChecked;

        if (Checked is { } checkedColor)
        {
            Add(CheckedDisabled ?? Disabled ?? checkedColor, -enabled, @checked);
        }

        Add(Disabled ?? Normal, -enabled);
        if (Checked is { } on)
        {
            Add(CheckedPressed ?? on, @checked, pressed);
            Add(CheckedPointerOver ?? on, @checked, hovered);
            Add(on, @checked);
        }

        Add(Pressed ?? Normal, pressed);
        Add(PointerOver ?? Normal, hovered);
        Add(Normal);
        return new AColorStateList(states.ToArray(), colors.ToArray());
    }

    /// <summary>A list with one colour for every state.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The list.</returns>
    internal static AColorStateList Single(int color) => AColorStateList.ValueOf(new global::Android.Graphics.Color(color));
}
