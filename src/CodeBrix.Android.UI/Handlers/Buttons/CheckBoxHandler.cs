// Technique from .NET MAUI, src/Core/src/Handlers/CheckBox/CheckBoxHandler.Android.cs and
// src/Core/src/Handlers/RadioButton/RadioButtonHandler2.Android.cs @ 828569a864 (Material compound
// buttons created through a Material 3 themed context; the checked state written with a reentrancy
// guard). Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See
// THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using ACanvas = global::Android.Graphics.Canvas;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using ACompoundButton = global::Android.Widget.CompoundButton;
using AContext = global::Android.Content.Context;
using AMaterialCheckBox = Google.Android.Material.CheckBox.MaterialCheckBox;
using AMaterialRadioButton = Google.Android.Material.RadioButton.MaterialRadioButton;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using AResource = global::Android.Resource;
using ASystemClock = global::Android.OS.SystemClock;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>A MaterialCheckBox that reports where it drew its box (for <see cref="NativeTemplateParts"/>).</summary>
internal sealed class CodeBrixCheckBox : AMaterialCheckBox
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal CodeBrixCheckBox(AContext context)
        : base(context)
    {
    }

    /// <summary>Called after every draw with the button drawable's bounds (pixels, view coordinates).</summary>
    internal Action<global::Android.Graphics.Rect> Drawn { get; set; }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (Drawn != null && ButtonDrawable is { } drawable)
        {
            Drawn(drawable.Bounds);
        }
    }
}

/// <summary>A MaterialRadioButton that reports where it drew its circle.</summary>
internal sealed class CodeBrixRadioButton : AMaterialRadioButton
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal CodeBrixRadioButton(AContext context)
        : base(context)
    {
    }

    /// <summary>Called after every draw with the button drawable's bounds.</summary>
    internal Action<global::Android.Graphics.Rect> Drawn { get; set; }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (Drawn != null && ButtonDrawable is { } drawable)
        {
            Drawn(drawable.Bounds);
        }
    }
}

