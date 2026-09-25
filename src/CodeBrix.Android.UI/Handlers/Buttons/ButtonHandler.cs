// Technique from .NET MAUI, src/Core/src/Handlers/Button/ButtonHandler.Android.cs and
// src/Core/src/Platform/Android/MauiMaterialButton.cs @ 828569a864 (zero insets and minimum sizes so the
// cross-platform layout owns the size; listeners allocated once per handler). Copyright (c) .NET
// Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AGravityFlags = global::Android.Views.GravityFlags;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AMaterialShapeDrawable = Google.Android.Material.Shape.MaterialShapeDrawable;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using ARippleDrawable = global::Android.Graphics.Drawables.RippleDrawable;
using ASystemClock = global::Android.OS.SystemClock;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The Button families a <see cref="ButtonHandler"/> serves (their theme keys differ).</summary>
internal enum ButtonFamily
{
    /// <summary>A Button with the default style (Material: tonal button, D-P14).</summary>
    Button,

    /// <summary>A Button with AccentButtonStyle (Material: filled button, D-P14).</summary>
    Accent,

    /// <summary>A ToggleButton (checkable Material button).</summary>
    ToggleButton,

    /// <summary>A RepeatButton.</summary>
    RepeatButton,

    /// <summary>A HyperlinkButton (Material text button).</summary>
    HyperlinkButton,
}

