#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AChoreographer = global::Android.Views.Choreographer;
using ASystemClock = global::Android.OS.SystemClock;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// Live re-pointing of re-keyed brushes (plan 2.11, D-O4; corpus section 3.1 pattern 3): an app repaints its
/// colour scheme by assigning <c>SolidColorBrush.Color</c> on the brushes its resource dictionaries hold
/// (GitHubIssueFinder re-points 163 control keys and its own palette at run time). A Fluent template repaints by
/// itself (its parts hold those brushes); a native widget got its state lists from the keys when it was mapped,
/// so this watcher follows every SolidColorBrush of the application's and the live elements' resource
/// dictionaries and, once per frame after any of them changed, re-maps the colours of every native control
/// handler in every window (the same mappings a Style change runs), and re-colours the Material dialogs.
/// </summary>
internal static class ThemeRefresh
{
    private static readonly ConditionalWeakTable<SolidColorBrush, object> _watched = new();
    private static readonly ConditionalWeakTable<ResourceDictionary, object> _dictionaries = new();
    private static readonly FrameCallback _callback = new();
    private static bool _posted;
    private static long _lastDiscovery;

    /// <summary>
    /// The properties re-mapped on a refresh: every native control handler maps its colours from one of them
    /// (Style re-reads the family's theme keys).
    /// </summary>
    private static DependencyProperty[] _colourProperties;

    /// <summary>How many refreshes ran (diagnostics, tests).</summary>
    internal static int RefreshCount { get; private set; }

    /// <summary>How many brushes are watched (diagnostics, tests).</summary>
    internal static int WatchedBrushCount { get; private set; }

    /// <summary>Called after a refresh re-mapped the handlers (the Material dialog colours follow here).</summary>
    internal static event Action Refreshed;

    /// <summary>Asks for a refresh on the next frame (coalesced).</summary>
    internal static void RequestRefresh()
    {
        if (_posted || AChoreographer.Instance is not { } choreographer)
        {
            return;
        }

        _posted = true;
        choreographer.PostFrameCallback(_callback);
    }

    /// <summary>Re-maps the colours of every native control handler in every window now (UI thread).</summary>
    internal static void RefreshAll()
    {
        RefreshCount++;
        _colourProperties ??= new[]
        {
            FrameworkElement.StyleProperty,
            Control.ForegroundProperty,
            Control.BackgroundProperty,
            Control.BorderBrushProperty,
            TextBlock.ForegroundProperty,
        };

        Discover();
        foreach (var root in Roots())
        {
            Walk(root, Remap);
        }

        try
        {
            Refreshed?.Invoke();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogError(exception, "A theme refresh listener failed.");
        }
    }

    /// <summary>Watches the brushes of the application's dictionaries and of the live elements' (throttled to once a second).</summary>
    internal static void DiscoverThrottled()
    {
        var now = ASystemClock.UptimeMillis();
        if (now - _lastDiscovery < 1000)
        {
            return;
        }

        Discover();
    }

    /// <summary>Watches the brushes of the application's dictionaries and of the live elements' now.</summary>
    internal static void Discover()
    {
        _lastDiscovery = ASystemClock.UptimeMillis();
        if (Application.Current?.Resources is { } application)
        {
            Watch(application);
        }

        foreach (var root in Roots())
        {
            Walk(root, element =>
            {
                if (element is FrameworkElement { Resources: { Count: > 0 } resources })
                {
                    Watch(resources);
                }
            });
        }
    }

    /// <summary>Watches every SolidColorBrush of a dictionary and of its merged dictionaries (framework dictionaries excluded).</summary>
    /// <param name="dictionary">The dictionary.</param>
    internal static void Watch(ResourceDictionary dictionary)
    {
        if (dictionary == null || IsFrameworkDictionary(dictionary))
        {
            return;
        }

        if (!_dictionaries.TryGetValue(dictionary, out _))
        {
            _dictionaries.Add(dictionary, null);
        }

        try
        {
            foreach (var pair in dictionary)
            {
                if (pair.Value is SolidColorBrush brush && !_watched.TryGetValue(brush, out _))
                {
                    _watched.Add(brush, null);
                    brush.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => RequestRefresh());
                    WatchedBrushCount++;
                }
            }

            foreach (var merged in dictionary.MergedDictionaries)
            {
                Watch(merged);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Watching a resource dictionary failed.");
        }
    }

    /// <summary>
    /// True for the framework's own theme dictionaries (XamlControlsResources and the other Fluent theme
    /// dictionaries): their brushes are the framework defaults, which the theme bridge owns.
    /// </summary>
    /// <param name="dictionary">A dictionary.</param>
    /// <returns>True for a framework dictionary.</returns>
    internal static bool IsFrameworkDictionary(ResourceDictionary dictionary) =>
        dictionary != null
        && dictionary.GetType() != typeof(ResourceDictionary)
        && dictionary.GetType().Assembly.GetName().Name is { } name
        && name.StartsWith("CodeBrix.Platform.UI.FluentTheme", StringComparison.Ordinal);

    /// <summary>The root element of every CodeBrix window (the visual root: content, popups, overlays).</summary>
    /// <returns>The roots.</returns>
    internal static IEnumerable<UIElement> Roots()
    {
        var seen = new HashSet<UIElement>();
        foreach (var activity in PresentationPolicy.Activities())
        {
            if (activity.XamlWindow?.Content?.XamlRoot is { } root
                && XamlRootMap.GetHostForRoot(root) is AndroidXamlRootHost { RootElement: { } element }
                && seen.Add(element))
            {
                yield return element;
            }
        }
    }

    /// <summary>Visits an element and its visual descendants (depth first).</summary>
    /// <param name="root">The root.</param>
    /// <param name="visit">The visitor.</param>
    internal static void Walk(DependencyObject root, Action<UIElement> visit)
    {
        if (root == null)
        {
            return;
        }

        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is UIElement element)
            {
                visit(element);
            }

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = count - 1; i >= 0; i--)
            {
                if (VisualTreeHelper.GetChild(current, i) is { } child)
                {
                    stack.Push(child);
                }
            }
        }
    }

    private static void Remap(UIElement element)
    {
        if (element.Handler is not IAndroidElementHandler handler || handler is TemplatedFallbackHandler || element is not (Control or TextBlock))
        {
            return;
        }

        try
        {
            foreach (var property in _colourProperties)
            {
                handler.UpdateValue(property);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Re-mapping the colours of {Element} failed.", element.GetType().Name);
        }
    }

    private sealed class FrameCallback : global::Java.Lang.Object, AChoreographer.IFrameCallback
    {
        public void DoFrame(long frameTimeNanos)
        {
            _posted = false;
            RefreshAll();
        }
    }
}
#endif