/// <summary>
/// The base of the CheckBox and RadioButton handlers: a Material compound button (box or circle plus
/// the content text). The toggle behaviour is CORE's (IsThreeState cycling, GroupName exclusivity,
/// Checked/Unchecked/Indeterminate): a touch reaches the native widget first and then Core, which
/// toggles; the checked state Core ends with is written back to the widget (it may have toggled itself
/// on the same touch). A click not caused by a touch (accessibility) is raised through
/// <c>ButtonBase.RaiseClickFromPlatform</c>.
/// </summary>
/// <typeparam name="TControl">CheckBox or RadioButton.</typeparam>
/// <typeparam name="TView">The Material widget.</typeparam>
internal abstract class CompoundButtonHandler<TControl, TView> : ViewHandler<TControl, TView>
    where TControl : ToggleButton
    where TView : ACompoundButton
{
    private readonly BrushWatcher _foregroundWatcher;
    private long _lastTouchUp;
    private bool _updating;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The mapper.</param>
    protected CompoundButtonHandler(IPropertyMapper mapper)
        : base(mapper)
    {
        _foregroundWatcher = new BrushWatcher(Recolor);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The part stand-ins of the control.</summary>
    protected NativeTemplateParts Parts { get; private set; }

    /// <summary>Maps Content (the text beside the box).</summary>
    public static void MapContent(CompoundButtonHandler<TControl, TView> handler, TControl element)
    {
        var text = element.Content switch
        {
            null => string.Empty,
            string s => s,
            TextBlock block => block.Text ?? string.Empty,
            UIElement => string.Empty,
            var other => other.ToString(),
        };
        if (!string.Equals(handler.PlatformView.Text, text, StringComparison.Ordinal))
        {
            handler.PlatformView.Text = text;
        }

        element.InvalidateMeasure();
    }

    /// <summary>Maps IsChecked (written with a reentrancy guard).</summary>
    public static void MapIsChecked(CompoundButtonHandler<TControl, TView> handler, TControl element)
    {
        handler._updating = true;
        try
        {
            handler.ApplyChecked(element.IsChecked);
        }
        finally
        {
            handler._updating = false;
        }

        handler.PlatformView.JumpDrawablesToCurrentState();
        handler.Recolor();
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(CompoundButtonHandler<TControl, TView> handler, TControl element) =>
        handler.PlatformView.Enabled = element.IsEnabled;

    /// <summary>Maps Foreground, the theme keys and Style (the colours).</summary>
    public static void MapColors(CompoundButtonHandler<TControl, TView> handler, TControl element) => handler.Recolor();

    /// <summary>Maps the font of the content text.</summary>
    public static void MapFont(CompoundButtonHandler<TControl, TView> handler, TControl element)
    {
        var view = handler.PlatformView;
        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        if (typeface != null && !ReferenceEquals(view.Typeface, typeface))
        {
            view.Typeface = typeface;
        }

        view.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        view.LetterSpacing = element.CharacterSpacing / 1000f;
        element.InvalidateMeasure();
    }

    /// <summary>Maps Padding (around the content text).</summary>
    public static void MapPadding(CompoundButtonHandler<TControl, TView> handler, TControl element)
    {
        var density = handler.Density;
        var padding = element.Padding;
        handler.PlatformView.SetPadding(
            LayoutReplayMath.ToPixels(padding.Left, density),
            LayoutReplayMath.ToPixels(padding.Top, density),
            LayoutReplayMath.ToPixels(padding.Right, density),
            LayoutReplayMath.ToPixels(padding.Bottom, density));
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <summary>Writes Core's checked state to the widget.</summary>
    /// <param name="isChecked">True, false, or null (indeterminate).</param>
    protected abstract void ApplyChecked(bool? isChecked);

    /// <summary>Applies the colours (theme keys and the element's brushes).</summary>
    protected abstract void ApplyColors(TControl element);

    /// <summary>Publishes the part stand-ins from the drawn button drawable's bounds.</summary>
    /// <param name="bounds">The drawable bounds in view pixels.</param>
    protected abstract void PublishParts(global::Android.Graphics.Rect bounds);

    /// <summary>A rectangle in view pixels as element DIPs.</summary>
    protected Rect ToDips(float left, float top, float right, float bottom)
    {
        var density = Density;
        return new Rect(left / density, top / density, Math.Max(0, right - left) / density, Math.Max(0, bottom - top) / density);
    }

    /// <summary>Called by the widget after it drew.</summary>
    protected void OnDrawn(global::Android.Graphics.Rect bounds)
    {
        if (NativeTemplateParts.Enabled && Element != null)
        {
            PublishParts(bounds);
        }
    }

    /// <inheritdoc />
    protected override void ConnectHandler(TView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Touch += OnNativeTouch;
        platformView.Click += OnNativeClick;
        Parts = Element != null ? NativeTemplateParts.For(Element) : null;
        DeclareParts();
    }

    /// <summary>Declares the control's part stand-ins (before Core's first layout of it).</summary>
    protected virtual void DeclareParts()
    {
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TView platformView)
    {
        platformView.Touch -= OnNativeTouch;
        platformView.Click -= OnNativeClick;
        _foregroundWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (NativeTemplateParts.Enabled && Parts != null && PlatformView is { } view)
        {
            // Before the first draw the parts come from the laid-out widget: the button drawable sits at the
            // start, centred vertically (CompoundButton's own placement); the draw refines them.
            NativeLayoutNow.Ensure(view, new Size(finalRect.Width, finalRect.Height), Density);
            if (view.ButtonDrawable is { } drawable && drawable.Bounds.IsEmpty)
            {
                var w = drawable.IntrinsicWidth;
                var h = drawable.IntrinsicHeight;
                var top = (view.Height - h) / 2;
                PublishParts(new global::Android.Graphics.Rect(0, top, w, top + h));
            }
        }

        Parts?.Arrange();
    }

    /// <summary>Configures a freshly created widget: Core owns size, focus and the toggle logic.</summary>
    protected static void Configure(ACompoundButton view)
    {
        view.SetMinWidth(0);
        view.SetMinHeight(0);
        view.SetMinimumWidth(0);
        view.SetMinimumHeight(0);
        view.Focusable = false;
        view.FocusableInTouchMode = false;
        view.SoundEffectsEnabled = false;
        view.SetIncludeFontPadding(false);
    }

    /// <summary>A state list for the compound button's box: disabled, indeterminate (optional), checked, unchecked.</summary>
    protected static AColorStateList ButtonTint(int off, int on, int? indeterminate, int? disabled, int indeterminateState)
    {
        var enabled = AResource.Attribute.StateEnabled;
        var checkedState = AResource.Attribute.StateChecked;
        if (indeterminate is { } mixed && indeterminateState != 0)
        {
            return new AColorStateList(
                new[] { new[] { -enabled }, new[] { indeterminateState }, new[] { checkedState }, Array.Empty<int>() },
                new[] { disabled ?? off, mixed, on, off });
        }

        return new AColorStateList(
            new[] { new[] { -enabled }, new[] { checkedState }, Array.Empty<int>() },
            new[] { disabled ?? off, on, off });
    }

    private void Recolor()
    {
        if (Element is TControl element && PlatformView != null)
        {
            _foregroundWatcher.Watch(element.Foreground);
            ApplyColors(element);
        }
    }

    private void OnNativeTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        if (e.Event is { } motion && (motion.ActionMasked == AMotionEventActions.Up || motion.ActionMasked == AMotionEventActions.Cancel))
        {
            _lastTouchUp = ASystemClock.UptimeMillis();
        }
    }

    private void OnNativeClick(object sender, EventArgs e)
    {
        if (_updating || Element is not TControl element)
        {
            return;
        }

        if (ASystemClock.UptimeMillis() - _lastTouchUp >= 1000)
        {
            element.RaiseClickFromPlatform();
        }

        // Core decides the state (it saw the touch too); the widget may have toggled itself already.
        PlatformView.Post(() =>
        {
            if (Element is TControl current)
            {
                MapIsChecked(this, current);
            }
        });
    }
}

/// <summary>
/// The CheckBox handler (plan 3 row CheckBox): MaterialCheckBox, IsThreeState -> the indeterminate
/// checked state; colours from the CheckBox lightweight-styling keys (CheckBoxCheckBackgroundFill*,
/// CheckBoxCheckBackgroundStroke*, CheckBoxCheckGlyphForeground*, CheckBoxForeground*).
/// </summary>
internal sealed class CheckBoxHandler : CompoundButtonHandler<CheckBox, CodeBrixCheckBox>
{
    /// <summary>CheckBox's mapper.</summary>
    public static readonly PropertyMapper<CheckBox, CheckBoxHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ContentControl.ContentProperty] = MapContent,
        [ToggleButton.IsCheckedProperty] = MapIsChecked,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
        [Control.FontStretchProperty] = MapFont,
        [Control.CharacterSpacingProperty] = MapFont,
        [Control.PaddingProperty] = MapPadding,
    };

    /// <summary>Creates the handler.</summary>
    public CheckBoxHandler()
        : base(Mapper)
    {
    }

    /// <summary>The CheckBox handler, or the templated fallback for a CheckBox with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is CheckBox box && NativeControlPolicy.IsNative(box, typeof(CheckBox), new[] { "DefaultCheckBoxStyle" }, out _)
            ? new CheckBoxHandler()
            : new TemplatedFallbackHandler();

    /// <inheritdoc />
    protected override CodeBrixCheckBox CreatePlatformView()
    {
        var view = new CodeBrixCheckBox(MaterialWidgets.Material3(Context));
        Configure(view);
        view.UseMaterialThemeColors = false;
        view.Drawn = OnDrawn;
        return view;
    }

    /// <inheritdoc />
    protected override void DeclareParts() => Parts?.Declare("NormalRectangle", "CheckGlyph");

    /// <inheritdoc />
    protected override void ApplyChecked(bool? isChecked) =>
        PlatformView.CheckedState = isChecked switch
        {
            true => AMaterialCheckBox.StateChecked,
            false => AMaterialCheckBox.StateUnchecked,
            null => AMaterialCheckBox.StateIndeterminate,
        };

    /// <inheritdoc />
    protected override void ApplyColors(CheckBox element)
    {
        var view = PlatformView;
        var indeterminateState = MaterialWidgets.AttrId(view.Context, "state_indeterminate");
        var fillChecked = ThemeResources.FindColor(element, "CheckBoxCheckBackgroundFillChecked") ?? unchecked((int)0xFF0078D4);
        var strokeUnchecked = ThemeResources.FindColor(element, "CheckBoxCheckBackgroundStrokeUnchecked") ?? unchecked((int)0xFF808080);
        var fillIndeterminate = ThemeResources.FindColor(element, "CheckBoxCheckBackgroundFillIndeterminate") ?? fillChecked;
        var disabled = ThemeResources.FindColor(element, "CheckBoxCheckBackgroundStrokeUncheckedDisabled");
        view.ButtonTintList = ButtonTint(strokeUnchecked, fillChecked, fillIndeterminate, disabled, indeterminateState);

        var glyph = ThemeResources.FindColor(element, "CheckBoxCheckGlyphForegroundChecked") ?? unchecked((int)0xFFFFFFFF);
        var glyphMixed = ThemeResources.FindColor(element, "CheckBoxCheckGlyphForegroundIndeterminate") ?? glyph;
        view.ButtonIconTintList = ButtonTint(glyph, glyph, glyphMixed, ThemeResources.FindColor(element, "CheckBoxCheckGlyphForegroundCheckedDisabled"), indeterminateState);

        var foreground = StateColors.FromKeys(element, "CheckBoxForegroundUnchecked", unchecked((int)0xFF000000));
        if (ThemeResources.ColorOf(element.Foreground) is { } actual && actual != foreground.Normal)
        {
            foreground = foreground.WithNormalEverywhere(actual);
        }

        view.SetTextColor(foreground.ToColorStateList());

        // The re-keyed CheckBox family's full state lists (presentation policy, AP5).
        Policy.ThemeKeyAppliers.CheckBox(this, element);
    }

    /// <inheritdoc />
    protected override void PublishParts(global::Android.Graphics.Rect bounds)
    {
        // The Material box is drawn inside its drawable with a margin: the visible box is 18 dp square, centred.
        var density = Density;
        var box = (float)(18 * density);
        var cx = bounds.ExactCenterX();
        var cy = bounds.ExactCenterY();
        var half = Math.Min(box, Math.Min(bounds.Width(), bounds.Height())) / 2f;
        var rect = ToDips(cx - half, cy - half, cx + half, cy + half);
        Parts.Set("NormalRectangle", rect);
        Parts.Set("CheckGlyph", rect);
    }
}

