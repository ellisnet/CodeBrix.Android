// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI/UI/Xaml/Window/Native/NativeWindowWrapper.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Android.UI.Portable.Input;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Xaml.Core;
using CodeBrix.Platform.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using AViewTreeObserver = global::Android.Views.ViewTreeObserver;
using AWindowCompat = global::AndroidX.Core.View.WindowCompat;
using AWindowInsetsCompat = global::AndroidX.Core.View.WindowInsetsCompat;
using AWindowInsetsControllerCompat = global::AndroidX.Core.View.WindowInsetsControllerCompat;
using JniHandleOwnership = global::Android.Runtime.JniHandleOwnership;
using MUX = Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Hosting; //was previously: {U}.UI.Xaml.Controls;

/// <summary>
/// The native window of a XAML <see cref="Window"/> on Android: wraps the
/// <see cref="CodeBrixActivity"/> that shows it. Bounds are the activity window's metrics;
/// the visible bounds are the bounds minus the system-bar and display-cutout insets (the
/// content is always laid out edge to edge); activation follows the top-resumed activity,
/// visibility follows OnStart/OnStop, closing follows the activity finishing.
/// </summary>
internal sealed class AndroidNativeWindowWrapper : NativeWindowWrapperBase //was previously: NativeWindowWrapper
{
    private readonly ActivationPreDrawListener _preDrawListener;
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Hosting.NativeWindow");
    private bool _contentViewAttachedToWindow;
    private bool _themeSubscribed;
    private string _title = string.Empty;
    private Rect _previousTrueVisibleBounds;
    private SafeAreaPadding _safeAreaPx;
    private bool _hasListenerInsets;
    private double _keyboardPx;
    private SafeAreaPadding _safeAreaDips;
    private Rect _occludedRect;

    internal AndroidNativeWindowWrapper(MUX.Window window, XamlRoot xamlRoot)
        : base(window, xamlRoot)
    {
        _preDrawListener = new ActivationPreDrawListener(this);
    }

    /// <summary>The activity that currently shows this window (null until one attaches).</summary>
    internal CodeBrixActivity Activity { get; private set; }

    /// <inheritdoc />
    public override object NativeWindow => Activity?.Window;

