// Technique from .NET MAUI, src/Core/src/Handlers/Switch/SwitchHandler2.Android.cs @ 828569a864
// (MaterialSwitch; IsOn written back from the checked-change listener with a reentrancy guard; track and
// thumb colours as tint lists). Copyright (c) .NET Foundation and Contributors. Licensed under the MIT
// License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using ACanvas = global::Android.Graphics.Canvas;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AGravityFlags = global::Android.Views.GravityFlags;
using AMaterialSwitch = Google.Android.Material.MaterialSwitch.MaterialSwitch;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AResource = global::Android.Resource;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>A MaterialSwitch that reports where it drew its thumb and track.</summary>
internal sealed class CodeBrixSwitch : AMaterialSwitch
{
    /// <summary>Creates the switch.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal CodeBrixSwitch(AContext context)
        : base(context)
    {
    }

    /// <summary>Called after every draw with the thumb and track drawable bounds (switch pixels).</summary>
    internal Action<global::Android.Graphics.Rect, global::Android.Graphics.Rect> Drawn { get; set; }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (Drawn != null && ThumbDrawable is { } thumb && TrackDrawable is { } track)
        {
            Drawn(thumb.Bounds, track.Bounds);
        }
    }
}

/// <summary>
/// The native view of a ToggleSwitch (plan 3 row ToggleSwitch: CodeBrixToggleSwitchView = Header
/// TextView + MaterialSwitch + On/OffContent TextView): the header above, then the switch with the
/// On/Off content beside it, as the Fluent template lays them out. A tap anywhere on the switch row
/// toggles, as the Fluent SwitchAreaGrid does.
/// </summary>
internal sealed class ToggleSwitchView : AViewGroup
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal ToggleSwitchView(AContext context)
        : base(context)
    {
        Header = new ATextView(context) { Visibility = AViewStates.Gone };
        Header.SetIncludeFontPadding(false);
        Switch = new CodeBrixSwitch(context);
        Switch.SetMinWidth(0);
        Switch.SetMinHeight(0);
        Switch.SetMinimumWidth(0);
        Switch.SetMinimumHeight(0);
        Switch.Focusable = false;
        Switch.FocusableInTouchMode = false;
        Switch.SoundEffectsEnabled = false;
        Switch.ShowText = false;
        Switch.SetPadding(0, 0, 0, 0);
        Content = new ATextView(context) { Gravity = AGravityFlags.CenterVertical | AGravityFlags.Start };
        Content.SetIncludeFontPadding(false);
        AddView(Header);
        AddView(Switch);
        AddView(Content);
        Clickable = true;
        Focusable = false;
        FocusableInTouchMode = false;
        SoundEffectsEnabled = false;
    }

    /// <summary>The header text (Gone without a header).</summary>
    internal ATextView Header { get; }

    /// <summary>The Material switch.</summary>
    internal CodeBrixSwitch Switch { get; }

    /// <summary>The On/Off content text.</summary>
    internal ATextView Content { get; }

    /// <summary>The gap between the switch and its content, in pixels.</summary>
    internal int ContentGap { get; set; }

    /// <summary>The gap below the header, in pixels.</summary>
    internal int HeaderGap { get; set; }

    /// <summary>Where the switch row (switch + content) is, in view pixels, after the last layout.</summary>
    internal global::Android.Graphics.Rect RowBounds { get; } = new();

    /// <inheritdoc />
    public override bool PerformClick()
    {
        // A tap on the row, not on the switch itself: toggle like the Fluent SwitchAreaGrid.
        base.PerformClick();
        if (Enabled)
        {
            Switch.Toggle();
        }

        return true;
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var unspecified = AMeasureSpecMode.Unspecified.MakeMeasureSpec(0);
        var headerHeight = 0;
        var headerWidth = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Measure(unspecified, unspecified);
            headerHeight = Header.MeasuredHeight + HeaderGap;
            headerWidth = Header.MeasuredWidth;
        }

        Switch.Measure(unspecified, unspecified);
        Content.Measure(unspecified, unspecified);
        var rowWidth = Switch.MeasuredWidth + ContentGap + Content.MeasuredWidth;
        var rowHeight = Math.Max(Switch.MeasuredHeight, Content.MeasuredHeight);
        var width = Math.Max(headerWidth, rowWidth);
        var height = headerHeight + rowHeight;
        SetMeasuredDimension(ResolveSize(width, widthMeasureSpec), ResolveSize(height, heightMeasureSpec));
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var top = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Layout(0, 0, Header.MeasuredWidth, Header.MeasuredHeight);
            top = Header.MeasuredHeight + HeaderGap;
        }

        var rowHeight = Math.Max(Switch.MeasuredHeight, Content.MeasuredHeight);
        var switchTop = top + ((rowHeight - Switch.MeasuredHeight) / 2);
        Switch.Layout(0, switchTop, Switch.MeasuredWidth, switchTop + Switch.MeasuredHeight);
        var contentLeft = Switch.MeasuredWidth + ContentGap;
        var contentTop = top + ((rowHeight - Content.MeasuredHeight) / 2);
        Content.Layout(contentLeft, contentTop, Math.Max(contentLeft, r - l), contentTop + Content.MeasuredHeight);
        RowBounds.Set(0, top, r - l, top + rowHeight);
    }
}

