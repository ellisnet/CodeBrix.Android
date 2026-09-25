using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Core;
using AOnBackPressedCallback = global::AndroidX.Activity.OnBackPressedCallback;
using ABackEventCompat = global::AndroidX.Activity.BackEventCompat;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The Android back button and back gesture of a CodeBrix activity (plan 2.8): one AndroidX
/// OnBackPressedCallback per activity, ENABLED only while something in the app will take the back - otherwise
/// the system's own back (and its predictive back-to-home animation) applies. The order is WinUI's:
/// <list type="number">
/// <item>the topmost overlay Core shows (a flyout, a ContentDialog - its close path, as Escape - or a
/// light-dismiss Popup) closes; platform overlays (Material dialogs, menus, sheets) take the back themselves;</item>
/// <item>SystemNavigationManager.BackRequested (the app's own back handling);</item>
/// <item>a NavigationView: an open overlay pane closes, else its BackRequested is raised while its back button
/// is enabled;</item>
/// <item>the innermost visible Frame that can go back goes back (with the back motion; during a predictive
/// back gesture the page being left previews the gesture - shrinks and shifts - and springs back on cancel).</item>
/// </list>
/// The enabled state is re-evaluated after every Core layout tick of the activity's window and whenever a frame
/// or a NavigationView changes.
/// </summary>
internal static class BackNavigation
{
    private static readonly Dictionary<CodeBrixActivity, BackCallback> _callbacks = new();
    private static readonly List<WeakReference<NavigationViewHandler>> _navigationViews = new();
    private static readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Navigation");

    /// <summary>What a back press does now.</summary>
    internal enum BackTarget
    {
        /// <summary>Nothing in the app takes it: the system's back.</summary>
        System,

        /// <summary>The topmost Core overlay closes.</summary>
        Overlay,

        /// <summary>SystemNavigationManager.BackRequested is raised.</summary>
        BackRequested,

        /// <summary>A NavigationView closes its pane or raises BackRequested.</summary>
        NavigationView,

        /// <summary>A Frame goes back.</summary>
        Frame,
    }

    /// <summary>Tracks a connected NavigationView.</summary>
    internal static void Add(NavigationViewHandler handler)
    {
        _navigationViews.Add(new WeakReference<NavigationViewHandler>(handler));
        if (handler.Context is CodeBrixActivity activity)
        {
            Update(activity);
        }
    }

    /// <summary>Forgets a disconnected NavigationView.</summary>
    internal static void Remove(NavigationViewHandler handler)
    {
        _navigationViews.RemoveAll(w => !w.TryGetTarget(out var live) || ReferenceEquals(live, handler));
        if (handler.Context is CodeBrixActivity activity)
        {
            Update(activity);
        }
    }

    /// <summary>Makes sure the activity has its back callback and re-evaluates whether it is enabled.</summary>
    internal static void Update(CodeBrixActivity activity)
    {
        if (activity == null || activity.IsDestroyed)
        {
            return;
        }

        if (!_callbacks.TryGetValue(activity, out var callback))
        {
            callback = new BackCallback(activity);
            _callbacks[activity] = callback;
            activity.OnBackPressedDispatcher.AddCallback(activity, callback);
        }

        callback.EnsureHost();
        if (!callback.InGesture)
        {
            callback.Enabled = Target(activity, out _) != BackTarget.System;
        }
    }

    /// <summary>Forgets a destroyed activity.</summary>
    internal static void Forget(CodeBrixActivity activity)
    {
        if (_callbacks.Remove(activity, out var callback))
        {
            callback.Detach();
        }
    }

