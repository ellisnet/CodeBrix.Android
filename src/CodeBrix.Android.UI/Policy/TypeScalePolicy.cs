#if __ANDROID__
using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using ATextView = global::Android.Widget.TextView;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// The type scale and the FontSize unit rule on the native text widgets (plan 2.11 "Type scale", D-O4):
/// <list type="bullet">
/// <item>FontSize is in sp while IsTextScaleFactorEnabled is true (the WinUI default: the user's font scale
/// applies) and in dp when an element turns it off - on TextBlock, Button, CheckBox, RadioButton and the text
/// boxes, re-applied when the system font scale changes.</item>
/// <item>A TextBlock the app styled with one of the framework's TextBlock styles (Caption/Body/BodyStrong/Subtitle/
/// Title/TitleLarge/Display) gets that style's Material type role (size, line height, weight, tracking), unless it
/// sets FontSize itself; the app's font family stays. TextBlocks inside a control template keep the Fluent
/// metrics their template is laid out for.</item>
/// </list>
/// Applied after each handler's own font mapping (property-mapper appends).
/// </summary>
internal static class TypeScalePolicy
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, object> _roled = new();
    private static float _lastFontScale = -1;

    /// <summary>The font scale in use (the override, else the system's).</summary>
    internal static double FontScale =>
        TypeScale.EffectiveFontScale(global::Android.App.Application.Context?.Resources?.Configuration?.FontScale ?? 1f);

    /// <summary>Installs the rule on the text-bearing handlers' mappers.</summary>
    internal static void Install()
    {
        foreach (var property in new[] { TextBlock.FontSizeProperty, TextBlock.FontFamilyProperty, TextBlock.FontWeightProperty, TextBlock.FontStyleProperty, TextBlock.FontStretchProperty, TextBlock.CharacterSpacingProperty, TextBlock.IsTextScaleFactorEnabledProperty, FrameworkElement.StyleProperty, TextBlock.LineHeightProperty })
        {
            TextBlockHandler.Mapper.AppendToMapping(property, (handler, element) =>
            {
                var hadRole = _roled.TryGetValue(element, out _);
                var hasRole = ApplyTextBlock(handler.PlatformView, element, handler.Density);
                if (hasRole && !hadRole)
                {
                    _roled.Add(element, null);
                }
                else if (hadRole && !hasRole)
                {
                    // The role's tracking, line height and weight go: the handler maps the element's own again.
                    _roled.Remove(element);
                    handler.UpdateValue(TextBlock.CharacterSpacingProperty);
                    handler.UpdateValue(TextBlock.LineHeightProperty);
                    handler.UpdateValue(TextBlock.FontWeightProperty);
                }
            });
        }

        foreach (var property in new[] { Control.FontSizeProperty, Control.FontFamilyProperty, Control.FontWeightProperty, Control.FontStyleProperty, Control.FontStretchProperty, Control.CharacterSpacingProperty, Control.IsTextScaleFactorEnabledProperty, ContentControl.ContentProperty })
        {
            ButtonHandler.Mapper.AppendToMapping(property, (handler, element) => ApplyControl(handler.MaterialButton, element, handler.Density));
            CheckBoxHandler.Mapper.AppendToMapping(property, (handler, element) => ApplyControl(handler.PlatformView, element, handler.Density));
            RadioButtonHandler.Mapper.AppendToMapping(property, (handler, element) => ApplyControl(handler.PlatformView, element, handler.Density));
            if (property != ContentControl.ContentProperty)
            {
                TextBoxHandler.Mapper.AppendToMapping(property, (handler, element) => ApplyControl(handler.EditText, element, handler.Density));
            }
        }
    }

    /// <summary>Re-applies the rule to every text widget when the system font scale changed (UI thread).</summary>
    /// <param name="fontScale">The new system font scale.</param>
    internal static void OnFontScaleChanged(float fontScale)
    {
        if (Math.Abs(fontScale - _lastFontScale) < 0.001f)
        {
            return;
        }

        var first = _lastFontScale < 0;
        _lastFontScale = fontScale;
        if (!first)
        {
            Refresh();
        }
    }

    /// <summary>Re-applies the rule to every text widget in every window (after a font-scale change or an override).</summary>
    internal static void Refresh()
    {
        foreach (var root in ThemeRefresh.Roots())
        {
            ThemeRefresh.Walk(root, element =>
            {
                if (element.Handler is IAndroidElementHandler handler)
                {
                    handler.UpdateValue(element is TextBlock ? TextBlock.FontSizeProperty : Control.FontSizeProperty);
                }
            });
        }
    }

    /// <summary>The text size of a TextBlock: its role's size for a framework style (FontSize not set), else FontSize; in sp or dp.</summary>
    /// <param name="view">The text view.</param>
    /// <param name="element">The TextBlock.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>True when a Material type role applies.</returns>
    internal static bool ApplyTextBlock(ATextView view, TextBlock element, double density)
    {
        if (view == null || element == null)
        {
            return false;
        }

        var role = RoleOf(element);
        var fontSize = role != null && !ThemeResources.IsLocal(element, TextBlock.FontSizeProperty) ? role.Size : element.FontSize;
        var size = TypeScale.TextSizePx(fontSize, density, FontScale, element.IsTextScaleFactorEnabled);
        if (Math.Abs(view.TextSize - size) > 0.01f)
        {
            view.SetTextSize(AComplexUnitType.Px, size);
            element.InvalidateMeasure();
        }

        if (role != null)
        {
            if (!ThemeResources.IsLocal(element, TextBlock.CharacterSpacingProperty))
            {
                view.LetterSpacing = (float)role.TrackingEm;
            }

            if (!ThemeResources.IsLocal(element, TextBlock.LineHeightProperty))
            {
                view.LineHeight = Math.Max(1, (int)Math.Round(TypeScale.TextSizePx(role.LineHeight, density, FontScale, element.IsTextScaleFactorEnabled)));
            }

            if (!ThemeResources.IsLocal(element, TextBlock.FontWeightProperty)
                && AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, new FontWeight((ushort)role.Weight), element.FontStyle, element.FontStretch) is { } typeface
                && !ReferenceEquals(view.Typeface, typeface))
            {
                view.Typeface = typeface;
            }
        }

        return role != null;
    }

    /// <summary>The text size of a control's text widget, in sp or dp.</summary>
    /// <param name="view">The text widget (null: nothing to do, e.g. a Button with element content).</param>
    /// <param name="element">The control.</param>
    /// <param name="density">Pixels per DIP.</param>
    internal static void ApplyControl(ATextView view, Control element, double density)
    {
        if (view == null || element == null)
        {
            return;
        }

        var size = TypeScale.TextSizePx(element.FontSize, density, FontScale, element.IsTextScaleFactorEnabled);
        if (Math.Abs(view.TextSize - size) > 0.01f)
        {
            view.SetTextSize(AComplexUnitType.Px, size);
            element.InvalidateMeasure();
        }
    }

    /// <summary>The Material role of a TextBlock styled with a framework TextBlock style, or null.</summary>
    /// <param name="element">The TextBlock.</param>
    /// <returns>The role.</returns>
    internal static TypeRole RoleOf(TextBlock element)
    {
        // Only a TextBlock the app wrote: a template's own TextBlocks keep the Fluent metrics their template is laid
        // out for (a ContentDialog title, a header).
        if (!TypeScale.MapBuiltInStyles || element?.Style is not { } style || element.TemplatedParent != null)
        {
            return null;
        }

        foreach (var key in TypeScale.StyleKeys)
        {
            if (ThemeResources.TryFind(element, key, out var value) && ReferenceEquals(value, style) && !ThemeKeys.IsAppKey(element, key))
            {
                return TypeScale.RoleOf(key);
            }
        }

        return null;
    }
}
#endif