/// <summary>
/// The ToggleSwitch handler (plan 3 row ToggleSwitch). The switching behaviour is the WIDGET's (tap and
/// drag on the MaterialSwitch; a tap on the row toggles): a native change writes IsOn (Core raises
/// Toggled); Core-only pointers are forwarded to the widget (<see cref="CorePointerBridge"/>). Colours
/// from the ToggleSwitch keys (ToggleSwitchFillOn/Off, ToggleSwitchStrokeOff, ToggleSwitchKnobFillOn/Off,
/// ToggleSwitchContentForeground, ToggleSwitchHeaderForeground).
/// </summary>
internal sealed class ToggleSwitchHandler : ViewHandler<ToggleSwitch, ToggleSwitchView>
{
    /// <summary>ToggleSwitch's mapper.</summary>
    public static readonly PropertyMapper<ToggleSwitch, ToggleSwitchHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ToggleSwitch.IsOnProperty] = MapIsOn,
        [ToggleSwitch.HeaderProperty] = MapTexts,
        [ToggleSwitch.OnContentProperty] = MapTexts,
        [ToggleSwitch.OffContentProperty] = MapTexts,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
        [Control.FontStretchProperty] = MapFont,
    };

    private readonly CorePointerBridge _bridge;
    private NativeTemplateParts _parts;
    private bool _updating;

    /// <summary>Creates the handler.</summary>
    public ToggleSwitchHandler()
        : base(Mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The ToggleSwitch handler, or the templated fallback for one with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is ToggleSwitch toggle && NativeControlPolicy.IsNative(toggle, typeof(ToggleSwitch), new[] { "DefaultToggleSwitchStyle" }, out _)
            ? new ToggleSwitchHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps IsOn (written with a reentrancy guard; the thumb jumps, Core owns animation).</summary>
    public static void MapIsOn(ToggleSwitchHandler handler, ToggleSwitch element)
    {
        var view = handler.PlatformView;
        if (view.Switch.Checked != element.IsOn)
        {
            handler._updating = true;
            try
            {
                view.Switch.Checked = element.IsOn;
            }
            finally
            {
                handler._updating = false;
            }
        }

        MapTexts(handler, element);
        view.Switch.Invalidate();
    }

    /// <summary>Maps Header and On/OffContent.</summary>
    public static void MapTexts(ToggleSwitchHandler handler, ToggleSwitch element)
    {
        var view = handler.PlatformView;
        var header = element.Header switch
        {
            null => null,
            string s => s,
            TextBlock block => block.Text,
            UIElement => null,
            var other => other.ToString(),
        };
        view.Header.Text = header ?? string.Empty;
        view.Header.Visibility = string.IsNullOrEmpty(header) ? AViewStates.Gone : AViewStates.Visible;
        var content = element.IsOn ? element.OnContent : element.OffContent;
        view.Content.Text = content switch
        {
            null => string.Empty,
            string s => s,
            TextBlock block => block.Text ?? string.Empty,
            UIElement => string.Empty,
            var other => other.ToString(),
        };
        element.InvalidateMeasure();
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(ToggleSwitchHandler handler, ToggleSwitch element)
    {
        var view = handler.PlatformView;
        view.Enabled = element.IsEnabled;
        view.Switch.Enabled = element.IsEnabled;
        view.Header.Enabled = element.IsEnabled;
        view.Content.Enabled = element.IsEnabled;
    }

    /// <summary>Maps the colours.</summary>
    public static void MapColors(ToggleSwitchHandler handler, ToggleSwitch element)
    {
        var view = handler.PlatformView;
        var enabled = AResource.Attribute.StateEnabled;
        var @checked = AResource.Attribute.StateChecked;
        AColorStateList List(int on, int off, int? onDisabled, int? offDisabled) => new(
            new[] { new[] { -enabled, @checked }, new[] { -enabled }, new[] { @checked }, Array.Empty<int>() },
            new[] { onDisabled ?? on, offDisabled ?? off, on, off });

        var fillOn = ThemeResources.FindColor(element, "ToggleSwitchFillOn") ?? unchecked((int)0xFF0078D4);
        var fillOff = ThemeResources.FindColor(element, "ToggleSwitchFillOff") ?? 0;
        var strokeOff = ThemeResources.FindColor(element, "ToggleSwitchStrokeOff") ?? unchecked((int)0xFF808080);
        var knobOn = ThemeResources.FindColor(element, "ToggleSwitchKnobFillOn") ?? unchecked((int)0xFFFFFFFF);
        var knobOff = ThemeResources.FindColor(element, "ToggleSwitchKnobFillOff") ?? unchecked((int)0xFF606060);
        view.Switch.TrackTintList = List(fillOn, fillOff, ThemeResources.FindColor(element, "ToggleSwitchFillOnDisabled"), ThemeResources.FindColor(element, "ToggleSwitchFillOffDisabled"));
        view.Switch.TrackDecorationTintList = List(fillOn, strokeOff, ThemeResources.FindColor(element, "ToggleSwitchFillOnDisabled"), ThemeResources.FindColor(element, "ToggleSwitchStrokeOffDisabled"));
        view.Switch.ThumbTintList = List(knobOn, knobOff, ThemeResources.FindColor(element, "ToggleSwitchKnobFillOnDisabled"), ThemeResources.FindColor(element, "ToggleSwitchKnobFillOffDisabled"));

        var content = StateColors.FromKeys(element, "ToggleSwitchContentForeground", unchecked((int)0xFF000000));
        if (ThemeResources.ColorOf(element.Foreground) is { } actual && actual != content.Normal)
        {
            content = content.WithNormalEverywhere(actual);
        }

        view.Content.SetTextColor(content.ToColorStateList());
        view.Header.SetTextColor(StateColors.FromKeys(element, "ToggleSwitchHeaderForeground", content.Normal).ToColorStateList());
    }

    /// <summary>Maps the font of the header and content texts.</summary>
    public static void MapFont(ToggleSwitchHandler handler, ToggleSwitch element)
    {
        var view = handler.PlatformView;
        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        foreach (var text in new[] { view.Header, view.Content })
        {
            if (typeface != null)
            {
                text.Typeface = typeface;
            }

            text.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        }

        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <inheritdoc />
    protected override ToggleSwitchView CreatePlatformView()
    {
        var view = new ToggleSwitchView(MaterialWidgets.Material3(Context));
        var density = Density;
        view.ContentGap = MaterialWidgets.Px(12, density);
        view.HeaderGap = MaterialWidgets.Px(8, density);
        view.Switch.Drawn = OnSwitchDrawn;
        return view;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(ToggleSwitchView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Switch.CheckedChange += OnCheckedChange;
        platformView.Touch += OnTouch;
        platformView.Switch.Touch += OnTouch;
        _parts = Element != null ? NativeTemplateParts.For(Element) : null;
        _parts?.Declare("SwitchKnob", "SwitchKnobBounds", "SwitchAreaGrid");
        if (Element != null)
        {
            _bridge.Attach(Element);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ToggleSwitchView platformView)
    {
        _bridge.Detach();
        platformView.Switch.CheckedChange -= OnCheckedChange;
        platformView.Touch -= OnTouch;
        platformView.Switch.Touch -= OnTouch;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (NativeTemplateParts.Enabled && _parts != null && PlatformView is { } view)
        {
            // Before the first draw: the track is the switch's own rectangle, the knob a square at the end
            // its state puts it (the draw refines both from the drawables).
            NativeLayoutNow.Ensure(view, new Size(finalRect.Width, finalRect.Height), Density);
            var sw = view.Switch;
            if (sw.ThumbDrawable is not { } thumb || thumb.Bounds.IsEmpty)
            {
                var side = sw.Height;
                var left = sw.Checked ? sw.Width - side : 0;
                OnSwitchDrawn(new global::Android.Graphics.Rect(left, 0, left + side, side), new global::Android.Graphics.Rect(0, 0, sw.Width, sw.Height));
            }
        }

        _parts?.Arrange();
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _bridge.OnNativeTouch(sender as AView, e.Event);
    }

    private void OnCheckedChange(object sender, global::Android.Widget.CompoundButton.CheckedChangeEventArgs e)
    {
        if (_updating || Element is not ToggleSwitch element)
        {
            return;
        }

        if (element.IsOn != e.IsChecked)
        {
            element.IsOn = e.IsChecked;
        }
    }

    private void OnSwitchDrawn(global::Android.Graphics.Rect thumb, global::Android.Graphics.Rect track)
    {
        if (!NativeTemplateParts.Enabled || _parts == null || PlatformView is not { } view)
        {
            return;
        }

        var density = Density;
        var dx = view.Switch.Left;
        var dy = view.Switch.Top;
        Rect Dips(float l, float t, float r, float b) => new(l / density, t / density, Math.Max(0, r - l) / density, Math.Max(0, b - t) / density);

        // The thumb drawable spans the track's height; the visible knob is the circle centred in it.
        var knob = Math.Min(thumb.Width(), thumb.Height());
        var cx = dx + thumb.ExactCenterX();
        var cy = dy + thumb.ExactCenterY();
        _parts.Set("SwitchKnob", Dips(cx - (knob / 2f), cy - (knob / 2f), cx + (knob / 2f), cy + (knob / 2f)));
        _parts.Set("SwitchKnobBounds", Dips(dx + track.Left, dy + track.Top, dx + track.Right, dy + track.Bottom));
        var row = view.RowBounds;
        _parts.Set("SwitchAreaGrid", Dips(row.Left, row.Top, row.Right, row.Bottom));
    }
}