    /// <summary>Handles one back request as the callback does (tests and the callback itself).</summary>
    /// <returns>What handled it (System = nothing in the app did).</returns>
    internal static BackTarget HandleBack(CodeBrixActivity activity)
    {
        var target = Target(activity, out var frame);
        switch (target)
        {
            case BackTarget.Overlay:
                CloseTopCoreOverlay(activity.XamlWindow?.Content?.XamlRoot, dryRun: false);
                break;
            case BackTarget.BackRequested:
                if (!SystemNavigationManager.GetForCurrentView().RequestBack())
                {
                    // The app looked and did not take it: fall through to the NavigationView / frame.
                    target = HandleAfterBackRequested(activity);
                }

                break;
            case BackTarget.NavigationView:
                HandleNavigationView(activity);
                break;
            case BackTarget.Frame:
                frame?.GoBackFromSystem();
                break;
        }

        return target;
    }

    /// <summary>What a back press would do now in <paramref name="activity"/>.</summary>
    internal static BackTarget Target(CodeBrixActivity activity, out FrameHandler frame)
    {
        frame = null;
        var root = activity.XamlWindow?.Content?.XamlRoot;
        if (root != null && CloseTopCoreOverlay(root, dryRun: true))
        {
            return BackTarget.Overlay;
        }

        if (SystemNavigationManager.GetForCurrentView().HasBackRequestedSubscribers)
        {
            return BackTarget.BackRequested;
        }

        return TargetAfterBackRequested(activity, out frame);
    }

    private static BackTarget TargetAfterBackRequested(CodeBrixActivity activity, out FrameHandler frame)
    {
        frame = null;
        if (FindNavigationViewAction(activity, out _) != null)
        {
            return BackTarget.NavigationView;
        }

        frame = FrameBackNavigation.FindFrameThatCanGoBack(activity);
        return frame != null ? BackTarget.Frame : BackTarget.System;
    }

    private static BackTarget HandleAfterBackRequested(CodeBrixActivity activity)
    {
        var target = TargetAfterBackRequested(activity, out var frame);
        if (target == BackTarget.NavigationView)
        {
            HandleNavigationView(activity);
        }
        else if (target == BackTarget.Frame)
        {
            frame.GoBackFromSystem();
        }

        return target;
    }

    private static void HandleNavigationView(CodeBrixActivity activity)
    {
        if (FindNavigationViewAction(activity, out var closePane) is not { } view)
        {
            return;
        }

        if (closePane)
        {
            view.IsPaneOpen = false;
        }
        else
        {
            view.RaiseBackRequestedFromPlatform();
        }
    }

    private static NavigationView FindNavigationViewAction(CodeBrixActivity activity, out bool closePane)
    {
        closePane = false;
        foreach (var weak in _navigationViews.ToArray())
        {
            if (!weak.TryGetTarget(out var handler) || !ReferenceEquals(handler.Context, activity) || handler.Element is not NavigationView view || !IsShown(view))
            {
                continue;
            }

            // AP5: a NavigationView in a native container closes its pane only when it is an open modal drawer.
            if (handler.IsNative ? handler.ClosesPaneOnBack
                : view.IsPaneOpen && view.DisplayMode != NavigationViewDisplayMode.Expanded && view.PaneDisplayMode != NavigationViewPaneDisplayMode.Top)
            {
                closePane = true;
                return view;
            }

            if (view.IsBackEnabled && view.IsBackButtonVisible != NavigationViewBackButtonVisible.Collapsed)
            {
                return view;
            }
        }

        return null;
    }

