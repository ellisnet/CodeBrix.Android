// Technique from .NET MAUI, src/Core/src/Handlers/Button/ButtonHandler.Android.cs and
// src/Core/src/Platform/Android/MauiMaterialButton.cs @ 828569a864 (a MaterialButton with zero insets and minimum
// sizes so the cross-platform layout owns the size; the icon placed by the button's icon gravity). Copyright (c)
// .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using ASystemClock = global::Android.OS.SystemClock;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the handler of an AppBarButton / AppBarToggleButton OUTSIDE a CommandBar (tsv rows AppBarButton,
/// AppBarToggleButton: "icon MaterialButton when inline"; inside a native CommandBar the button is a menu item of the
/// app bar, <see cref="CommandBarHandler"/>). A Material text button with its icon above its label (the WinUI
/// layout; icon only when IsCompact or LabelPosition Collapsed, the label then being the tooltip and content
/// description); an AppBarToggleButton is a checkable Material button. Input model of the ButtonBase family (Core
/// acts: a real touch shows the ripple and reaches Core, which raises Click, runs the Command and toggles; a native
/// click no touch caused is raised through RaiseClickFromPlatform). A button inside a CommandBar that keeps its
/// Fluent template, or re-templated by the application, keeps the templated fallback.
/// </summary>
internal sealed class AppBarButtonHandler : ViewGroupHandler<ButtonBase, ButtonHostView>
{
    /// <summary>The mapper.</summary>
    public static readonly PropertyMapper<ButtonBase, AppBarButtonHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [AppBarButton.LabelProperty] = MapContent,
        [AppBarButton.IconProperty] = MapContent,
        [AppBarButton.IsCompactProperty] = MapContent,
        [AppBarButton.LabelPositionProperty] = MapContent,
        [AppBarToggleButton.LabelProperty] = MapContent,
        [AppBarToggleButton.IconProperty] = MapContent,
        [AppBarToggleButton.IsCompactProperty] = MapContent,
        [AppBarToggleButton.LabelPositionProperty] = MapContent,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.BackgroundProperty] = MapColors,
        [Control.ForegroundProperty] = MapColors,
        [ToggleButton.IsCheckedProperty] = MapColors,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
    };

    private readonly BrushWatcher _watcher;
    private AMaterialButton _button;
    private long _lastTouchUp;

    /// <summary>Creates the handler.</summary>
    public AppBarButtonHandler()
        : base(Mapper)
    {
        _watcher = new BrushWatcher(() => Recolor());
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The Material button (null while disconnected).</summary>
    internal AMaterialButton MaterialButton => _button;

    /// <summary>The native handler for a stand-alone app bar button with its default look, else the templated fallback.</summary>
    /// <param name="element">The AppBarButton or AppBarToggleButton.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element)
    {
        if (element is not (AppBarButton or AppBarToggleButton) || InsideCommandBar(element))
        {
            return new TemplatedFallbackHandler();
        }

        return NativeControlPolicy.IsNative((Control)element, element is AppBarToggleButton ? typeof(AppBarToggleButton) : typeof(AppBarButton), Array.Empty<string>(), out _)
            ? new AppBarButtonHandler()
            : new TemplatedFallbackHandler();
    }

    /// <summary>Maps Label / Icon / IsCompact / LabelPosition.</summary>
    public static void MapContent(AppBarButtonHandler handler, ButtonBase element)
    {
        handler.Refill();
        element.InvalidateMeasure();
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(AppBarButtonHandler handler, ButtonBase element)
    {
        if (handler._button != null)
        {
            handler._button.Enabled = element.IsEnabled;
        }
    }

    /// <summary>Maps the colours and the checked state.</summary>
    public static void MapColors(AppBarButtonHandler handler, ButtonBase element) => handler.Recolor();

    /// <summary>Maps the label font.</summary>
    public static void MapFont(AppBarButtonHandler handler, ButtonBase element)
    {
        if (handler._button is not { } button)
        {
            return;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        if (typeface != null)
        {
            button.Typeface = typeface;
        }

        button.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        _button == null ? new Size(0, 0) : ViewHandlerExtensions.GetDesiredSizeFromView(_button, availableSize, Density);

    /// <inheritdoc />
    protected override ButtonHostView CreatePlatformView() => new(Context) { Focusable = false, FocusableInTouchMode = false };

    /// <inheritdoc />
    protected override void ConnectHandler(ButtonHostView platformView)
    {
        base.ConnectHandler(platformView);
        var context = MaterialWidgets.Material3(Context);
        var style = MaterialWidgets.AttrId(context, "borderlessButtonStyle");
        _button = style != 0 ? new AMaterialButton(context, null, style) : new AMaterialButton(context);
        _button.InsetTop = 0;
        _button.InsetBottom = 0;
        _button.SetMinWidth(0);
        _button.SetMinHeight(0);
        _button.SetMinimumWidth(0);
        _button.SetMinimumHeight(0);
        _button.SetAllCaps(false);
        _button.SoundEffectsEnabled = false;
        _button.Focusable = false;
        _button.FocusableInTouchMode = false;
        _button.ToggleCheckedStateOnClick = false;
        _button.Checkable = Element is AppBarToggleButton;
        _button.IconGravity = AMaterialButton.IconGravityTop;
        _button.IconPadding = MaterialWidgets.Px(4, Density);
        _button.SetIncludeFontPadding(false);
        _button.Touch += OnNativeTouch;
        _button.Click += OnNativeClick;
        platformView.NativeChild = _button;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ButtonHostView platformView)
    {
        if (_button != null)
        {
            _button.Touch -= OnNativeTouch;
            _button.Click -= OnNativeClick;
            platformView.NativeChild = null;
            _button = null;
        }

        _watcher.Clear();
        base.DisconnectHandler(platformView);
    }

    private static bool InsideCommandBar(UIElement element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is CommandBar or CommandBarOverflowPresenter)
            {
                return true;
            }
        }

        return false;
    }

    private static (string Label, IconElement Icon, bool Compact) Read(ButtonBase element) => element switch
    {
        AppBarButton b => (b.Label ?? string.Empty, b.Icon, b.IsCompact || b.LabelPosition == CommandBarLabelPosition.Collapsed),
        AppBarToggleButton t => (t.Label ?? string.Empty, t.Icon, t.IsCompact || t.LabelPosition == CommandBarLabelPosition.Collapsed),
        _ => (string.Empty, null, false),
    };

    private void Refill()
    {
        if (_button is not { } button || Element is not ButtonBase element)
        {
            return;
        }

        var (label, icon, compact) = Read(element);
        button.Text = compact && icon != null ? string.Empty : label;
        button.ContentDescription = label;
        button.TooltipText = label;
        Recolor();
    }

    private void Recolor()
    {
        if (_button is not { } button || Element is not ButtonBase element)
        {
            return;
        }

        var toggle = element is AppBarToggleButton;
        var isChecked = element is ToggleButton { IsChecked: true };
        var prefix = toggle ? "AppBarToggleButton" : "AppBarButton";
        _watcher.Watch(element.Foreground);
        var foreground = StateColors.FromKeys(element, prefix + "Foreground", unchecked((int)0xFF1B1B1F), toggle ? prefix + "ForegroundChecked" : null);
        var background = StateColors.FromKeys(element, prefix + "Background", 0, toggle ? prefix + "BackgroundChecked" : null);
        if (ThemeResources.ColorOf(element.Foreground) is int fore && fore != foreground.Normal)
        {
            foreground = foreground.WithNormalEverywhere(fore);
        }

        if (ThemeResources.ColorOf(element.Background) is int back && back != background.Normal)
        {
            background = background.WithNormalEverywhere(back);
        }

        if (button.Checkable)
        {
            button.Checked = isChecked;
        }

        button.BackgroundTintList = background.ToColorStateList();
        button.SetTextColor(foreground.ToColorStateList());
        var current = isChecked ? foreground.AsChecked().Normal : foreground.Normal;
        button.Icon = Read(element).Icon is { } icon ? IconDrawables.Create(icon, Context, Density, current, 20) : null;
        button.IconTint = foreground.ToColorStateList();
        button.RippleColor = AColorStateList.ValueOf(new AColor(AColor.Argb(0x33, AColor.GetRedComponent(current), AColor.GetGreenComponent(current), AColor.GetBlueComponent(current))));
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
        if (ASystemClock.UptimeMillis() - _lastTouchUp < 1000)
        {
            return;
        }

        (Element as ButtonBase)?.RaiseClickFromPlatform();
    }
}
