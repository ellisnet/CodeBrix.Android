#if __ANDROID__
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Contracts;
using AActivity = global::Android.App.Activity;
using AApplication = global::Android.App.Application;
using ABundle = global::Android.OS.Bundle;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// Registers what AP4 adds to the platform: the <see cref="IOverlayPresenterPlatform"/> (seam hooks H11/H12),
/// the activity bridge the WinRT services use (pickers, share, launcher: <see cref="SystemUiBridge"/>), and one
/// activity-lifecycle listener (the back callback of each activity, the frame navigation state across process
/// death). Idempotent.
/// </summary>
/// <remarks>
/// Called by AndroidPlatformBootstrap just before it asks Core to resolve its element-handler services
/// (UIElement.RefreshElementHandlerServices, which resolves the overlay presenter), so the presenter is seen on
/// that refresh without relying on the order in which Core resolves the factory and the presenter.
/// </remarks>
internal static class OverlayBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;
    private static LifecycleListener _listener;

    /// <summary>The registered presenter.</summary>
    internal static AndroidOverlayPresenter Presenter { get; private set; }

    /// <summary>Registers the presenter, the activity bridge and the lifecycle listener (once).</summary>
    internal static void EnsureRegistered()
    {
        lock (_gate)
        {
            if (!_registered)
            {
                var presenter = new AndroidOverlayPresenter();
                Presenter = presenter;
                ApiExtensibility.Register(typeof(IOverlayPresenterPlatform), _ => presenter);
                SystemUiBridge.Install();
                _registered = true;
            }
        }

        EnsureLifecycleListener(AApplication.Context as AApplication);
    }

    /// <summary>Registers the lifecycle listener with <paramref name="application"/> if not done yet.</summary>
    internal static void EnsureLifecycleListener(AApplication application)
    {
        lock (_gate)
        {
            if (_listener != null || application == null)
            {
                return;
            }

            _listener = new LifecycleListener();
            application.RegisterActivityLifecycleCallbacks(_listener);
        }
    }

    private sealed class LifecycleListener : global::Java.Lang.Object, AApplication.IActivityLifecycleCallbacks
    {
        public void OnActivityCreated(AActivity activity, ABundle savedInstanceState) =>
            NavigationStateKeeper.OnActivityCreated(activity, savedInstanceState);

        public void OnActivityDestroyed(AActivity activity)
        {
            if (activity is CodeBrixActivity codeBrixActivity)
            {
                BackNavigation.Forget(codeBrixActivity);
            }
        }

        public void OnActivityPaused(AActivity activity)
        {
        }

        public void OnActivityResumed(AActivity activity)
        {
            if (activity is CodeBrixActivity codeBrixActivity)
            {
                BackNavigation.Update(codeBrixActivity);
            }
        }

        public void OnActivitySaveInstanceState(AActivity activity, ABundle outState) =>
            NavigationStateKeeper.OnSaveInstanceState(activity, outState);

        public void OnActivityStarted(AActivity activity)
        {
        }

        public void OnActivityStopped(AActivity activity)
        {
        }
    }
}
#endif