    private static bool IsShown(UIElement element)
    {
        for (DependencyObject node = element; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement { Visibility: not Visibility.Visible })
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Closes (or, with <paramref name="dryRun"/>, only finds) the topmost overlay Core presents in
    /// <paramref name="root"/>: a flyout Core shows (not one shown natively), else the last open popup when it
    /// hosts a ContentDialog (its close path) or is light-dismiss.
    /// </summary>
    internal static bool CloseTopCoreOverlay(XamlRoot root, bool dryRun)
    {
        if (root == null)
        {
            return false;
        }

        var flyouts = FlyoutBase.OpenFlyouts;
        for (var i = flyouts.Count - 1; i >= 0; i--)
        {
            var flyout = flyouts[i];
            if (NativeOverlays.Find(flyout) != null || (flyout.XamlRoot != null && !ReferenceEquals(flyout.XamlRoot, root)))
            {
                continue;
            }

            if (!dryRun)
            {
                flyout.Hide();
            }

            return true;
        }

        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(root);
        for (var i = popups.Count - 1; i >= 0; i--)
        {
            var popup = popups[i];
            if (popup.AssociatedFlyout != null)
            {
                continue;
            }

            if (popup.Child is ContentDialog dialog)
            {
                if (!dryRun)
                {
                    dialog.RaiseButtonFromPlatform(ContentDialogButton.None);
                }

                return true;
            }

            if (popup.IsLightDismissEnabled)
            {
                if (!dryRun)
                {
                    popup.IsOpen = false;
                }

                return true;
            }
        }

        return false;
    }

    private sealed class BackCallback : AOnBackPressedCallback
    {
        private readonly WeakReference<CodeBrixActivity> _activity;
        private AndroidXamlRootHost _host;
        private FrameHandler _previewFrame;
        private AView _previewView;

        internal BackCallback(CodeBrixActivity activity)
            : base(false)
        {
            _activity = new WeakReference<CodeBrixActivity>(activity);
        }

        /// <summary>True between HandleOnBackStarted and the press / cancel.</summary>
        internal bool InGesture { get; private set; }

        internal void EnsureHost()
        {
            if (_host != null || !_activity.TryGetTarget(out var activity))
            {
                return;
            }

            if (activity.XamlWindow?.Content?.XamlRoot is { } root && XamlRootMap.GetHostForRoot(root) is AndroidXamlRootHost host)
            {
                _host = host;
                host.LayoutUpdated += OnLayoutUpdated;
            }
        }

        internal void Detach()
        {
            if (_host != null)
            {
                _host.LayoutUpdated -= OnLayoutUpdated;
                _host = null;
            }

            Remove();
        }

        public override void HandleOnBackStarted(ABackEventCompat backEvent)
        {
            InGesture = true;
            _previewFrame = null;
            _previewView = null;
            if (_activity.TryGetTarget(out var activity) && Target(activity, out var frame) == BackTarget.Frame)
            {
                _previewFrame = frame;
                _previewView = frame.CurrentPageView;
            }
        }

        public override void HandleOnBackProgressed(ABackEventCompat backEvent)
        {
            if (_previewView is not { } view || backEvent == null)
            {
                return;
            }

            var density = view.Resources?.DisplayMetrics?.Density ?? 1f;
            var (scale, translation) = BackGestureMath.Preview(backEvent.Progress, backEvent.SwipeEdge == ABackEventCompat.EdgeLeft, view.Width, density);
            view.PivotX = view.Width / 2f;
            view.PivotY = view.Height / 2f;
            view.ScaleX = scale;
            view.ScaleY = scale;
            view.TranslationX = translation;
        }

        public override void HandleOnBackCancelled()
        {
            InGesture = false;
            if (_previewView is { } view)
            {
                view.Animate()?.ScaleX(1f).ScaleY(1f).TranslationX(0f).SetDuration(150).Start();
            }

            _previewView = null;
            _previewFrame = null;
            Refresh();
        }

        public override void HandleOnBackPressed()
        {
            InGesture = false;
            if (_previewView is { } view)
            {
                view.Animate()?.Cancel();
                view.ScaleX = 1f;
                view.ScaleY = 1f;
                view.TranslationX = 0f;
            }

            _previewView = null;
            _previewFrame = null;
            if (!_activity.TryGetTarget(out var activity) || activity.IsDestroyed)
            {
                return;
            }

            try
            {
                if (HandleBack(activity) == BackTarget.System)
                {
                    // Nothing in the app took it after all: let the system handle this back press.
                    Enabled = false;
                    activity.OnBackPressedDispatcher.OnBackPressed();
                }
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "The back press failed.");
            }

            Refresh();
        }

        private void OnLayoutUpdated(object sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            if (!InGesture && _activity.TryGetTarget(out var activity) && !activity.IsDestroyed)
            {
                Enabled = Target(activity, out _) != BackTarget.System;
            }
        }
    }
}
