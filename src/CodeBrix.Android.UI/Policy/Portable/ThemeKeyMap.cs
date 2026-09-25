using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Android.UI.Policy;

/// <summary>The control families whose Fluent lightweight-styling keys the corpus apps re-key (plan 2.11).</summary>
internal enum ThemeKeyFamily
{
    /// <summary>Button* (default Button).</summary>
    Button,

    /// <summary>AccentButton* (AccentButtonStyle).</summary>
    AccentButton,

    /// <summary>ComboBox* and ComboBoxItem*.</summary>
    ComboBox,

    /// <summary>CheckBox*.</summary>
    CheckBox,

    /// <summary>TextControl* (TextBox, PasswordBox, NumberBox, AutoSuggestBox).</summary>
    TextControl,

    /// <summary>ScrollBar*.</summary>
    ScrollBar,

    /// <summary>ListViewItem*.</summary>
    ListViewItem,

    /// <summary>Slider*.</summary>
    Slider,

    /// <summary>ContentDialog*.</summary>
    ContentDialog,

    /// <summary>ProgressBar*.</summary>
    ProgressBar,
}

/// <summary>How a re-keyed brush reaches what the app's page shows on Android.</summary>
internal enum ThemeKeyPath
{
    /// <summary>The family's native handler reads the key into the Material widget's state lists.</summary>
    NativeHandler,

    /// <summary>
    /// The control keeps its Fluent template on Android (drawn natively by the panel handlers), which resolves
    /// the key exactly as on the Skia heads.
    /// </summary>
    FluentTemplate,

    /// <summary>The presentation-policy layer (AP5) applies the key to the native widget (Policy/ThemeKeyAppliers).</summary>
    PolicyApplier,

    /// <summary>The Material widget has no slot for it (documented, not honored).</summary>
    NotRepresentable,
}

/// <summary>One re-keyed Fluent key: its family, the native slot it drives and how it gets there.</summary>
/// <param name="Key">The resource key.</param>
/// <param name="Family">The control family.</param>
/// <param name="Slot">The Material slot (or why there is none).</param>
/// <param name="Path">How the key reaches the widget.</param>
internal sealed record ThemeKeyEntry(string Key, ThemeKeyFamily Family, string Slot, ThemeKeyPath Path)
{
    /// <summary>True when the key's colour appears on Android.</summary>
    internal bool IsHonored => Path != ThemeKeyPath.NotRepresentable;
}

/// <summary>
/// The 173 Fluent control resource keys that the corpus apps re-key (XAML_CORPUS section 3.1; D-O4 "honor them
/// exactly"), each with the Material slot it drives on Android and the path it takes there. Live re-pointing of
/// any of them (an app assigning SolidColorBrush.Color at run time) repaints the widgets that use it
/// (Policy/ThemeRefresh).
/// </summary>
internal static class ThemeKeyMap
{
    private static readonly string[] _states = { string.Empty, "PointerOver", "Pressed", "Disabled" };
    private static readonly IReadOnlyList<ThemeKeyEntry> _all = Build();
    private static readonly HashSet<string> _keys = new(_all.Select(e => e.Key), StringComparer.Ordinal);

    /// <summary>Every re-keyed key (173).</summary>
    internal static IReadOnlyList<ThemeKeyEntry> All => _all;

    /// <summary>True when <paramref name="key"/> is one of the 173 keys.</summary>
    /// <param name="key">A resource key.</param>
    /// <returns>True for a mapped key.</returns>
    internal static bool Contains(string key) => key != null && _keys.Contains(key);