/// <summary>
/// The handler of Button, ToggleButton, RepeatButton and HyperlinkButton (plan 3 rows Button, ButtonBase,
/// ToggleButton, RepeatButton, HyperlinkButton, EmbeddedImageButton; D-P11, D-P14): text content is a
/// Material button (MaterialButton, zero insets and minimum sizes - Core owns the size), element content
/// (StackPanel, Grid, FontIcon, an app control's composed content) is HOSTED as Core content inside a
/// Material shape background with a ripple (CodeBrixContentButton, D-P11). The two switch at run time
/// when the content changes kind (the capabilities follow).
/// </summary>
/// <remarks>
/// Input model: the ButtonBase family's behaviour is CORE's (ClickMode, IsPressed, pointer capture,
/// Command, RepeatButton's Delay/Interval, ToggleButton/three-state cycling, HyperlinkButton navigation):
/// a real touch reaches the native widget first (ripple, pressed state) and then Core, unhandled, which
/// raises Click; injected Core input takes the same Core path. A native click that did NOT come from a
/// touch (accessibility services) is raised through <c>ButtonBase.RaiseClickFromPlatform</c>. Colours
/// follow the element's effective brushes and the family's lightweight-styling keys
/// (<see cref="StateColors"/>, D-O4 "honor").
/// </remarks>
internal sealed class ButtonHandler : ViewGroupHandler<ButtonBase, ButtonHostView>
{
    /// <summary>The Button-family mapper.</summary>
    public static readonly PropertyMapper<ButtonBase, ButtonHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ContentControl.ContentProperty] = MapContent,
        [ContentControl.ContentTemplateProperty] = MapContent,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.BackgroundProperty] = MapColors,
        [Control.ForegroundProperty] = MapColors,
        [Control.BorderBrushProperty] = MapColors,
        [Control.BorderThicknessProperty] = MapBorderThickness,
        [Control.CornerRadiusProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.PaddingProperty] = MapLayout,
        [Control.HorizontalContentAlignmentProperty] = MapLayout,
        [Control.VerticalContentAlignmentProperty] = MapLayout,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
        [Control.FontStretchProperty] = MapFont,
        [Control.CharacterSpacingProperty] = MapFont,
        [ToggleButton.IsCheckedProperty] = MapColors,
    };

    private readonly ButtonFamily _family;
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _foregroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private bool _contentMode;
    private AMaterialButton _button;
    private AMaterialShapeDrawable _shape;
    private long _lastTouchUp;

    /// <summary>Creates the handler of <paramref name="element"/> (its content decides the initial mode).</summary>
    /// <param name="element">The element the handler is created for.</param>
    /// <param name="family">The Button family.</param>
    internal ButtonHandler(UIElement element, ButtonFamily family)
        : base(Mapper)
    {
        _family = family;
        _contentMode = IsElementContent(element as ContentControl);
        _backgroundWatcher = new BrushWatcher(Recolor);
        _foregroundWatcher = new BrushWatcher(Recolor);
        _borderWatcher = new BrushWatcher(Recolor);
    }

    /// <summary>The family whose theme keys colour the widget.</summary>
    internal ButtonFamily Family => _family;

    /// <summary>True while the control's Core content is hosted (element content), false for the Material button.</summary>
    internal bool IsContentMode => _contentMode;

    /// <summary>The Material button of text content (null in content mode).</summary>
    internal AMaterialButton MaterialButton => _button;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => _contentMode
        ? ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.HostsContent | ElementHandlerCapabilities.OwnsChildren
        : ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>
    /// Creates the handler of a Button-family element, or the templated fallback when the element does
    /// not look the way its default style says (<see cref="NativeControlPolicy"/>).
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element)
    {
        switch (element)
        {
            case HyperlinkButton hyperlink when NativeControlPolicy.IsNative(hyperlink, typeof(HyperlinkButton), new[] { "DefaultHyperlinkButtonStyle" }, out _):
                return new ButtonHandler(element, ButtonFamily.HyperlinkButton);
            case RepeatButton repeat when NativeControlPolicy.IsNative(repeat, typeof(RepeatButton), new[] { "DefaultRepeatButtonStyle" }, out _):
                return new ButtonHandler(element, ButtonFamily.RepeatButton);
            case Button button when NativeControlPolicy.IsNative(button, typeof(Button), new[] { "AccentButtonStyle", "DefaultButtonStyle" }, out var style):
                return new ButtonHandler(element, style == "AccentButtonStyle" ? ButtonFamily.Accent : ButtonFamily.Button);
            case ToggleButton toggle when toggle is not CheckBox and not RadioButton
                && NativeControlPolicy.IsNative(toggle, typeof(ToggleButton), new[] { "DefaultToggleButtonStyle" }, out _):
                return new ButtonHandler(element, ButtonFamily.ToggleButton);
            default:
                return new TemplatedFallbackHandler();
        }
    }

    /// <summary>Maps Content / ContentTemplate: the text of the Material button, or a switch between the two modes.</summary>
    public static void MapContent(ButtonHandler handler, ButtonBase element)
    {
        var contentMode = IsElementContent(element);
        if (contentMode != handler._contentMode)
        {
            handler._contentMode = contentMode;
            handler.ApplyMode();
            element.NotifyHandlerCapabilitiesChanged();
        }

        if (!contentMode && handler._button != null)
        {
            var text = element.Content switch
            {
                null => string.Empty,
                string s => s,
                var other => other.ToString(),
            };
            if (!string.Equals(handler._button.Text, text, StringComparison.Ordinal))
            {
                handler._button.Text = text;
            }
        }

        element.InvalidateMeasure();
    }

    /// <summary>Maps IsEnabled (native disabled state: the Disabled colours of the state lists).</summary>
    public static void MapIsEnabled(ButtonHandler handler, ButtonBase element)
    {
        handler.PlatformView.Enabled = element.IsEnabled;
        if (handler._button != null)
        {
            handler._button.Enabled = element.IsEnabled;
        }
    }

    /// <summary>Maps the colours (Background, Foreground, BorderBrush, IsChecked, Style, CornerRadius).</summary>
    public static void MapColors(ButtonHandler handler, ButtonBase element) => handler.Recolor();

    /// <summary>Maps BorderThickness (stroke width; content layout inside the border).</summary>
    public static void MapBorderThickness(ButtonHandler handler, ButtonBase element)
    {
        handler.Recolor();
        element.InvalidateMeasure();
    }

    /// <summary>Maps Padding and the content alignments.</summary>
    public static void MapLayout(ButtonHandler handler, ButtonBase element)
    {
        if (handler._button is { } button)
        {
            var density = handler.Density;
            var padding = element.Padding;
            button.SetPadding(
                LayoutReplayMath.ToPixels(padding.Left, density),
                LayoutReplayMath.ToPixels(padding.Top, density),
                LayoutReplayMath.ToPixels(padding.Right, density),
                LayoutReplayMath.ToPixels(padding.Bottom, density));
            button.Gravity = Gravity(element.HorizontalContentAlignment, element.VerticalContentAlignment);
        }

        element.InvalidateMeasure();
    }

    /// <summary>Maps the font of text content (element content inherits it in Core).</summary>
    public static void MapFont(ButtonHandler handler, ButtonBase element)
    {
        if (handler._button is not { } button)
        {
            return;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        if (typeface != null && !ReferenceEquals(button.Typeface, typeface))
        {
            button.Typeface = typeface;
        }

        button.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        button.LetterSpacing = element.CharacterSpacing / 1000f;
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Element is not ButtonBase element)
        {
            return new Size(0, 0);
        }

        if (!_contentMode)
        {
            return _button == null ? new Size(0, 0) : ViewHandlerExtensions.GetDesiredSizeFromView(_button, availableSize, Density);
        }

        var inner = ContentHostMath.Inner(element.BorderThickness, element.Padding, SafeAreaPadding.Empty);
        if (Content(element) is not { } content)
        {
            return ContentHostMath.Inflate(new Size(0, 0), inner);
        }

        content.Measure(ContentHostMath.Deflate(availableSize, inner));
        return ContentHostMath.Inflate(content.DesiredSize, inner);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (_contentMode && Element is ButtonBase element && Content(element) is { } content)
        {
            var rect = ContentHostMath.ArrangeRect(
                new Size(finalRect.Width, finalRect.Height),
                ContentHostMath.Inner(element.BorderThickness, element.Padding, SafeAreaPadding.Empty),
                content.DesiredSize,
                element.HorizontalContentAlignment,
                element.VerticalContentAlignment);
            content.Arrange(rect);
        }

        base.OnArranged(finalRect, changed);
    }

    /// <inheritdoc />
    protected override ButtonHostView CreatePlatformView()
    {
        var view = new ButtonHostView(Context)
        {
            Focusable = false,
            FocusableInTouchMode = false,
        };
        return view;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(ButtonHostView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Touch += OnNativeTouch;
        platformView.Click += OnNativeClick;
        ApplyMode();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ButtonHostView platformView)
    {
        platformView.Touch -= OnNativeTouch;
        platformView.Click -= OnNativeClick;
        if (_button != null)
        {
            _button.Touch -= OnNativeTouch;
            _button.Click -= OnNativeClick;
        }

        _backgroundWatcher.Clear();
        _foregroundWatcher.Clear();
        _borderWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        Recolor();
    }

    private static bool IsElementContent(ContentControl control) =>
        control != null && (control.Content is UIElement || (control.Content != null && control.Content is not string && control.ContentTemplate != null));

    private static UIElement Content(ContentControl control)
    {
        if (control.ContentTemplateRoot is { } root)
        {
            return root;
        }

        return VisualTreeHelper.GetChildrenCount(control) > 0 ? VisualTreeHelper.GetChild(control, 0) as UIElement : null;
    }

    private static AGravityFlags Gravity(HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var h = horizontal switch
        {
            HorizontalAlignment.Left => AGravityFlags.Start,
            HorizontalAlignment.Right => AGravityFlags.End,
            _ => AGravityFlags.CenterHorizontal,
        };
        var v = vertical switch
        {
            VerticalAlignment.Top => AGravityFlags.Top,
            VerticalAlignment.Bottom => AGravityFlags.Bottom,
            _ => AGravityFlags.CenterVertical,
        };
        return h | v;
    }

    private void ApplyMode()
    {
        var host = PlatformView;
        if (_contentMode)
        {
            if (_button != null)
            {
                _button.Touch -= OnNativeTouch;
                _button.Click -= OnNativeClick;
                host.NativeChild = null;
                _button = null;
            }

            host.Clickable = true;
            _shape ??= new AMaterialShapeDrawable();
            host.Background = _shape;
        }
        else
        {
            host.Background = null;
            host.Foreground = null;
            host.Clickable = false;
            _shape = null;
            if (_button == null)
            {
                _button = CreateMaterialButton();
                _button.Touch += OnNativeTouch;
                _button.Click += OnNativeClick;
                host.NativeChild = _button;
            }
        }

        if (Element is ButtonBase element)
        {
            MapIsEnabled(this, element);
            MapLayout(this, element);
            MapFont(this, element);
            if (!_contentMode && _button != null)
            {
                _button.Text = element.Content switch
                {
                    null => string.Empty,
                    string s => s,
                    var other => other.ToString(),
                };
            }
        }

        Recolor();
    }

    private AMaterialButton CreateMaterialButton()
    {
        var context = MaterialWidgets.Material3(Context);
        var styleAttr = _family switch
        {
            ButtonFamily.HyperlinkButton => MaterialWidgets.AttrId(context, "borderlessButtonStyle"),
            ButtonFamily.Accent => MaterialWidgets.AttrId(context, "materialButtonStyle"),
            _ => MaterialWidgets.AttrId(context, "materialButtonTonalStyle") is var tonal and not 0 ? tonal : MaterialWidgets.AttrId(context, "materialButtonStyle"),
        };
        var button = styleAttr != 0 ? new AMaterialButton(context, null, styleAttr) : new AMaterialButton(context);

        // MAUI's MauiMaterialButton recipe: Core owns the size, so no insets and no minimum sizes.
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
        button.ToggleCheckedStateOnClick = false;
        button.Checkable = _family == ButtonFamily.ToggleButton;
        button.SetIncludeFontPadding(false);
        button.Elevation = 0;
        button.StateListAnimator = null;
        return button;
    }

    private void Recolor()
    {
        if (Element is not ButtonBase element || PlatformView is not { } host)
        {
            return;
        }

        _backgroundWatcher.Watch(element.Background);
        _foregroundWatcher.Watch(element.Foreground);
        _borderWatcher.Watch(element.BorderBrush);

        var isChecked = element is ToggleButton { IsChecked: true };
        var background = Colors(element, element.Background, BackgroundKey(), CheckedKey("Background"), 0);
        var foreground = Colors(element, element.Foreground, ForegroundKey(), CheckedKey("Foreground"), unchecked((int)0xFF000000));
        var border = Colors(element, element.BorderBrush, BorderKey(), CheckedKey("BorderBrush"), 0);
        var density = Density;
        var strokeWidth = MaterialShapes.StrokeWidth(element.BorderThickness, density);
        var shape = MaterialShapes.Model(element.CornerRadius, density);

        if (_button is { } button)
        {
            if (button.Checkable)
            {
                button.Checked = isChecked;
            }

            button.BackgroundTintList = background.ToColorStateList();
            button.SetTextColor(foreground.ToColorStateList());
            button.IconTint = foreground.ToColorStateList();
            button.StrokeColor = border.ToColorStateList();
            button.StrokeWidth = strokeWidth;
            button.ShapeAppearanceModel = shape;
            button.RippleColor = Ripple(foreground.Normal);
            return;
        }

        if (_shape is { } drawable)
        {
            // Content mode: the view group has no checked state of its own - the checked colours are chosen here.
            var fill = isChecked ? background.AsChecked() : background.AsUnchecked();
            var stroke = isChecked ? border.AsChecked() : border.AsUnchecked();
            drawable.ShapeAppearanceModel = shape;
            drawable.FillColor = fill.ToColorStateList();
            drawable.SetStroke(strokeWidth, stroke.ToColorStateList());
            var mask = new AMaterialShapeDrawable(shape) { FillColor = StateColors.Single(unchecked((int)0xFFFFFFFF)) };
            host.Foreground = new ARippleDrawable(Ripple(foreground.Normal), null, mask);
        }
    }

    private static AColorStateList Ripple(int foreground) =>
        StateColors.Single(AColor.Argb(0x33, AColor.GetRedComponent(foreground), AColor.GetGreenComponent(foreground), AColor.GetBlueComponent(foreground)));

    private static StateColors Colors(ButtonBase element, Brush brush, string key, string checkedKey, int fallback)
    {
        var colors = StateColors.FromKeys(element, key, fallback, checkedKey);
        var actual = ThemeResources.ColorOf(brush) ?? 0;
        if (actual != colors.Normal)
        {
            // The app's own brush (local value, app style or binding) wins over the theme's hover/pressed keys.
            colors = colors.WithNormalEverywhere(actual);
        }

        return colors;
    }

    private string Prefix() => _family switch
    {
        ButtonFamily.Accent => "AccentButton",
        ButtonFamily.ToggleButton => "ToggleButton",
        ButtonFamily.RepeatButton => "RepeatButton",
        ButtonFamily.HyperlinkButton => "HyperlinkButton",
        _ => "Button",
    };

    private string BackgroundKey() => Prefix() + "Background";

    private string ForegroundKey() => Prefix() + "Foreground";

    private string BorderKey() => Prefix() + "BorderBrush";

    private string CheckedKey(string part) => _family == ButtonFamily.ToggleButton ? "ToggleButton" + part + "Checked" : null;

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
        // A click right after a touch is Core's (it saw the same touch); any other native click comes from
        // an accessibility service or a key the native view handled, and is raised in Core here.
        if (ASystemClock.UptimeMillis() - _lastTouchUp < 1000)
        {
            return;
        }

        (Element as ButtonBase)?.RaiseClickFromPlatform();
    }
}