/// <summary>
/// The RadioButton handler (plan 3 row RadioButton): MaterialRadioButton; GroupName exclusivity stays
/// Core's; colours from the RadioButton keys (RadioButtonOuterEllipse*, RadioButtonCheckGlyph*,
/// RadioButtonForeground*).
/// </summary>
internal sealed class RadioButtonHandler : CompoundButtonHandler<RadioButton, CodeBrixRadioButton>
{
    /// <summary>RadioButton's mapper.</summary>
    public static readonly PropertyMapper<RadioButton, RadioButtonHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ContentControl.ContentProperty] = MapContent,
        [ToggleButton.IsCheckedProperty] = MapIsChecked,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
        [Control.FontStretchProperty] = MapFont,
        [Control.CharacterSpacingProperty] = MapFont,
        [Control.PaddingProperty] = MapPadding,
    };

    /// <summary>Creates the handler.</summary>
    public RadioButtonHandler()
        : base(Mapper)
    {
    }

    /// <summary>The RadioButton handler, or the templated fallback for a RadioButton with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is RadioButton radio && NativeControlPolicy.IsNative(radio, typeof(RadioButton), new[] { "DefaultRadioButtonStyle" }, out _)
            ? new RadioButtonHandler()
            : new TemplatedFallbackHandler();

    /// <inheritdoc />
    protected override CodeBrixRadioButton CreatePlatformView()
    {
        var view = new CodeBrixRadioButton(MaterialWidgets.Material3(Context));
        Configure(view);
        view.UseMaterialThemeColors = false;
        view.Drawn = OnDrawn;
        return view;
    }

    /// <inheritdoc />
    protected override void ApplyChecked(bool? isChecked) => PlatformView.Checked = isChecked == true;

    /// <inheritdoc />
    protected override void ApplyColors(RadioButton element)
    {
        var view = PlatformView;
        var on = ThemeResources.FindColor(element, "RadioButtonOuterEllipseCheckedFill") ?? unchecked((int)0xFF0078D4);
        var off = ThemeResources.FindColor(element, "RadioButtonOuterEllipseStroke") ?? unchecked((int)0xFF808080);
        view.ButtonTintList = ButtonTint(off, on, null, ThemeResources.FindColor(element, "RadioButtonOuterEllipseStrokeDisabled"), 0);

        var foreground = StateColors.FromKeys(element, "RadioButtonForeground", unchecked((int)0xFF000000));
        if (ThemeResources.ColorOf(element.Foreground) is { } actual && actual != foreground.Normal)
        {
            foreground = foreground.WithNormalEverywhere(actual);
        }

        view.SetTextColor(foreground.ToColorStateList());
    }

    /// <inheritdoc />
    protected override void PublishParts(global::Android.Graphics.Rect bounds)
    {
        // No scenario addresses a RadioButton's template parts: none are published (no children added).
    }
}
