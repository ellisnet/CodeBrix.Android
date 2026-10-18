using System;
using CodeBrix.Android.Android;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Platform.Insets;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Android.UI.Portable.Input;
using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.Extensions.Logging;
using Windows.UI.Core;
using ABundle = global::Android.OS.Bundle;
using AConfigChanges = global::Android.Content.PM.ConfigChanges;
using AConfiguration = global::Android.Content.Res.Configuration;
using AEdgeToEdge = global::AndroidX.Activity.EdgeToEdge;
using AIntent = global::Android.Content.Intent;
using AActivityFlags = global::Android.Content.ActivityFlags;
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
///           LaunchMode = LaunchMode.SingleTask,
///           ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
/// public class MainActivity : CodeBrixActivity { }
/// </code>
/// The app has ONE XAML window, shown by one activity, so the main activity must be single-task
/// (<c>LaunchMode = LaunchMode.SingleTask</c>): a repeated launch (the launcher icon after <c>am start -n</c>, a deep
/// link, a notification tap) then reaches the running activity as an intent, which
/// <see cref="CodeBrixApplication.OnLaunchIntent"/> receives - also when the launch had to create a new activity
/// because Back had finished the previous one while the application lived. An activity that is not single-task still never shows a
/// second, blank window: a second instance created while the first one shows the window logs one warning, forwards its
/// intent to <see cref="CodeBrixApplication.OnLaunchIntent"/>, brings the running activity to the front and finishes
/// (its <c>IsFinishing</c> is true when <c>base.OnCreate</c> returns).
/// Resizing, docking, orientation, density and theme changes are handled without
/// re-creating the activity (declare <see cref="HandledConfigurationChanges"/>); content is
/// laid out edge to edge; the first activity starts the XAML application
/// (<see cref="CodeBrixApplication"/>). The soft keyboard is hidden at start
/// (WindowSoftInputMode StateHidden unless the activity declares a visibility): it shows when a
/// text box is focused by touch, as WinUI's touch keyboard does. How the window makes room for it
/// follows <see cref="CodeBrixApplication.SoftInputAdjust"/> (default: the window pans, MAUI-style).
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
    private int _declaredSoftInputMode;
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
        // Android's initial focus. An app's own [Activity(WindowSoftInputMode = ...)] visibility wins. The adjust
        // mode follows the app's SoftInputAdjust (default Pan, MAUI-style; a declared adjust mode is kept until the
        // app sets one) - ApplySoftInputAdjust; the insets listener reports the keyboard in every mode.
        _declaredSoftInputMode = Window?.Attributes is { } attributes ? (int)attributes.SoftInputMode : 0;
        if (Window is { } window
            && (window.Attributes.SoftInputMode & global::Android.Views.SoftInput.MaskState) == global::Android.Views.SoftInput.StateUnspecified)
        {
            window.SetSoftInputMode((window.Attributes.SoftInputMode & ~global::Android.Views.SoftInput.MaskState) | global::Android.Views.SoftInput.StateHidden);
        }

        ApplySoftInputAdjust();

        if (Application is not CodeBrixApplication application)
        {
            throw new InvalidOperationException(
                "The Android Application class of a CodeBrix app must derive from CodeBrix.Android.UI.Hosting.CodeBrixApplication.");
        }

        // One XAML window, one activity: a second instance stacked by a repeated launch (main activity not
        // single-task) hands its intent to the running one and goes away instead of showing a blank window.
        var existingWindow = NativeWindowFactoryAndroidExtension.Instance.MainWindow;
        var host = existingWindow?.Activity;
        var action = ActivityInstancePolicy.Decide(
            existingWindow != null,
            ActivityInstancePolicy.IsHostedByAnotherLiveActivity(host != null, host == this, host?.IsDestroyed == true, host?.IsFinishing == true));
        if (action == ActivityCreateAction.ForwardToRunningInstance)
        {
            ForwardToRunningInstance(host);
            return;
        }

        RootLayout = new CodeBrixRootLayout(this);
        RootLayout.ViewAttachedToWindow += OnRootLayoutAttachedToWindow;
        RootLayout.SizeChanged += OnRootLayoutSizeChanged;
        _insetsListener = new WindowInsetsListener();
        _insetsListener.InsetsChanged += OnWindowInsetsChanged;
        _insetsListener.Attach(RootLayout);

        ActivityRegistry.OnCreated(this);
        InputHook ??= new ActivityInputRouter(this);

        if (action == ActivityCreateAction.ShowExistingWindow)
        {
            // The XAML application is already running and no other live activity shows its window (this activity
            // was re-created, or the one that showed it is finishing): show the window here. An activity that a
            // LAUNCH created (no saved state: Back had finished the previous one, or the app had closed its window)
            // then hands that launch to the application, as OnNewIntent does for a living activity - after the
            // window is shown here, so the app's handler finds its window.
            existingWindow.AttachActivity(this);
            if (Intent is { } launch
                && ActivityInstancePolicy.SurfacesCreateAsLaunch(action, savedInstanceState != null, launch.GetBooleanExtra(ActivityInstancePolicy.BringToFrontExtra, false)))
            {
                application.DeliverLaunchIntent(launch);
            }
        }
        else
        {
            application.EnsureXamlApplicationStarted(this);
        }
    }

    /// <summary>
    /// A repeated launch reached this (single-task) activity: the intent becomes the activity's
    /// <c>Intent</c> and is surfaced through <see cref="CodeBrixApplication.OnLaunchIntent"/>.
    /// An override must call the base method.
    /// </summary>
    /// <param name="intent">The new intent.</param>
    protected override void OnNewIntent(AIntent intent)
    {
        base.OnNewIntent(intent);
        if (intent == null || !ActivityInstancePolicy.SurfacesAsLaunch(intent.GetBooleanExtra(ActivityInstancePolicy.BringToFrontExtra, false)))
        {
            return;
        }

        ReceiveLaunchIntent(intent);
    }

    /// <summary>Takes a launch intent as this activity's intent and surfaces it to the application.</summary>
    internal void ReceiveLaunchIntent(AIntent intent)
    {
        Intent = intent;
        (Application as CodeBrixApplication)?.DeliverLaunchIntent(intent);
    }

    /// <summary>
    /// The host safety for a main activity that is not single-task: this second instance gives its intent to the
    /// activity that shows the XAML window, brings that activity's task and the activity to the front, and finishes.
    /// </summary>
    private void ForwardToRunningInstance(CodeBrixActivity running)
    {
        if (_log.IsEnabled(LogLevel.Warning))
        {
            _log.LogWarning(
                "A second {Activity} was created while the running one shows the XAML window: the main activity must be declared single-task ([Activity(LaunchMode = LaunchMode.SingleTask)]). The launch is forwarded to the running activity and this instance finishes.",
                GetType().Name);
        }

        if (Intent is { } launch)
        {
            running.ReceiveLaunchIntent(launch);
        }

        // Finish first, so the reorder below finds the running instance, not this one. NEW_TASK brings the task of
        // the running activity forward (this instance may live in another task); REORDER_TO_FRONT moves the running
        // instance to the top of it instead of creating a third one. Neither needs a permission.
        Finish();
        var bringToFront = new AIntent(this, running.Class);
        bringToFront.AddFlags(AActivityFlags.NewTask | AActivityFlags.ReorderToFront);
        bringToFront.PutExtra(ActivityInstancePolicy.BringToFrontExtra, true);
        StartActivity(bringToFront);
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
        // The window is never closed here (WindowLifecyclePolicy): backing out of the app finishes this activity (seen on
        // API 36 and 37) while the process and the XAML app live on, and the window stays open for the next activity,
        // as on API 33, where Back only moves the task to the back; an app's own Window.Close was already closed by
        // Core (Closed raised once) before it finished this activity.
        var wrapper = WindowWrapper;
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

    /// <summary>
    /// True while this activity's window is in adjustResize: the page is laid out above the soft keyboard (the
    /// keyboard's height is withheld from the bottom of the XAML root). False for Pan, Unspecified and adjustNothing.
    /// </summary>
    internal bool WithholdsKeyboard { get; private set; }

    /// <summary>
    /// Applies the soft-input adjust policy (<see cref="SoftInputModePolicy"/>): the app's
    /// <see cref="CodeBrixApplication.SoftInputAdjust"/> when it set one, else the activity's declared adjust mode, else
    /// Pan; then tells the window wrapper whether the keyboard is withheld from the page. Main thread; called at
    /// create and whenever the app setting changes.
    /// </summary>
    internal void ApplySoftInputAdjust()
    {
        if (Window is not { } window || IsDestroyed)
        {
            return;
        }

        var current = (int)window.Attributes.SoftInputMode;
        var mode = SoftInputModePolicy.Resolve(current, _declaredSoftInputMode, (Application as CodeBrixApplication)?.ExplicitSoftInputAdjust);
        if (mode != current)
        {
            window.SetSoftInputMode((global::Android.Views.SoftInput)mode);
        }

        WithholdsKeyboard = SoftInputModePolicy.WithholdsKeyboard(mode);
        if (_log.IsEnabled(LogLevel.Debug))
        {
            _log.LogDebug("Soft-input mode 0x{Mode:X2} (declared 0x{Declared:X2}); keyboard withheld from the page: {Withholds}.", mode, _declaredSoftInputMode, WithholdsKeyboard);
        }

        WindowWrapper?.RaiseNativeSizeChanged();
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

    /// <summary>Called by the window wrapper when the window moves on to another activity (this one is finishing).</summary>
    internal void ReleaseWindow(AndroidNativeWindowWrapper wrapper)
    {
        if (WindowWrapper == wrapper)
        {
            WindowWrapper = null;
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
