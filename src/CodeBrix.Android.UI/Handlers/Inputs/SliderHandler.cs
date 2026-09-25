// Technique from .NET MAUI, src/Core/src/Handlers/Slider/SliderHandler2.Android.cs @ 828569a864
// (Material Slider; value written back from the change listener only for user changes, with a
// reentrancy guard; track and thumb colours as tint lists). Copyright (c) .NET Foundation and
// Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AMaterialSlider = Google.Android.Material.Slider.Slider;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AResource = global::Android.Resource;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native view of a Slider: the Header text above a Material Slider. The slider keeps its own
/// (touch-target) height and is centred on the space Core gives it, so its track sits in the middle of
/// the control as the Fluent template draws it.
/// </summary>
internal sealed class SliderView : AViewGroup
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal SliderView(AContext context)
        : base(context)
    {
        Header = new ATextView(context) { Visibility = AViewStates.Gone };
        Header.SetIncludeFontPadding(false);
        Slider = new AMaterialSlider(context);
        AddView(Header);
        AddView(Slider);
        SetClipChildren(false);
        SetClipToPadding(false);
    }

    /// <summary>The header text (Gone without a header).</summary>
    internal ATextView Header { get; }

    /// <summary>The Material slider.</summary>
    internal AMaterialSlider Slider { get; }

    /// <summary>The gap below the header, in pixels.</summary>
    internal int HeaderGap { get; set; }

    /// <summary>The top of the slider area (below the header), in pixels.</summary>
    internal int SliderAreaTop { get; private set; }

    /// <summary>The vertical centre of the slider area (the track's centre line), in pixels.</summary>
    internal float TrackCenterY { get; private set; }

    /// <summary>Raised after every layout pass.</summary>
    internal event EventHandler LaidOut;

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

        var widthMode = widthMeasureSpec.GetMode();
        var sliderWidthSpec = widthMode == AMeasureSpecMode.Unspecified
            ? unspecified
            : AMeasureSpecMode.Exactly.MakeMeasureSpec(widthMeasureSpec.GetSize());
        Slider.Measure(sliderWidthSpec, unspecified);
        var width = widthMode == AMeasureSpecMode.Unspecified ? Math.Max(headerWidth, Slider.MeasuredWidth) : widthMeasureSpec.GetSize();
        var height = headerHeight + Slider.MeasuredHeight;
        SetMeasuredDimension(ResolveSize(width, widthMeasureSpec), ResolveSize(height, heightMeasureSpec));
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var width = r - l;
        var height = b - t;
        var top = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Layout(0, 0, Header.MeasuredWidth, Header.MeasuredHeight);
            top = Header.MeasuredHeight + HeaderGap;
        }

        if (Slider.MeasuredWidth != width)
        {
            Slider.Measure(AMeasureSpecMode.Exactly.MakeMeasureSpec(width), AMeasureSpecMode.Unspecified.MakeMeasureSpec(0));
        }

        var sliderHeight = Slider.MeasuredHeight;
        var area = Math.Max(0, height - top);
        var sliderTop = top + ((area - sliderHeight) / 2);
        Slider.Layout(0, sliderTop, width, sliderTop + sliderHeight);
        SliderAreaTop = top;
        TrackCenterY = sliderTop + (sliderHeight / 2f);
        LaidOut?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The Slider handler (plan 3 row Slider): Material Slider (Minimum/Maximum -> valueFrom/valueTo, Value,
/// StepFrequency snapping, Header -> label TextView, vertical Orientation). The dragging and tapping
/// behaviour is the WIDGET's: a user change writes RangeBase.Value (Core raises ValueChanged); Core-only
/// pointers are forwarded to the widget (<see cref="CorePointerBridge"/>). The slider is shaped like the
/// Fluent one (4 dp track, round 20 dp thumb, no gap, no stop indicator, no tick marks) and coloured
/// from the Slider keys (SliderTrackValueFill*, SliderTrackFill*, SliderInnerThumbBackground*).
/// </summary>
/// <remarks>
/// The Material slider validates its configuration when it draws (valueFrom &lt; valueTo, the value in
/// range, the value on a step): the handler therefore keeps it continuous (stepSize 0) and snaps values
/// itself, and never writes an out-of-range value.
/// </remarks>
internal sealed class SliderHandler : ViewHandler<Slider, SliderView>
{
    /// <summary>Slider's mapper.</summary>
    public static readonly PropertyMapper<Slider, SliderHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [RangeBase.MinimumProperty] = MapRange,
        [RangeBase.MaximumProperty] = MapRange,
        [RangeBase.ValueProperty] = MapRange,
        [Slider.OrientationProperty] = MapOrientation,
        [Slider.HeaderProperty] = MapHeader,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.FontFamilyProperty] = MapHeader,
        [Control.FontSizeProperty] = MapHeader,
    };

    private readonly CorePointerBridge _bridge;
    private NativeTemplateParts _parts;
    private bool _updating;

    /// <summary>Creates the handler.</summary>
    public SliderHandler()
        : base(Mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The Slider handler, or the templated fallback for one with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is Slider slider && NativeControlPolicy.IsNative(slider, typeof(Slider), new[] { "DefaultSliderStyle" }, out _)
            ? new SliderHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Minimum, Maximum and Value (in that order, always a valid Material configuration).</summary>
    public static void MapRange(SliderHandler handler, Slider element)
    {
        var slider = handler.PlatformView.Slider;
        var min = element.Minimum;
        var max = element.Maximum;
        if (!(max > min))
        {
            max = min + 1;
        }

        var value = Math.Clamp(element.Value, min, max);
        handler._updating = true;
        try
        {
            // Widen first, then narrow, so the value is inside the range at every step.
            slider.ValueFrom = (float)Math.Min(min, slider.ValueFrom);
            slider.ValueTo = (float)Math.Max(max, slider.ValueTo);
            slider.Value = (float)value;
            slider.ValueFrom = (float)min;
            slider.ValueTo = (float)max;
            slider.Value = (float)value;
        }
        finally
        {
            handler._updating = false;
        }

        handler.PublishParts();
    }

    /// <summary>Maps Orientation.</summary>
    public static void MapOrientation(SliderHandler handler, Slider element)
    {
        handler.PlatformView.Slider.SetOrientation(element.Orientation == Orientation.Vertical ? 1 : 0);
        element.InvalidateMeasure();
    }

    /// <summary>Maps Header (a text label above the slider) and its font.</summary>
    public static void MapHeader(SliderHandler handler, Slider element)
    {
        var header = handler.PlatformView.Header;
        var text = element.Header switch
        {
            null => null,
            string s => s,
            TextBlock block => block.Text,
            UIElement => null,
            var other => other.ToString(),
        };
        header.Text = text ?? string.Empty;
        header.Visibility = string.IsNullOrEmpty(text) ? AViewStates.Gone : AViewStates.Visible;
        if (AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch) is { } typeface)
        {
            header.Typeface = typeface;
        }

        header.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        element.InvalidateMeasure();
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(SliderHandler handler, Slider element)
    {
        handler.PlatformView.Enabled = element.IsEnabled;
        handler.PlatformView.Slider.Enabled = element.IsEnabled;
        handler.PlatformView.Header.Enabled = element.IsEnabled;
    }

    /// <summary>Maps the colours.</summary>
    public static void MapColors(SliderHandler handler, Slider element)
    {
        var slider = handler.PlatformView.Slider;
        var enabled = AResource.Attribute.StateEnabled;
        AColorStateList List(string key, int fallback)
        {
            var normal = ThemeResources.FindColor(element, key) ?? fallback;
            var disabled = ThemeResources.FindColor(element, key + "Disabled") ?? normal;
            return new AColorStateList(new[] { new[] { -enabled }, Array.Empty<int>() }, new[] { disabled, normal });
        }

        var accent = unchecked((int)0xFF0078D4);
        slider.TrackActiveTintList = List("SliderTrackValueFill", accent);
        slider.TrackInactiveTintList = List("SliderTrackFill", unchecked((int)0xFF909090));
        slider.ThumbTintList = List("SliderInnerThumbBackground", ThemeResources.FindColor(element, "SliderTrackValueFill") ?? accent);
        handler.PlatformView.Header.SetTextColor(StateColors.FromKeys(element, "SliderHeaderForeground", unchecked((int)0xFF000000)).ToColorStateList());

        // The re-keyed Slider family's full state lists (presentation policy, AP5).
        Policy.ThemeKeyAppliers.Slider(handler, element);
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <inheritdoc />
    protected override SliderView CreatePlatformView()
    {
        var view = new SliderView(MaterialWidgets.Material3(Context));
        var density = Density;
        var slider = view.Slider;
        slider.StepSize = 0;
        slider.TickVisible = false;
        slider.LabelBehavior = 2; // LABEL_GONE
        slider.TrackHeight = MaterialWidgets.Px(4, density);
        slider.ThumbWidth = MaterialWidgets.Px(20, density);
        slider.ThumbHeight = MaterialWidgets.Px(20, density);
        slider.ThumbTrackGapSize = 0;
        slider.TrackStopIndicatorSize = 0;
        slider.TrackCornerSize = MaterialWidgets.Px(2, density);
        slider.TrackInsideCornerSize = 0;
        slider.HaloRadius = 0;
        slider.ThumbElevation = 0;
        slider.Focusable = false;
        slider.FocusableInTouchMode = false;
        slider.SoundEffectsEnabled = false;
        view.HeaderGap = MaterialWidgets.Px(8, density);
        return view;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(SliderView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Slider.Change += OnChange;
        platformView.Slider.Touch += OnTouch;
        platformView.LaidOut += OnLaidOut;
        _parts = Element != null ? NativeTemplateParts.For(Element) : null;
        _parts?.Declare("HorizontalTrackRect", "HorizontalDecreaseRect", "HorizontalThumb");
        if (Element != null)
        {
            _bridge.Attach(Element);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(SliderView platformView)
    {
        _bridge.Detach();
        platformView.Slider.Change -= OnChange;
        platformView.Slider.Touch -= OnTouch;
        platformView.LaidOut -= OnLaidOut;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (NativeTemplateParts.Enabled && _parts != null && PlatformView is { } view)
        {
            NativeLayoutNow.Ensure(view, new Size(finalRect.Width, finalRect.Height), Density);
            PublishParts();
        }

        _parts?.Arrange();
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _bridge.OnNativeTouch(sender as AView, e.Event);
    }

    private void OnLaidOut(object sender, EventArgs e) => PublishParts();

    private void OnChange(object sender, AMaterialSlider.ChangeEventArgs e)
    {
        if (_updating || !e.P2 || Element is not Slider element)
        {
            return;
        }

        var value = Snap(element, e.P1);
        if (value != element.Value)
        {
            element.Value = value;
        }

        PublishParts();
    }

    private static double Snap(Slider element, double value)
    {
        var step = element.StepFrequency;
        if (element.SnapsTo == SliderSnapsTo.StepValues && step > 0)
        {
            value = element.Minimum + (Math.Round((value - element.Minimum) / step) * step);
        }

        return Math.Clamp(value, element.Minimum, element.Maximum);
    }

    private void PublishParts()
    {
        if (!NativeTemplateParts.Enabled || _parts == null || PlatformView is not { } view || view.Width <= 0)
        {
            return;
        }

        var slider = view.Slider;
        if (slider.IsVertical)
        {
            return;
        }

        var density = Density;
        Rect Dips(float l, float t, float r, float b) => new(l / density, t / density, Math.Max(0, r - l) / density, Math.Max(0, b - t) / density);
        var x0 = slider.Left + slider.TrackSidePadding;
        var trackWidth = slider.TrackWidth;
        var x1 = x0 + trackWidth;
        var range = slider.ValueTo - slider.ValueFrom;
        var fraction = range > 0 ? (slider.Value - slider.ValueFrom) / range : 0;
        var cx = x0 + (fraction * trackWidth);
        var cy = view.TrackCenterY;
        var halfTrack = slider.TrackHeight / 2f;
        var thumbHalfWidth = slider.ThumbWidth / 2f;
        var thumbHalfHeight = slider.ThumbHeight / 2f;

        // The Fluent geometry the UI tests measure: the track spans the thumb's whole travel plus one thumb,
        // the filled part runs from the travel's start to the thumb's centre.
        _parts.Set("HorizontalTrackRect", Dips(x0 - thumbHalfWidth, cy - halfTrack, x1 + thumbHalfWidth, cy + halfTrack));
        _parts.Set("HorizontalDecreaseRect", Dips(x0, cy - halfTrack, cx, cy + halfTrack));
        _parts.Set("HorizontalThumb", Dips(cx - thumbHalfWidth, cy - thumbHalfHeight, cx + thumbHalfWidth, cy + thumbHalfHeight));
    }
}