    /// <summary>The entry of a key, or null.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The entry.</returns>
    internal static ThemeKeyEntry Find(string key) => _all.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.Ordinal));

    /// <summary>The entries of one family.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The entries.</returns>
    internal static IEnumerable<ThemeKeyEntry> Of(ThemeKeyFamily family) => _all.Where(e => e.Family == family);

    /// <summary>How many of the keys are honored on Android.</summary>
    internal static int HonoredCount => _all.Count(e => e.IsHonored);

    private static IReadOnlyList<ThemeKeyEntry> Build()
    {
        var list = new List<ThemeKeyEntry>(173);
        void Add(string key, ThemeKeyFamily family, string slot, ThemeKeyPath path) => list.Add(new ThemeKeyEntry(key, family, slot, path));

        // Button and AccentButton: ButtonHandler reads every state of the three brushes (StateColors).
        foreach (var (prefix, family) in new[] { ("Button", ThemeKeyFamily.Button), ("AccentButton", ThemeKeyFamily.AccentButton) })
        {
            foreach (var state in new[] { string.Empty, "Disabled", "PointerOver", "Pressed" })
            {
                Add(prefix + "Background" + state, family, "MaterialButton backgroundTintList" + StateNote(state), ThemeKeyPath.NativeHandler);
                Add(prefix + "BorderBrush" + state, family, "MaterialButton strokeColor" + StateNote(state), ThemeKeyPath.NativeHandler);
                Add(prefix + "Foreground" + state, family, "MaterialButton text colour and iconTint" + StateNote(state), ThemeKeyPath.NativeHandler);
            }
        }

        // ComboBox: the default ComboBox keeps its Fluent template (the native exposed drop-down is behind the
        // AppContext switch CodeBrix.Android.UI.NativeComboBox), so every ComboBox key reaches its template part.
        foreach (var key in new[]
        {
            "ComboBoxBackground", "ComboBoxBackgroundDisabled", "ComboBoxBackgroundFocused", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundPressed", "ComboBoxBackgroundUnfocused",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushDisabled", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed",
            "ComboBoxDropDownBackground", "ComboBoxDropDownBorderBrush", "ComboBoxDropDownForeground",
            "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundDisabled", "ComboBoxDropDownGlyphForegroundFocused", "ComboBoxDropDownGlyphForegroundFocusedPressed",
            "ComboBoxForeground", "ComboBoxForegroundDisabled", "ComboBoxForegroundFocused", "ComboBoxForegroundFocusedPressed", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed",
            "ComboBoxItemBackground", "ComboBoxItemBackgroundDisabled", "ComboBoxItemBackgroundPointerOver", "ComboBoxItemBackgroundPressed",
            "ComboBoxItemBackgroundSelected", "ComboBoxItemBackgroundSelectedPointerOver", "ComboBoxItemBackgroundSelectedPressed", "ComboBoxItemBackgroundSelectedUnfocused",
            "ComboBoxItemForeground", "ComboBoxItemForegroundDisabled", "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundPressed",
            "ComboBoxItemForegroundSelected", "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxItemForegroundSelectedPressed", "ComboBoxItemForegroundSelectedUnfocused",
            "ComboBoxItemPillFillBrush", "ComboBoxPlaceHolderForeground",
        })
        {
            Add(key, ThemeKeyFamily.ComboBox, "Fluent ComboBox template part (Core)", ThemeKeyPath.FluentTemplate);
        }

        // CheckBox: MaterialCheckBox has ONE box tint (outline when unchecked, filled box when checked), a glyph tint
        // and a text colour; the policy applier builds their full state lists.
        foreach (var state in _states)
        {
            Add("CheckBoxCheckBackgroundFillChecked" + state, ThemeKeyFamily.CheckBox, "MaterialCheckBox buttonTintList, checked" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("CheckBoxCheckBackgroundStrokeUnchecked" + state, ThemeKeyFamily.CheckBox, "MaterialCheckBox buttonTintList, unchecked (the outline)" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("CheckBoxCheckGlyphForegroundChecked" + state, ThemeKeyFamily.CheckBox, "MaterialCheckBox buttonIconTintList (the check mark)" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("CheckBoxForegroundChecked" + state, ThemeKeyFamily.CheckBox, "text colour, checked" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("CheckBoxForegroundUnchecked" + state, ThemeKeyFamily.CheckBox, "text colour, unchecked" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("CheckBoxCheckBackgroundFillUnchecked" + state, ThemeKeyFamily.CheckBox, "none: the Material unchecked box is an outline with a transparent inside", ThemeKeyPath.NotRepresentable);
            Add("CheckBoxCheckBackgroundStrokeChecked" + state, ThemeKeyFamily.CheckBox, "none: the Material checked box is one filled shape (CheckBackgroundFillChecked)", ThemeKeyPath.NotRepresentable);
        }

        // TextControl: TextBoxHandler reads the four brushes in their Normal/PointerOver/Focused/Disabled states.
        foreach (var brush in new[] { "Background", "BorderBrush", "Foreground", "PlaceholderForeground" })
        {
            foreach (var state in new[] { string.Empty, "Disabled", "Focused", "PointerOver" })
            {
                var slot = brush switch
                {
                    "Background" => "TextInputLayout boxBackgroundColor",
                    "BorderBrush" => "TextInputLayout boxStrokeColor",
                    "Foreground" => "EditText text colour",
                    _ => "EditText hint colour",
                };
                Add("TextControl" + brush + state, ThemeKeyFamily.TextControl, slot + StateNote(state), ThemeKeyPath.NativeHandler);
            }
        }

        Add("TextControlSelectionHighlightColor", ThemeKeyFamily.TextControl, "EditText highlight colour (through the style's SelectionHighlightColor)", ThemeKeyPath.NativeHandler);
        foreach (var state in new[] { string.Empty, "PointerOver", "Pressed" })
        {
            Add("TextControlButtonBackground" + state, ThemeKeyFamily.TextControl, "TextInputLayout end icon (clear text / reveal) background" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("TextControlButtonForeground" + state, ThemeKeyFamily.TextControl, "TextInputLayout endIconTintList" + StateNote(state), ThemeKeyPath.PolicyApplier);
        }

        Add("TextControlElevationBorderFocusedBrush", ThemeKeyFamily.TextControl, "TextInputLayout boxStrokeColor, focused (the Fluent focus line)", ThemeKeyPath.PolicyApplier);
        Add("TextControlElevationBorderBrush", ThemeKeyFamily.TextControl, "none: the Fluent resting bottom edge has no Material counterpart (the outlined box has one stroke)", ThemeKeyPath.NotRepresentable);

        // ScrollBar: ScrollViewer keeps its template on Android; its scroll bars are Core's templated ScrollBars.
        foreach (var key in new[]
        {
            "ScrollBarBackground", "ScrollBarBackgroundDisabled", "ScrollBarBackgroundPointerOver", "ScrollBarBorderBrush", "ScrollBarBorderBrushDisabled", "ScrollBarBorderBrushPointerOver",
            "ScrollBarButtonArrowForeground", "ScrollBarButtonArrowForegroundDisabled", "ScrollBarButtonArrowForegroundPointerOver", "ScrollBarButtonArrowForegroundPressed",
            "ScrollBarButtonBackground", "ScrollBarButtonBackgroundDisabled", "ScrollBarButtonBackgroundPointerOver", "ScrollBarButtonBackgroundPressed",
            "ScrollBarThumbFill", "ScrollBarThumbFillDisabled", "ScrollBarThumbFillPointerOver", "ScrollBarThumbFillPressed",
            "ScrollBarTrackFill", "ScrollBarTrackFillDisabled", "ScrollBarTrackFillPointerOver", "ScrollBarTrackStroke", "ScrollBarTrackStrokeDisabled", "ScrollBarTrackStrokePointerOver",
        })
        {
            // The *Thumb* keys colour the mouse-mode thumb; with touch the Fluent template shows its panning indicator.
            Add(key, ThemeKeyFamily.ScrollBar, "Fluent ScrollBar template part (Core; ScrollViewer keeps its template)", ThemeKeyPath.FluentTemplate);
        }

        // ListViewItem: the RecyclerView rows are Core's ListViewItem containers with their Fluent template (AP3b).
        foreach (var brush in new[] { "Background", "Foreground" })
        {
            foreach (var state in new[] { string.Empty, "PointerOver", "Pressed", "Selected", "SelectedPointerOver", "SelectedPressed" })
            {
                Add("ListViewItem" + brush + state, ThemeKeyFamily.ListViewItem, "Fluent ListViewItem template (Core, in the RecyclerView row)", ThemeKeyPath.FluentTemplate);
            }
        }

        // Slider: the policy applier builds the Material slider's full state lists.
        foreach (var state in new[] { string.Empty, "PointerOver", "Pressed" })
        {
            Add("SliderThumbBackground" + state, ThemeKeyFamily.Slider, "Slider thumbTintList" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("SliderTrackFill" + state, ThemeKeyFamily.Slider, "Slider trackInactiveTintList" + StateNote(state), ThemeKeyPath.PolicyApplier);
            Add("SliderTrackValueFill" + state, ThemeKeyFamily.Slider, "Slider trackActiveTintList" + StateNote(state), ThemeKeyPath.PolicyApplier);
        }

        // ContentDialog: a text dialog is a Material dialog (AP4); the applier colours its surface, stroke, texts
        // and scrim. (A dialog with XAML content is Core's, whose template resolves all seven.)
        Add("ContentDialogBackground", ThemeKeyFamily.ContentDialog, "Material dialog surface (background tint)", ThemeKeyPath.PolicyApplier);
        Add("ContentDialogBorderBrush", ThemeKeyFamily.ContentDialog, "Material dialog surface stroke", ThemeKeyPath.PolicyApplier);
        Add("ContentDialogForeground", ThemeKeyFamily.ContentDialog, "Material dialog title and message colour", ThemeKeyPath.PolicyApplier);
        Add("ContentDialogSmokeFill", ThemeKeyFamily.ContentDialog, "Material dialog scrim (dim amount from the brush alpha)", ThemeKeyPath.PolicyApplier);
        Add("ContentDialogLightDismissOverlayBackground", ThemeKeyFamily.ContentDialog, "Material dialog scrim (dim amount from the brush alpha)", ThemeKeyPath.PolicyApplier);
        Add("ContentDialogSeparatorBorderBrush", ThemeKeyFamily.ContentDialog, "none on a Material dialog (no separator between content and buttons); honored on a Core dialog", ThemeKeyPath.NotRepresentable);
        Add("ContentDialogTopOverlay", ThemeKeyFamily.ContentDialog, "none on a Material dialog (one surface colour); honored on a Core dialog", ThemeKeyPath.NotRepresentable);

        // ProgressBar: the style's Foreground/Background are the element's brushes, which the handler maps.
        Add("ProgressBarForeground", ThemeKeyFamily.ProgressBar, "LinearProgressIndicator indicatorColor (through the style's Foreground)", ThemeKeyPath.NativeHandler);
        Add("ProgressBarBackground", ThemeKeyFamily.ProgressBar, "LinearProgressIndicator trackColor (through the style's Background)", ThemeKeyPath.NativeHandler);
        Add("ProgressBarBorderBrush", ThemeKeyFamily.ProgressBar, "none: the Material indicator has no border (plan 2.11: ignored)", ThemeKeyPath.NotRepresentable);

        return list;
    }

    private static string StateNote(string state) => string.IsNullOrEmpty(state) ? string.Empty : ", " + state;
}
