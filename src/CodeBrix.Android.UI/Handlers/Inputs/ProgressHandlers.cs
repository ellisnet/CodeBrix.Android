// Technique from .NET MAUI, src/Core/src/Handlers/ProgressBar/ProgressBarHandler2.Android.cs,
// src/Core/src/Handlers/ActivityIndicator/ActivityIndicatorHandler.Android.cs (ActivityIndicatorHandler2)
// and src/Core/src/Platform/Android/MaterialActivityIndicator.cs @ 828569a864 (Material progress
// indicators; progress written with setProgressCompat; the indicator colour from the element).
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using ACircularProgressIndicator = Google.Android.Material.ProgressIndicator.CircularProgressIndicator;
using ALinearProgressIndicator = Google.Android.Material.ProgressIndicator.LinearProgressIndicator;
using AValueAnimator = global::Android.Animation.ValueAnimator;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The ProgressBar handler (plan 3 row ProgressBar): LinearProgressIndicator; IsIndeterminate, Value
/// between Minimum and Maximum, Foreground -> indicator colour, Background -> track colour; ShowError / ShowPaused
/// draw the indicator in the Fluent template's Error / Paused colours (SystemFillColorCriticalBrush /
/// SystemFillColorCautionBrush, AP1.9: the copied C0g ProgressBar scenario). Shaped like
/// the Fluent bar (the indicator starts at the left edge and stops exactly at the value: no gap, no stop
/// indicator, square ends) and as thick as Core lays the control out.
/// </summary>
internal sealed class ProgressBarHandler : ViewHandler<ProgressBar, ALinearProgressIndicator>
{
    private const int Scale = 10000;

    /// <summary>The share of the track a still indeterminate indicator covers (animations removed).</summary>
    private const double StillIndeterminateFraction = 0.3;

