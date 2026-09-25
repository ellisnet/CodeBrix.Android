using System;
using CodeBrix.Android.Android;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Platform.Insets;
using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.Extensions.Logging;
using Windows.UI.Core;
using ABundle = global::Android.OS.Bundle;
using AConfigChanges = global::Android.Content.PM.ConfigChanges;
using AConfiguration = global::Android.Content.Res.Configuration;
using AEdgeToEdge = global::AndroidX.Activity.EdgeToEdge;
using AKeyEvent = global::Android.Views.KeyEvent;
using AMotionEvent = global::Android.Views.MotionEvent;
using AppCompatActivity = global::AndroidX.AppCompat.App.AppCompatActivity;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// The Android activity that shows a CodeBrix XAML <see cref="Microsoft.UI.Xaml.Window"/>.
/// The app's launcher activity derives from it:
/// <code>
/// [Activity(MainLauncher = true, Theme = "@style/Theme.Material3.DayNight.NoActionBar",
///           ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
/// public class MainActivity : CodeBrixActivity { }
/// </code>
/// Resizing, docking, orientation, density and theme changes are handled without
/// re-creating the activity (declare <see cref="HandledConfigurationChanges"/>); content is
/// laid out edge to edge; the first activity starts the XAML application
/// (<see cref="CodeBrixApplication"/>). The soft keyboard is hidden at start
/// (WindowSoftInputMode StateHidden unless the activity declares a visibility): it shows when a
/// text box is focused by touch, as WinUI's touch keyboard does.
/// </summary>
public class CodeBrixActivity : AppCompatActivity
{
    /// <summary>
    /// The configuration changes a CodeBrix activity handles itself. Put this value in the
    /// activity's <c>[Activity(ConfigurationChanges = ...)]</c> attribute (Android reads it
    /// from the manifest, so it cannot be inherited).
    /// </summary>
    public const AConfigChanges HandledConfigurationChanges =
        AConfigChanges.Orientation | AConfigChanges.ScreenSize | AConfigChanges.ScreenLayout
        | AConfigChanges.SmallestScreenSize | AConfigChanges.UiMode | AConfigChanges.Density
        | AConfigChanges.Keyboard | AConfigChanges.KeyboardHidden | AConfigChanges.Navigation;

    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Hosting.Activity");
    private bool _isContentViewSet;
    private WindowInsetsListener _insetsListener;

    /// <summary>The root layout (content view) of this activity; created in OnCreate.</summary>
    public CodeBrixRootLayout RootLayout { get; private set; }

    /// <summary>The XAML window this activity shows (null until the XAML application created it).</summary>
    public Microsoft.UI.Xaml.Window XamlWindow => WindowWrapper?.Window;

    /// <summary>The native wrapper of the XAML window this activity shows (null until bound).</summary>
    internal AndroidNativeWindowWrapper WindowWrapper { get; private set; }

    /// <summary>The key and motion input hook (the window's Core input sources, AP2.5).</summary>
    internal IActivityInputHook InputHook { get; set; }

    /// <summary>Raised when the root layout is attached to the Android window.</summary>
    internal event EventHandler ContentViewAttachedToWindow;

    /// <inheritdoc />
    protected override void OnCreate(ABundle savedInstanceState)
    {
        AEdgeToEdge.Enable(this);
        base.OnCreate(savedInstanceState);

        // WinUI shows the touch keyboard only when a text box gets focus from a touch: never raise the IME for
        // Android's initial focus. An app's own [Activity(WindowSoftInputMode = ...)] visibility wins; the adjust
        // mode is left as declared (the insets listener reports the keyboard either way).
        if (Window is { } window
            && (window.Attributes.SoftInputMode & global::Android.Views.SoftInput.MaskState) == global::Android.Views.SoftInput.StateUnspecified)
        {
            window.SetSoftInputMode((window.Attributes.SoftInputMode & ~global::Android.Views.SoftInput.MaskState) | global::Android.Views.SoftInput.StateHidden);
        }

        RootLayout = new CodeBrixRootLayout(this);
        RootLayout.ViewAttachedToWindow += OnRootLayoutAttachedToWindow;
        RootLayout.SizeChanged += OnRootLayoutSizeChanged;
        _insetsListener = new WindowInsetsListener();
        _insetsListener.InsetsChanged += OnWindowInsetsChanged;
        _insetsListener.Attach(RootLayout);

        ActivityRegistry.OnCreated(this);
        InputHook ??= new ActivityInputRouter(this);

        if (Application is not CodeBrixApplication application)
        {
            throw new InvalidOperationException(
                "The Android Application class of a CodeBrix app must derive from CodeBrix.Android.UI.Hosting.CodeBrixApplication.");
        }

        var existingWindow = NativeWindowFactoryAndroidExtension.Instance.MainWindow;
        if (existingWindow != null)
        {
            // The XAML application is already running (this activity was re-created): show its window here.
            existingWindow.AttachActivity(this);
        }
        else
        {
            application.EnsureXamlApplicationStarted(this);
        }
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        base.OnStart();
        WindowWrapper?.OnNativeVisibilityChanged(true);
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        ActivityRegistry.OnResumed(this);
        WindowWrapper?.RaiseNativeSizeChanged();
    }

