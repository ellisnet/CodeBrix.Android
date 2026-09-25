#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AResource = global::Android.Resource;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// The policy appliers of the re-keyed Fluent keys (plan 2.11 key-family table, D-O4 "honor them exactly") for
/// the families whose handler reads only part of them: CheckBox (every state of the box, the check mark and the
/// text), Slider (every state of the tracks and the thumb), TextControl (the end-icon button, the focus line).
/// An applier runs at the end of the family handler's own colour code (CheckBoxHandler.ApplyColors,
/// SliderHandler.MapColors, TextBoxHandler's recolour and editing refresh - so a recolour the handler makes outside
/// its mapper, e.g. an in-place Foreground re-point or a PasswordBox RefreshFromCore, keeps the full state lists),
/// once the layer is installed, and only when the app re-keyed at least one of the family's keys - a control whose keys are the framework's keeps exactly what
/// its handler gives it. Each key reads from the element's resource scope, so page-level keys win over
/// application-level ones as in a Fluent template.
/// </summary>
internal static class ThemeKeyAppliers
{
    private static readonly string[] _checkBoxKeys = ThemeKeyMap.Of(ThemeKeyFamily.CheckBox).Select(e => e.Key).ToArray();
    private static readonly string[] _sliderKeys = ThemeKeyMap.Of(ThemeKeyFamily.Slider).Select(e => e.Key).ToArray();
    private static readonly string[] _sliderThumbKeys = { "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed" };
    private static readonly string[] _textButtonKeys =
    {
        "TextControlButtonBackground", "TextControlButtonBackgroundPointerOver", "TextControlButtonBackgroundPressed",
        "TextControlButtonForeground", "TextControlButtonForegroundPointerOver", "TextControlButtonForegroundPressed",
    };

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Google.Android.Material.TextField.TextInputLayout, AColorStateList> _endIconTints = new();

    private static bool _enabled;

    /// <summary>How many times an applier re-coloured a widget (diagnostics, tests).</summary>
    internal static int AppliedCount { get; private set; }

    /// <summary>True once the presentation policy installed the appliers (the handlers call them from then on).</summary>
    internal static bool IsEnabled => _enabled;

    /// <summary>
    /// The end-icon tint an applier gave a text field (the Material field has no getter for it), or null
    /// (diagnostics, tests).
    /// </summary>
    /// <param name="field">The text field.</param>
    /// <returns>The tint list, or null.</returns>
    internal static AColorStateList EndIconTintOf(Google.Android.Material.TextField.TextInputLayout field) =>
        field != null && _endIconTints.TryGetValue(field, out var tint) ? tint : null;

    /// <summary>
    /// Turns the appliers on (the presentation policy's installation). The family handlers call them at the end
    /// of their own colour code; handlers connected before this get them through ThemeRefresh's re-map.
    /// </summary>
    internal static void Install() => _enabled = true;

    /// <summary>The CheckBox family: the box (checked fill / unchecked outline), the check mark and the text, per state.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="element">The CheckBox.</param>
    internal static void CheckBox(CheckBoxHandler handler, CheckBox element)
    {
        if (!_enabled || handler?.PlatformView is not { } view || !ThemeKeys.AnyAppKey(element, _checkBoxKeys))
        {
            return;
        }

        int C(string key, int fallback) => ThemeKeys.Color(element, key, fallback);
        var enabled = AResource.Attribute.StateEnabled;
        var pressed = AResource.Attribute.StatePressed;
        var hovered = AResource.Attribute.StateHovered;
        var @checked = AResource.Attribute.StateChecked;
        var indeterminate = MaterialWidgets.AttrId(view.Context, "state_indeterminate");

        var fillChecked = C("CheckBoxCheckBackgroundFillChecked", unchecked((int)0xFF0078D4));
        var strokeUnchecked = C("CheckBoxCheckBackgroundStrokeUnchecked", unchecked((int)0xFF808080));
        var box = new List<(int[] State, int Color)>
        {
            (new[] { -enabled, @checked }, C("CheckBoxCheckBackgroundFillCheckedDisabled", fillChecked)),
        };
        if (indeterminate != 0)
        {
            box.Add((new[] { -enabled, indeterminate }, C("CheckBoxCheckBackgroundFillCheckedDisabled", fillChecked)));
        }

        box.Add((new[] { -enabled }, C("CheckBoxCheckBackgroundStrokeUncheckedDisabled", strokeUnchecked)));
        box.Add((new[] { @checked, pressed }, C("CheckBoxCheckBackgroundFillCheckedPressed", fillChecked)));
        box.Add((new[] { @checked, hovered }, C("CheckBoxCheckBackgroundFillCheckedPointerOver", fillChecked)));
        box.Add((new[] { @checked }, fillChecked));
        if (indeterminate != 0)
        {
            box.Add((new[] { indeterminate }, C("CheckBoxCheckBackgroundFillIndeterminate", fillChecked)));
        }

        box.Add((new[] { pressed }, C("CheckBoxCheckBackgroundStrokeUncheckedPressed", strokeUnchecked)));
        box.Add((new[] { hovered }, C("CheckBoxCheckBackgroundStrokeUncheckedPointerOver", strokeUnchecked)));
        box.Add((Array.Empty<int>(), strokeUnchecked));
        view.ButtonTintList = List(box);

        var glyph = C("CheckBoxCheckGlyphForegroundChecked", unchecked((int)0xFFFFFFFF));
        view.ButtonIconTintList = List(new List<(int[] State, int Color)>
        {
            (new[] { -enabled }, C("CheckBoxCheckGlyphForegroundCheckedDisabled", glyph)),
            (new[] { pressed }, C("CheckBoxCheckGlyphForegroundCheckedPressed", glyph)),
            (new[] { hovered }, C("CheckBoxCheckGlyphForegroundCheckedPointerOver", glyph)),
            (Array.Empty<int>(), glyph),
        });

        // The app's own Foreground (a local value) wins over the text keys, as in the Fluent template.
        if (!ThemeResources.IsLocal(element, Control.ForegroundProperty))
        {
            view.SetTextColor(ThemeKeys.States(
                element,
                "CheckBoxForegroundUnchecked",
                unchecked((int)0xFF000000),
                "CheckBoxForegroundUncheckedPointerOver",
                "CheckBoxForegroundUncheckedPressed",
                "CheckBoxForegroundUncheckedDisabled",
                checkedStates: ("CheckBoxForegroundChecked", "CheckBoxForegroundCheckedPointerOver", "CheckBoxForegroundCheckedPressed", "CheckBoxForegroundCheckedDisabled")));
        }

        AppliedCount++;
    }