    /// <summary>ProgressBar's mapper.</summary>
    public static readonly PropertyMapper<ProgressBar, ProgressBarHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [RangeBase.MinimumProperty] = MapProgress,
        [RangeBase.MaximumProperty] = MapProgress,
        [RangeBase.ValueProperty] = MapProgress,
        [ProgressBar.IsIndeterminateProperty] = MapProgress,
        [ProgressBar.ShowPausedProperty] = MapColors,
        [ProgressBar.ShowErrorProperty] = MapColors,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
    };

    private readonly BrushWatcher _foregroundWatcher;
    private readonly BrushWatcher _backgroundWatcher;
    private NativeTemplateParts _parts;

    /// <summary>Creates the handler.</summary>
    public ProgressBarHandler()
        : base(Mapper)
    {
        _foregroundWatcher = new BrushWatcher(() => { if (Element is ProgressBar e) { MapColors(this, e); } });
        _backgroundWatcher = new BrushWatcher(() => { if (Element is ProgressBar e) { MapColors(this, e); } });
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The ProgressBar handler, or the templated fallback for one with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is ProgressBar bar && NativeControlPolicy.IsNative(bar, typeof(ProgressBar), new[] { "DefaultProgressBarStyle" }, out _)
            ? new ProgressBarHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Minimum/Maximum/Value and IsIndeterminate.</summary>
    public static void MapProgress(ProgressBarHandler handler, ProgressBar element)
    {
        var view = handler.PlatformView;

        // With animations removed (the system animator duration scale is 0 - an accessibility setting)
        // Material draws nothing for an indeterminate bar; WinUI still shows its indicator, so the bar then
        // shows a still indicator segment instead of the (disabled) sweep.
        var animated = AValueAnimator.AreAnimatorsEnabled();
        var indeterminate = element.IsIndeterminate && animated;
        if (view.Indeterminate != indeterminate)
        {
            // Material only switches mode while the indicator is not shown.
            var visibility = view.Visibility;
            view.Visibility = AViewStates.Invisible;
            view.Indeterminate = indeterminate;
            view.Visibility = visibility;
        }

        if (!indeterminate)
        {
            var fraction = element.IsIndeterminate ? StillIndeterminateFraction : Fraction(element);
            view.SetProgressCompat((int)Math.Round(fraction * Scale), false);
        }

        handler.PublishParts();

        // A still indeterminate bar keeps moving under MotionMode.AlwaysAnimate (presentation policy, AP5).
        Platform.Animation.IndeterminateProgressDriver.Update(view, element.IsIndeterminate);
    }

    /// <summary>Maps Foreground (indicator), Background (track) and the Error / Paused states (indicator).</summary>
    public static void MapColors(ProgressBarHandler handler, ProgressBar element)
    {
        handler._foregroundWatcher.Watch(element.Foreground);
        handler._backgroundWatcher.Watch(element.Background);
        var view = handler.PlatformView;

        // The Fluent template's Error / Paused visual states recolour the indicator (Error wins, as its state
        // group's order does); otherwise the Foreground.
        var indicator = element.ShowError
            ? ThemeResources.FindColor(element, "SystemFillColorCriticalBrush") ?? unchecked((int)0xFFC42B1C)
            : element.ShowPaused
                ? ThemeResources.FindColor(element, "SystemFillColorCautionBrush") ?? unchecked((int)0xFF9D5D00)
                : ThemeResources.ColorOf(element.Foreground) ?? ThemeResources.FindColor(element, "ProgressBarForeground") ?? unchecked((int)0xFF0078D4);
        var track = ThemeResources.ColorOf(element.Background) ?? 0;
        view.SetIndicatorColor(indicator);
        view.TrackColor = track;
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        // The Fluent ProgressBar asks for nothing of its own: its MinHeight (the style's) or Height sets the
        // thickness and its alignment the length; the native bar takes the size Core arranges.
        new(0, 0);

    /// <inheritdoc />
    protected override ALinearProgressIndicator CreatePlatformView()
    {
        var view = new ALinearProgressIndicator(MaterialWidgets.Material3(Context))
        {
            Max = Scale,
            IndicatorTrackGapSize = 0,
            TrackStopIndicatorSize = 0,
            TrackCornerRadius = 0,
            ShowAnimationBehavior = 0,
            HideAnimationBehavior = 0,
        };
        view.SetMinimumHeight(0);
        view.SetMinimumWidth(0);
        return view;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(ALinearProgressIndicator platformView)
    {
        base.ConnectHandler(platformView);
        _parts = Element != null ? NativeTemplateParts.For(Element) : null;
        _parts?.Declare("DeterminateProgressBarIndicator");
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ALinearProgressIndicator platformView)
    {
        _foregroundWatcher.Clear();
        _backgroundWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        PlatformView.TrackThickness = Math.Max(1, MaterialWidgets.Px(size.Height, Density));
        PublishParts();
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        PublishParts();
        _parts?.Arrange();
    }

    private static double Fraction(RangeBase element)
    {
        var range = element.Maximum - element.Minimum;
        return range > 0 ? Math.Clamp((element.Value - element.Minimum) / range, 0, 1) : 0;
    }

    private void PublishParts()
    {
        if (!NativeTemplateParts.Enabled || _parts == null || Element is not ProgressBar element || !HasArranged)
        {
            return;
        }

        var size = ArrangedRect;
        _parts.Set("DeterminateProgressBarIndicator", element.IsIndeterminate ? null : new Rect(0, 0, Fraction(element) * size.Width, size.Height));
    }
}

/// <summary>
/// The ProgressRing handler (plan 3 row ProgressRing): CircularProgressIndicator; IsActive shows or hides
/// it (an inactive ring draws nothing), IsIndeterminate (default) spins, a determinate ring shows Value;
/// Foreground -> indicator colour, Background -> track colour; the ring is as large as Core lays it out.
/// </summary>
internal sealed class ProgressRingHandler : ViewHandler<ProgressRing, ACircularProgressIndicator>
{
    private const int Scale = 10000;

    /// <summary>ProgressRing's mapper.</summary>
    public static readonly PropertyMapper<ProgressRing, ProgressRingHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ProgressRing.IsActiveProperty] = MapState,
        [ProgressRing.IsIndeterminateProperty] = MapState,
        [ProgressRing.ValueProperty] = MapState,
        [ProgressRing.MinimumProperty] = MapState,
        [ProgressRing.MaximumProperty] = MapState,
        [UIElement.VisibilityProperty] = MapState,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
    };

    /// <summary>Creates the handler.</summary>
    public ProgressRingHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The ProgressRing handler, or the templated fallback for one with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is ProgressRing ring && NativeControlPolicy.IsNative(ring, typeof(ProgressRing), new[] { "DefaultProgressRingStyle" }, out _)
            ? new ProgressRingHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps IsActive, IsIndeterminate, Value (and Visibility, which IsActive refines).</summary>
    public static void MapState(ProgressRingHandler handler, ProgressRing element)
    {
        ViewMappers.MapVisibility(handler, element);
        var view = handler.PlatformView;
        var indeterminate = element.IsIndeterminate;
        if (view.Indeterminate != indeterminate)
        {
            view.Visibility = AViewStates.Invisible;
            view.Indeterminate = indeterminate;
        }

        if (!indeterminate)
        {
            var range = element.Maximum - element.Minimum;
            var fraction = range > 0 ? Math.Clamp((element.Value - element.Minimum) / range, 0, 1) : 0;
            view.SetProgressCompat((int)Math.Round(fraction * Scale), false);
        }

        if (element.Visibility == Visibility.Visible)
        {
            view.Visibility = element.IsActive ? AViewStates.Visible : AViewStates.Invisible;
        }
    }

    /// <summary>Maps Foreground (indicator) and Background (track).</summary>
    public static void MapColors(ProgressRingHandler handler, ProgressRing element)
    {
        var indicator = ThemeResources.ColorOf(element.Foreground) ?? ThemeResources.FindColor(element, "ProgressRingForegroundThemeBrush") ?? unchecked((int)0xFF0078D4);
        handler.PlatformView.SetIndicatorColor(indicator);
        handler.PlatformView.TrackColor = ThemeResources.ColorOf(element.Background) ?? 0;
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 32 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 32 : availableSize.Height;
        var size = Math.Min(width, height);
        return new Size(size, size);
    }

    /// <inheritdoc />
    protected override ACircularProgressIndicator CreatePlatformView()
    {
        var view = new ACircularProgressIndicator(MaterialWidgets.Material3(Context))
        {
            Max = Scale,
            IndicatorTrackGapSize = 0,
            ShowAnimationBehavior = 0,
            HideAnimationBehavior = 0,
            Indeterminate = true,
        };
        view.SetMinimumHeight(0);
        view.SetMinimumWidth(0);
        return view;
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        var density = Density;
        var diameter = MaterialWidgets.Px(Math.Min(size.Width, size.Height), density);
        var thickness = Math.Max(MaterialWidgets.Px(2, density), (int)Math.Round(diameter / 10.0));
        PlatformView.TrackThickness = thickness;
        PlatformView.IndicatorInset = 0;
        PlatformView.IndicatorSize = Math.Max(thickness * 2, diameter);
    }
}