    /// <inheritdoc />
    public override void OnTopResumedActivityChanged(bool isTopResumedActivity)
    {
        base.OnTopResumedActivityChanged(isTopResumedActivity);
        WindowWrapper?.OnNativeActivated(isTopResumedActivity ? CoreWindowActivationState.CodeActivated : CoreWindowActivationState.Deactivated);
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        base.OnPause();
        ActivityRegistry.OnPaused(this);
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        base.OnStop();
        WindowWrapper?.OnNativeVisibilityChanged(false);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        var wrapper = WindowWrapper;
        if (wrapper != null && IsFinishing)
        {
            wrapper.OnNativeClosed();
        }

        wrapper?.DetachActivity(this);
        WindowWrapper = null;
        if (_insetsListener != null)
        {
            _insetsListener.InsetsChanged -= OnWindowInsetsChanged;
            _insetsListener.Detach();
            _insetsListener = null;
        }

        ActivityRegistry.OnDestroyed(this);
        base.OnDestroy();
    }

    /// <inheritdoc />
    public override void OnConfigurationChanged(AConfiguration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        AndroidPlatformNotifications.OnConfigurationChanged(newConfig);
        WindowWrapper?.RaiseNativeSizeChanged();
    }

    /// <inheritdoc />
    public override bool DispatchKeyEvent(AKeyEvent e)
    {
        if (e != null && InputHook is { } hook)
        {
            if (hook.OnKeyEvent(e, out var viewsSawKey))
            {
                return true;
            }

            if (viewsSawKey)
            {
                // A focused native text editor already had the key (and did not use it), and Core did not either:
                // unhandled, as base would report it - Android's own fallbacks (focus search, key shortcuts) still run.
                return false;
            }
        }

        return base.DispatchKeyEvent(e);
    }

    /// <summary>Dispatches a key to the activity's view hierarchy only (the focused view first), as base does.</summary>
    /// <param name="e">The key event.</param>
    /// <returns>True when a view used the key.</returns>
    internal bool DispatchKeyEventToViews(AKeyEvent e)
    {
        OnUserInteraction();
        return Window?.SuperDispatchKeyEvent(e) == true;
    }

    /// <inheritdoc />
    public override void OnProvideKeyboardShortcuts(System.Collections.Generic.IList<global::Android.Views.KeyboardShortcutGroup> data, global::Android.Views.IMenu menu, int deviceId)
    {
        // The app's KeyboardAccelerators in Android's keyboard shortcuts helper (Meta + /).
        base.OnProvideKeyboardShortcuts(data, menu, deviceId);
        CodeBrix.Android.UI.Handlers.KeyboardShortcuts.Provide(this, data);
    }

    /// <inheritdoc />
    public override bool DispatchTouchEvent(AMotionEvent ev)
    {
        var hook = InputHook;
        if (ev == null || hook == null)
        {
            return base.DispatchTouchEvent(ev);
        }

        // Native views first (a widget may mark the event handled, a scroll view may take the pointer
        // over), then Core's pointer pipeline (regime 2: hit test, routed events, gestures, capture).
        hook.BeginTouchEvent(ev);
        bool consumed;
        try
        {
            consumed = base.DispatchTouchEvent(ev);
        }
        finally
        {
            hook.EndTouchEvent(ev);
        }

        return consumed;
    }

    /// <inheritdoc />
    public override bool DispatchGenericMotionEvent(AMotionEvent ev)
    {
        if (ev != null && InputHook?.OnGenericMotionEvent(ev) == true)
        {
            return true;
        }

        return base.DispatchGenericMotionEvent(ev);
    }

    /// <summary>Called by the window wrapper when it binds to this activity.</summary>
    internal void AttachWindow(AndroidNativeWindowWrapper wrapper)
    {
        WindowWrapper = wrapper;
        if (_insetsListener is { HasInsets: true } listener)
        {
            wrapper.OnWindowInsets(listener.SafeAreaPx, listener.KeyboardPx);
        }
    }

    /// <summary>The root layout's insets listener (safe area and keyboard insets).</summary>
    internal WindowInsetsListener InsetsListener => _insetsListener;

    /// <summary>Sets the root layout as the activity's content view (once).</summary>
    internal void EnsureContentView()
    {
        if (!_isContentViewSet)
        {
            _isContentViewSet = true;
            SetContentView(RootLayout);
        }

        if (RootLayout.IsAttachedToWindow)
        {
            ContentViewAttachedToWindow?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnRootLayoutAttachedToWindow(object sender, AView.ViewAttachedToWindowEventArgs e)
    {
        if (_log.IsEnabled(LogLevel.Debug))
        {
            _log.LogDebug("Root layout attached to the window of {Activity}.", GetType().Name);
        }

        ContentViewAttachedToWindow?.Invoke(this, EventArgs.Empty);
    }

    private void OnRootLayoutSizeChanged(object sender, EventArgs e) => WindowWrapper?.RaiseNativeSizeChanged();

    private void OnWindowInsetsChanged(SafeAreaPadding safeAreaPx, double keyboardPx) => WindowWrapper?.OnWindowInsets(safeAreaPx, keyboardPx);
}

/// <summary>
/// The key and generic-motion hook of a <see cref="CodeBrixActivity"/>: the input layer
/// sees every event before the focused widget (a focused native text editor excepted, which gets its
/// keys first) and returns true when it handled it.
/// </summary>
internal interface IActivityInputHook
{
    /// <summary>A key event dispatched to the activity.</summary>
    /// <param name="e">The key event.</param>
    /// <param name="viewsSawKey">True when the hook already dispatched the key to the view hierarchy.</param>
    /// <returns>True when the key was used (by a native editor or by Core).</returns>
    bool OnKeyEvent(AKeyEvent e, out bool viewsSawKey);

    /// <summary>A generic motion event (mouse wheel, hover, joystick) dispatched to the activity.</summary>
    bool OnGenericMotionEvent(AMotionEvent e);

    /// <summary>A touch event is about to be dispatched to the activity's views.</summary>
    void BeginTouchEvent(AMotionEvent e);

    /// <summary>The touch event was dispatched to the activity's views.</summary>
    void EndTouchEvent(AMotionEvent e);
}
