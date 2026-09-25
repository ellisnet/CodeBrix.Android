using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// Tracks the live <see cref="CodeBrixActivity"/> instances: the most recently created one
/// (the activity a new XAML Window binds to) and the one that is currently resumed.
/// Main-thread only.
/// </summary>
internal static class ActivityRegistry
{
    private static readonly List<WeakReference<CodeBrixActivity>> _activities = new();
    private static WeakReference<CodeBrixActivity> _latest;
    private static WeakReference<CodeBrixActivity> _resumed;

    /// <summary>The resumed activity, else the most recently created live activity (or null).</summary>
    internal static CodeBrixActivity Current => Get(_resumed) ?? Get(_latest);

    /// <summary>The most recently created live activity (or null).</summary>
    internal static CodeBrixActivity Latest => Get(_latest);

    /// <summary>The number of live activities.</summary>
    internal static int Count
    {
        get
        {
            Prune();
            return _activities.Count;
        }
    }

    internal static void OnCreated(CodeBrixActivity activity)
    {
        Prune();
        _activities.Add(new WeakReference<CodeBrixActivity>(activity));
        _latest = new WeakReference<CodeBrixActivity>(activity);
    }

    internal static void OnResumed(CodeBrixActivity activity) => _resumed = new WeakReference<CodeBrixActivity>(activity);

    internal static void OnPaused(CodeBrixActivity activity)
    {
        if (Get(_resumed) == activity)
        {
            _resumed = null;
        }
    }

    internal static void OnDestroyed(CodeBrixActivity activity)
    {
        _activities.RemoveAll(r => !r.TryGetTarget(out var a) || a == activity);
        if (Get(_latest) == activity)
        {
            _latest = null;
            foreach (var reference in _activities)
            {
                if (reference.TryGetTarget(out var other))
                {
                    _latest = new WeakReference<CodeBrixActivity>(other);
                }
            }
        }

        OnPaused(activity);
    }

    private static CodeBrixActivity Get(WeakReference<CodeBrixActivity> reference) =>
        reference != null && reference.TryGetTarget(out var activity) && !activity.IsDestroyed ? activity : null;

    private static void Prune() => _activities.RemoveAll(r => !r.TryGetTarget(out var a) || a.IsDestroyed);
}