    /// <summary>The Slider family: the value and rest tracks and the thumb, per state.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="element">The Slider.</param>
    internal static void Slider(SliderHandler handler, Slider element)
    {
        if (!_enabled || handler?.PlatformView?.Slider is not { } slider || !ThemeKeys.AnyAppKey(element, _sliderKeys))
        {
            return;
        }

        var accent = ThemeKeys.Color(element, "SliderTrackValueFill", unchecked((int)0xFF0078D4));
        slider.TrackActiveTintList = ThemeKeys.States(element, "SliderTrackValueFill", accent, "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed", "SliderTrackValueFillDisabled");
        slider.TrackInactiveTintList = ThemeKeys.States(element, "SliderTrackFill", unchecked((int)0xFF909090), "SliderTrackFillPointerOver", "SliderTrackFillPressed", "SliderTrackFillDisabled");
        if (ThemeKeys.AnyAppKey(element, _sliderThumbKeys))
        {
            slider.ThumbTintList = ThemeKeys.States(element, "SliderThumbBackground", accent, "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed", "SliderThumbBackgroundDisabled");
        }

        AppliedCount++;
    }

    /// <summary>The TextControl extras: the end-icon button (clear text / password reveal) and the focus line.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="element">The TextBox (or PasswordBox).</param>
    internal static void TextControl(TextBoxHandler handler, TextBox element)
    {
        if (!_enabled || handler?.PlatformView?.Field is not { } field)
        {
            return;
        }

        var applied = false;
        if (ThemeKeys.AnyAppKey(element, _textButtonKeys))
        {
            var tint = ThemeKeys.States(element, "TextControlButtonForeground", unchecked((int)0xFF606060), "TextControlButtonForegroundPointerOver", "TextControlButtonForegroundPressed");
            field.SetEndIconTintList(tint);
            _endIconTints.AddOrUpdate(field, tint);
            var id = field.Context?.Resources?.GetIdentifier("text_input_end_icon", "id", field.Context.PackageName) ?? 0;
            if (id != 0 && field.FindViewById(id) is { } icon)
            {
                icon.BackgroundTintList = ThemeKeys.States(element, "TextControlButtonBackground", 0, "TextControlButtonBackgroundPointerOver", "TextControlButtonBackgroundPressed");
            }

            applied = true;
        }

        if (ThemeKeys.IsAppKey(element, "TextControlElevationBorderFocusedBrush")
            && !ThemeKeys.IsAppKey(element, "TextControlBorderBrushFocused")
            && !ThemeResources.IsLocal(element, Control.BorderBrushProperty))
        {
            field.SetBoxStrokeColorStateList(ThemeKeys.States(
                element,
                "TextControlBorderBrush",
                unchecked((int)0xFF808080),
                "TextControlBorderBrushPointerOver",
                null,
                "TextControlBorderBrushDisabled",
                "TextControlElevationBorderFocusedBrush"));
            applied = true;
        }

        if (applied)
        {
            AppliedCount++;
        }
    }

    private static AColorStateList List(List<(int[] State, int Color)> entries) =>
        new(entries.Select(e => e.State).ToArray(), entries.Select(e => e.Color).ToArray());
}
#endif
