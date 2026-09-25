#if __ANDROID__
using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Platform.Animation;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// The presentation-policy layer of CodeBrix.Android (plan 2.10-2.12, 2.18; work package AP5): window size
/// classes and the adaptive mapping table, the theme bridge (Material colour roles, dynamic colour, night mode,
/// the re-keyed Fluent brushes honored on the native widgets, live re-pointing), the type scale with the sp/dp
/// rule, and motion (Core animations ticked from Choreographer, the motion policy for native widgets).
/// </summary>
/// <remarks>
/// The layer customises the handlers of the other families through their public property mappers
/// (AppendToMapping, the MAUI customisation surface) rather than by editing them; everything it adds is in
/// Policy/, Platform/Animation/ and Handlers/Navigation/.
/// </remarks>
internal static class PresentationPolicy
{
    private static readonly List<WeakReference<CodeBrixActivity>> _activities = new();
    private static LifecycleCallbacks _lifecycle;
    private static bool _registered;
    private static bool _installed;

    /// <summary>True once the layer is installed.</summary>
    internal static bool IsInstalled => _installed;

    /// <summary>The live CodeBrix activities the layer follows.</summary>
    /// <returns>The activities.</returns>
    internal static IReadOnlyList<CodeBrixActivity> Activities()
    {
        _activities.RemoveAll(r => !r.TryGetTarget(out var a) || a.IsDestroyed);
        var list = new List<CodeBrixActivity>(_activities.Count);
        foreach (var reference in _activities)
        {
            if (reference.TryGetTarget(out var activity))
            {
                list.Add(activity);
            }
        }

        if (list.Count == 0 && ActivityRegistry.Current is { IsDestroyed: false } current)
        {
            list.Add(current);
        }

        return list;
    }

    /// <summary>
    /// Installs the layer (idempotent). Called when the handler registry is first built.
    /// </summary>
    /// <remarks>
    /// The registry is built from inside UIElement's static initializer (Core resolves the handler factory
    /// there), so nothing that touches UIElement's statics - a dependency property, a handler's mapper - may run
    /// synchronously: the installation is posted to the main looper, and runs before the first frame. Handlers
    /// that connected before it get the layer's mappings re-applied (<see cref="ThemeRefresh"/>).
    /// </remarks>
    internal static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        PostToMainThread(Install);
    }

    private static void Install()
    {
        try
        {
            InstallMapperCustomisations();
            CoreAnimationTicker.EnsureStarted();
            AnimatorScaleMonitor.EnsureStarted();
            if (global::Android.App.Application.Context is global::Android.App.Application application)
            {
                _lifecycle = new LifecycleCallbacks();
                application.RegisterActivityLifecycleCallbacks(_lifecycle);
            }

            if (ActivityRegistry.Current is { IsDestroyed: false } current)
            {
                Follow(current);
            }

            ThemeBridge.EnsureInstalled();
            if (global::Android.App.Application.Context is { } appContext)
            {
                TypeScalePolicy.OnFontScaleChanged(appContext.Resources?.Configuration?.FontScale ?? 1f);
                appContext.RegisterComponentCallbacks(new FontScaleCallbacks());
            }

            _installed = true;

            // Handlers that connected before the layer was installed get its mappings now.
            ThemeRefresh.RequestRefresh();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogError(exception, "Installing the presentation policy failed.");
        }
    }

    private static void InstallMapperCustomisations()
    {
        // Theme: the re-keyed brushes honored on the families whose handler reads only part of them.
        ThemeKeyAppliers.Install();

        // Type scale: sp while IsTextScaleFactorEnabled, the framework TextBlock styles' Material roles.
        TypeScalePolicy.Install();

        // ContentDialog: re-keyed colours on the Material dialogs.
        ContentDialogPolicy.Install();

        // Motion: ProgressBarHandler.MapProgress drives a frozen native indeterminate indicator itself
        // (IndeterminateProgressDriver) under MotionMode.AlwaysAnimate.
    }

    private static void Follow(CodeBrixActivity activity)
    {
        foreach (var reference in _activities)
        {
            if (reference.TryGetTarget(out var known) && ReferenceEquals(known, activity))
            {
                return;
            }
        }

        _activities.Add(new WeakReference<CodeBrixActivity>(activity));
        WindowSizeClassMonitor.Attach(activity);
        if (activity.Window?.DecorView is { ViewTreeObserver: { IsAlive: true } observer } decor)
        {
            observer.GlobalLayout += (_, _) => ThemeRefresh.DiscoverThrottled();

            // A dialog window appearing takes the focus from the activity's window: colour it on the next frame.
            observer.WindowFocusChange += (_, _) => decor.Post(ContentDialogPolicy.OnWindowFocusChanged);
        }
    }

    private static void Forget(CodeBrixActivity activity)
    {
        WindowSizeClassMonitor.Detach(activity);
        _activities.RemoveAll(r => !r.TryGetTarget(out var a) || ReferenceEquals(a, activity));
    }

    private static void PostToMainThread(Action action)
    {
        var looper = global::Android.OS.Looper.MainLooper;
        if (looper == null)
        {
            action();
            return;
        }

        new global::Android.OS.Handler(looper).Post(action);
    }

    private sealed class FontScaleCallbacks : global::Java.Lang.Object, global::Android.Content.IComponentCallbacks
    {
        public void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig) => TypeScalePolicy.OnFontScaleChanged(newConfig.FontScale);

        public void OnLowMemory()
        {
        }
    }

    private sealed class LifecycleCallbacks : global::Java.Lang.Object, global::Android.App.Application.IActivityLifecycleCallbacks
    {
        public void OnActivityCreated(global::Android.App.Activity activity, global::Android.OS.Bundle savedInstanceState)
        {
            if (activity is CodeBrixActivity codebrix)
            {
                Follow(codebrix);
            }
        }

        public void OnActivityDestroyed(global::Android.App.Activity activity)
        {
            if (activity is CodeBrixActivity codebrix)
            {
                Forget(codebrix);
            }
        }

        public void OnActivityPaused(global::Android.App.Activity activity)
        {
        }

        public void OnActivityResumed(global::Android.App.Activity activity)
        {
        }

        public void OnActivitySaveInstanceState(global::Android.App.Activity activity, global::Android.OS.Bundle outState)
        {
        }

        public void OnActivityStarted(global::Android.App.Activity activity)
        {
        }

        public void OnActivityStopped(global::Android.App.Activity activity)
        {
        }
    }
}
#endif