    /// <inheritdoc />
    public override string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            if (Activity != null)
            {
                Activity.Title = _title;
            }
        }
    }

    /// <summary>
    /// Binds this window to an activity: the first activity, or the one Android re-created
    /// (the XAML Window and its content survive the activity).
    /// </summary>
    internal void AttachActivity(CodeBrixActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (Activity == activity)
        {
            return;
        }

        Activity = activity;
        activity.AttachWindow(this);
        OnActivityCreated();

        if (!string.IsNullOrEmpty(_title))
        {
            activity.Title = _title;
        }

        if (WasShown)
        {
            // The window was shown by a previous activity: show it again on this one.
            ShowOnActivity();
        }
    }

    /// <summary>Called when the activity that shows this window is destroyed.</summary>
    internal void DetachActivity(CodeBrixActivity activity)
    {
        if (Activity == activity)
        {
            RemovePreDrawListener();
            Activity = null;
            _contentViewAttachedToWindow = false;
        }
    }

    internal void OnNativeVisibilityChanged(bool visible) => IsVisible = visible;

    /// <summary>
    /// The window's safe-area insets in DIPs (system bars and display cutout): what a Page or ScrollViewer
    /// touching a window edge absorbs (plan D-P10). The bounds minus these are the VisibleBounds.
    /// </summary>
    internal SafeAreaPadding SafeAreaDips => _safeAreaDips;

    /// <summary>The soft keyboard's height in DIPs (0 when hidden).</summary>
    internal double KeyboardDips { get; private set; }

    /// <summary>Raised on the UI thread when <see cref="SafeAreaDips"/> or the window size changed.</summary>
    internal event EventHandler SafeAreaChanged;

    /// <summary>
    /// The activity's root insets listener read a settled dispatch: safe area and keyboard inset, in
    /// physical pixels. Updates the bounds/VisibleBounds, the absorbing pages and InputPane.OccludedRect.
    /// </summary>
    internal void OnWindowInsets(SafeAreaPadding safeAreaPx, double keyboardPx)
    {
        _safeAreaPx = safeAreaPx;
        _keyboardPx = keyboardPx;
        _hasListenerInsets = true;
        RaiseNativeSizeChanged();
    }

    internal void OnActivityCreated() => AddPreDrawListener();

    internal void OnNativeActivated(CoreWindowActivationState state) => ActivationState = state;

    internal void OnNativeClosed() => RaiseClosing();

    internal void RaiseNativeSizeChanged()
    {
        var activity = Activity;
        if (activity == null || activity.IsDestroyed)
        {
            return;
        }

        var density = activity.Resources.DisplayMetrics.Density;
        var (bounds, visibleBounds, sizePx) = GetVisualBounds(activity, density);

        RasterizationScale = density;
        SetBoundsAndVisibleBounds(bounds, visibleBounds);
        SetSizes(sizePx, sizePx);
        ApplySystemOverlaysTheming();

        var safeArea = new SafeAreaPadding(
            visibleBounds.X - bounds.X,
            visibleBounds.Y - bounds.Y,
            Math.Max(0, bounds.Right - visibleBounds.Right),
            Math.Max(0, bounds.Bottom - visibleBounds.Bottom));
        var keyboard = _keyboardPx / density;
        var occluded = keyboard > 0 ? new Rect(0, Math.Max(0, bounds.Height - keyboard), bounds.Width, keyboard) : default;
        ApplyKeyboardOcclusionInset(SoftInputModePolicy.OcclusionInsetDips(_keyboardPx, density, activity.WithholdsKeyboard));
        var safeAreaChanged = SafeAreaMath.Differs(safeArea, _safeAreaDips) || bounds.Size != _lastBoundsSize;
        _lastBoundsSize = bounds.Size;
        _safeAreaDips = safeArea;
        KeyboardDips = keyboard;
        if (occluded != _occludedRect)
        {
            _occludedRect = occluded;
            UpdateInputPane(occluded);
        }

        if (safeAreaChanged)
        {
            SafeAreaChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_previousTrueVisibleBounds != visibleBounds)
        {
            _previousTrueVisibleBounds = visibleBounds;
            if (_log.IsEnabled(LogLevel.Debug))
            {
                _log.LogDebug("Window bounds {Bounds}, visible bounds {VisibleBounds} (DIPs), safe-area insets {Insets} px (from the insets listener: {FromListener}), density {Density}.", bounds, visibleBounds, _lastInsetsPx, _hasListenerInsets, density);
            }
        }
    }

    /// <inheritdoc />
    protected override void ShowCore()
    {
        if (!_themeSubscribed)
        {
            _themeSubscribed = true;
            MUX.Application.Current.RequestedThemeChanged += () =>
            {
                if (MUX.Application.Current.InitializationComplete)
                {
                    ApplySystemOverlaysTheming();
                }
            };
        }

        ShowOnActivity();
    }

    /// <inheritdoc />
    protected override void CloseCore()
    {
        var activity = Activity;
        if (activity != null && !activity.IsFinishing)
        {
            activity.Finish();
        }
    }

    /// <inheritdoc />
    public override void ExtendContentIntoTitleBar(bool extend)
    {
        // Content is always laid out edge to edge on Android; VisibleBounds carry the insets.
    }

    /// <inheritdoc />
    protected override IDisposable ApplyFullScreenPresenter()
    {
        UpdateFullScreenMode(true);
        return new ActionDisposable(() => UpdateFullScreenMode(false));
    }

    private void ShowOnActivity()
    {
        var activity = Activity;
        if (activity == null)
        {
            if (_log.IsEnabled(LogLevel.Warning))
            {
                _log.LogWarning("The window was shown before a CodeBrixActivity exists; it will appear when one is created.");
            }

            return;
        }

        activity.ContentViewAttachedToWindow -= OnContentViewAttachedToWindow;
        activity.ContentViewAttachedToWindow += OnContentViewAttachedToWindow;
        activity.EnsureContentView();
        RaiseNativeSizeChanged();
        ApplySystemOverlaysTheming();
    }

    private void OnContentViewAttachedToWindow(object sender, EventArgs e)
    {
        _contentViewAttachedToWindow = true;
        RaiseNativeSizeChanged();
    }

    private (Rect Bounds, Rect VisibleBounds, SizeInt32 SizePx) GetVisualBounds(CodeBrixActivity activity, float density)
    {
        var metrics = activity.WindowManager?.CurrentWindowMetrics;
        if (metrics == null)
        {
            return default;
        }

        var windowBounds = metrics.Bounds;
        SafeAreaPadding insets;
        if (_hasListenerInsets)
        {
            // The root listener's settled dispatch (the same system-bar/cutout union, but current
            // for this layout pass).
            insets = _safeAreaPx;
        }
        else
        {
            var fromMetrics = AWindowInsetsCompat.ToWindowInsetsCompat(metrics.WindowInsets)
                .GetInsets(AWindowInsetsCompat.Type.SystemBars() | AWindowInsetsCompat.Type.DisplayCutout());
            insets = new SafeAreaPadding(fromMetrics.Left, fromMetrics.Top, fromMetrics.Right, fromMetrics.Bottom);
        }

        var (bounds, visibleBounds) = WindowBoundsCalculator.Compute(
            windowBounds.Width(), windowBounds.Height(),
            (int)insets.Left, (int)insets.Top, (int)insets.Right, (int)insets.Bottom,
            density);
        _lastInsetsPx = insets;

        return (
            new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            new Rect(visibleBounds.X, visibleBounds.Y, visibleBounds.Width, visibleBounds.Height),
            new SizeInt32 { Width = windowBounds.Width(), Height = windowBounds.Height() });
    }

    private Size _lastBoundsSize;
    private SafeAreaPadding _lastInsetsPx;

    /// <summary>The height withheld from the bottom of the XAML root for the soft keyboard (DIPs; 0 unless Resize).</summary>
    internal double KeyboardOcclusionInsetDips { get; private set; }

    /// <summary>
    /// SoftInputAdjust.Resize: Core lays the page out above the soft keyboard through the root's content bottom
    /// occlusion inset (IRootElement.ContentBottomOcclusionInset - the seam the Platform's own on-screen keyboard
    /// uses); popups keep the full window. Pan / Unspecified: 0.
    /// </summary>
    private void ApplyKeyboardOcclusionInset(double insetDips)
    {
        if (Math.Abs(insetDips - KeyboardOcclusionInsetDips) < 0.01)
        {
            return;
        }

        if (Window?.RootElement is IRootElement root)
        {
            KeyboardOcclusionInsetDips = insetDips;
            root.ContentBottomOcclusionInset = insetDips;
            if (_log.IsEnabled(LogLevel.Debug))
            {
                _log.LogDebug("Content bottom occlusion inset {Inset} DIPs (soft keyboard, Resize).", insetDips);
            }
        }
    }

    private static void UpdateInputPane(Rect occluded)
    {
        // Core's InputPane raises Showing/Hiding and brings the focused element into view
        // (ScrollContentPresenter padding) from its occluded rectangle.
        InputPane.GetForCurrentView().OccludedRect = occluded;
    }

    private void ApplySystemOverlaysTheming()
    {
        // In the edge-to-edge experience the status bar foreground follows the app theme.
        if (MUX.Application.Current is { } application
            && Activity is { IsDestroyed: false } activity
            && activity.Window?.DecorView is { } decorView)
        {
            var insetsController = AWindowCompat.GetInsetsController(activity.Window, decorView);

            // "appearance light" refers to status bar set to light theme == dark foreground
            insetsController.AppearanceLightStatusBars = application.RequestedTheme == ApplicationTheme.Light;
            insetsController.AppearanceLightNavigationBars = application.RequestedTheme == ApplicationTheme.Light;
        }
    }

    private void UpdateFullScreenMode(bool isFullscreen)
    {
        if (Activity is not { IsDestroyed: false } activity || activity.Window?.DecorView is not { } decorView)
        {
            return;
        }

        var controller = AWindowCompat.GetInsetsController(activity.Window, decorView);
        if (isFullscreen)
        {
            controller.SystemBarsBehavior = AWindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            controller.Hide(AWindowInsetsCompat.Type.SystemBars());
        }
        else
        {
            controller.Show(AWindowInsetsCompat.Type.SystemBars());
        }
    }

    private void AddPreDrawListener()
    {
        if (Activity?.Window?.DecorView is { } decorView)
        {
            decorView.ViewTreeObserver.AddOnPreDrawListener(_preDrawListener);
        }
    }

    private void RemovePreDrawListener()
    {
        if (Activity?.Window?.DecorView is { } decorView && decorView.ViewTreeObserver.IsAlive)
        {
            decorView.ViewTreeObserver.RemoveOnPreDrawListener(_preDrawListener);
        }
    }

    private sealed class ActivationPreDrawListener : Java.Lang.Object, AViewTreeObserver.IOnPreDrawListener
    {
        private readonly AndroidNativeWindowWrapper _windowWrapper;

        public ActivationPreDrawListener(AndroidNativeWindowWrapper windowWrapper)
        {
            _windowWrapper = windowWrapper;
        }

        public ActivationPreDrawListener(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public bool OnPreDraw()
        {
            // Hold the first frame until the window's content view is attached (no empty frame
            // flashes before the XAML content); the root layout being attached counts too, so
            // a missed attach notification can never block drawing.
            if (_windowWrapper._contentViewAttachedToWindow || _windowWrapper.Activity?.RootLayout?.IsAttachedToWindow == true)
            {
                _windowWrapper._contentViewAttachedToWindow = true;
                _windowWrapper.RemovePreDrawListener();
                return true;
            }

            return false;
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action _action;

        public ActionDisposable(Action action) => _action = action;

        public void Dispose()
        {
            var action = _action;
            _action = null;
            action?.Invoke();
        }
    }
}
