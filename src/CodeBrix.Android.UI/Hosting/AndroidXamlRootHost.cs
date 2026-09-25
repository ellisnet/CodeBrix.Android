using System;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// The <see cref="IXamlRootHost"/> of a window on Android. Rendering is done by native
/// views, so a render invalidation is an Android layout/draw request of the activity's
/// root layout; after each Core layout tick <see cref="LayoutUpdated"/> is raised.
/// </summary>
internal sealed class AndroidXamlRootHost : IXamlRootHost
{
    private readonly WeakReference<Window> _window;
    private CodeBrixViewGroup _rootGroup;

    internal AndroidXamlRootHost(Window window, XamlRoot xamlRoot, AndroidNativeWindowWrapper wrapper)
    {
        _window = new WeakReference<Window>(window);
        XamlRoot = xamlRoot;
        Wrapper = wrapper;
        XamlRootMap.Register(xamlRoot, this);
    }

    /// <summary>Raised on the UI thread after each Core layout tick of this XamlRoot.</summary>
    internal event EventHandler LayoutUpdated;

    /// <summary>The hosted XamlRoot.</summary>
    internal XamlRoot XamlRoot { get; }

    /// <summary>The native window wrapper of the hosted window.</summary>
    internal AndroidNativeWindowWrapper Wrapper { get; }

    /// <summary>The window's Core pointer input source (created by Core's input manager through the registry).</summary>
    internal CodeBrix.Android.UI.Input.AndroidCorePointerInputSource PointerSource { get; set; }

    /// <summary>The window's Core keyboard input source.</summary>
    internal CodeBrix.Android.UI.Input.AndroidKeyboardInputSource KeyboardSource { get; set; }

    /// <inheritdoc />
    public UIElement RootElement => _window.TryGetTarget(out var window) ? window.RootElement : null;

    /// <inheritdoc />
    public void InvalidateRender()
    {
        Wrapper.Activity?.RootLayout?.PostInvalidateOnAnimation();

        // Core's own frame request starts the animation frame loop at once (not at the next looper-idle check).
        if (Platform.Animation.CoreAnimationTicker.IsStarted && global::Android.OS.Looper.MyLooper() == global::Android.OS.Looper.MainLooper)
        {
            Platform.Animation.CoreAnimationTicker.Poke();
        }
    }

    /// <summary>Called by the rendering platform after a Core layout tick.</summary>
    internal void OnLayoutUpdated()
    {
        AttachRootView();
        Wrapper.Activity?.RootLayout?.RequestLayout();
        SafeAreaAbsorbers.Revalidate(XamlRoot);
        LayoutUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Keeps the window's native view tree in the activity's content layer. The window's root
    /// element (XamlIslandRoot) enters Core's tree as the visual root and gets no element
    /// handler, so the host owns the root view group: a <see cref="CodeBrixViewGroup"/> filling
    /// the content layer that shows the native views of the root element's visual children
    /// (the window content, the popup root, ...) at their Core rectangles. It is re-synced
    /// after every Core layout tick and moved to a re-created activity's content layer.
    /// </summary>
    internal void AttachRootView()
    {
        var activity = Wrapper.Activity;
        var layer = activity?.RootLayout?.ContentLayer;
        var root = RootElement;
        if (layer == null || root == null)
        {
            return;
        }

        AView rootView;
        if (root.Handler is IViewHandler { NativeView: { } own })
        {
            // A Core build that connects the root element: its own view is the root view.
            rootView = own;
        }
        else
        {
            _rootGroup ??= new CodeBrixViewGroup(activity);
            SyncRootChildren(root, _rootGroup);
            rootView = _rootGroup;
        }

        if (rootView.Parent == layer)
        {
            return;
        }

        (rootView.Parent as AViewGroup)?.RemoveView(rootView);
        layer.AddView(rootView, 0, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
    }

    /// <summary>The host-owned root view group (null until the first layout tick, or when the root element has its own view).</summary>
    internal CodeBrixViewGroup RootGroup => _rootGroup;

    private static void SyncRootChildren(UIElement root, CodeBrixViewGroup group)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        var index = 0;
        var present = new System.Collections.Generic.HashSet<UIElement>();
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is UIElement child && child.Handler is IViewHandler { NativeView: { } view })
            {
                present.Add(child);
                if (!ReferenceEquals(group.ViewOf(child), view) || !ReferenceEquals(group.GetChildAt(index), view))
                {
                    group.AddElementChild(child, view, index);
                }

                index++;
            }
        }

        foreach (var shown in group.ElementChildren)
        {
            if (!present.Contains(shown))
            {
                group.RemoveElementChild(shown);
            }
        }
    }
}
