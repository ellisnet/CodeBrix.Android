using System;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Decides whether a control is shown by its NATIVE widget or keeps its Fluent template (the templated
/// fallback). Native when the control looks the way its default style says: it is not a part of another
/// control's template (a ScrollBar's RepeatButton, a SplitButton's halves keep the look their parent's
/// template gives them), its default style key is the family's own (an app subclass such as
/// <c>EmbeddedImageButton : Button</c> with <c>DefaultStyleKey = typeof(Button)</c> qualifies; a
/// framework subclass with a style of its own - AppBarButton, DropDownButton - does not), it has no
/// local Template, and no Style in its chain sets a Template - except the Fluent styles the native
/// widget reproduces itself (DefaultButtonStyle, AccentButtonStyle, ...), which are recognised by
/// identity with the theme's resources.
/// </summary>
internal static class NativeControlPolicy
{
    /// <summary>
    /// True when <paramref name="control"/> should be shown by the native widget of the family whose
    /// default style key is <paramref name="familyKey"/>.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="familyKey">The family's type (typeof(Button), typeof(CheckBox), ...).</param>
    /// <param name="knownStyleKeys">The Fluent style keys the native widget reproduces (e.g. "DefaultButtonStyle", "AccentButtonStyle").</param>
    /// <param name="matchedStyleKey">The known style key found in the control's style chain, or null.</param>
    /// <returns>True for the native widget.</returns>
    internal static bool IsNative(Control control, Type familyKey, string[] knownStyleKeys, out string matchedStyleKey)
    {
        matchedStyleKey = null;
        if (control == null)
        {
            return false;
        }

        if (control.GetTemplatedParent() != null)
        {
            return false;
        }

        var key = DefaultStyleKey(control) ?? control.GetType();
        if (key != familyKey)
        {
            return false;
        }

        if (control.ReadLocalValue(Control.TemplateProperty) != DependencyProperty.UnsetValue)
        {
            return false;
        }

        for (var style = control.Style; style != null; style = style.BasedOn)
        {
            if (Match(control, style, knownStyleKeys) is { } known)
            {
                matchedStyleKey ??= known;
                return true;
            }

            if (SetsTemplate(style))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when a style (or one it is based on) is the theme resource <paramref name="key"/>.</summary>
    /// <param name="control">The control (its resource scope).</param>
    /// <param name="key">The style key.</param>
    /// <returns>True when the control's style chain includes it.</returns>
    internal static bool HasStyle(Control control, string key)
    {
        if (control?.Style == null || !ThemeResources.TryFind(control, key, out var value) || value is not Style known)
        {
            return false;
        }

        for (var style = control.Style; style != null; style = style.BasedOn)
        {
            if (ReferenceEquals(style, known))
            {
                return true;
            }
        }

        return false;
    }

    // Control.DefaultStyleKey is protected and not stored in its dependency property in Core: read it through
    // the property (kept: Core itself reads it to find the default style).
    private static readonly PropertyInfo _defaultStyleKey =
        typeof(Control).GetProperty("DefaultStyleKey", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    /// <summary>The control's default style key (the type whose default style it uses), or null.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The key type, or null.</returns>
    [System.Diagnostics.CodeAnalysis.DynamicDependency("get_DefaultStyleKey", typeof(Control))]
    internal static Type DefaultStyleKey(Control control)
    {
        if (control.GetValue(Control.DefaultStyleKeyProperty) is Type fromProperty)
        {
            return fromProperty;
        }

        try
        {
            return _defaultStyleKey?.GetValue(control) as Type;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static string Match(Control control, Style style, string[] knownStyleKeys)
    {
        if (knownStyleKeys == null)
        {
            return null;
        }

        foreach (var key in knownStyleKeys)
        {
            if (ThemeResources.TryFind(control, key, out var value) && ReferenceEquals(value, style))
            {
                return key;
            }
        }

        return null;
    }

    private static bool SetsTemplate(Style style)
    {
        foreach (var setterBase in style.Setters)
        {
            if (setterBase is Setter { Property: { } property } && ReferenceEquals(property, Control.TemplateProperty))
            {
                return true;
            }
        }

        return false;
    }
}
