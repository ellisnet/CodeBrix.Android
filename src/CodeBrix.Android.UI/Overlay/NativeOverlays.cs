#if __ANDROID__
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Hosting;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// A platform window CodeBrix.Android shows for a Core overlay (tier 2): a Material dialog for a
/// ContentDialog, a popup menu or bottom sheet for a MenuFlyout. It lives in the
/// <see cref="NativeOverlays"/> registry from the moment it shows until it is dismissed.
/// </summary>
internal abstract class NativeOverlay
{
    /// <summary>Creates the overlay.</summary>
    /// <param name="activity">The activity the overlay is shown over.</param>
    /// <param name="owner">The Core object it presents (the ContentDialog or the FlyoutBase).</param>
    protected NativeOverlay(CodeBrixActivity activity, object owner)
    {
        Activity = activity;
        Owner = owner;
    }

    /// <summary>The activity the overlay is shown over.</summary>
    internal CodeBrixActivity Activity { get; }

    /// <summary>The Core object it presents.</summary>
    internal object Owner { get; }

    /// <summary>True while the platform window is showing.</summary>
    internal abstract bool IsShowing { get; }

    /// <summary>
    /// Takes the platform window down because CORE closed the overlay (a button's Closing completed, the app
    /// called Hide): no Core event is raised from here.
    /// </summary>
    internal abstract void CloseFromCore();
}

/// <summary>The open tier-2 overlays, oldest first (the last one is on top).</summary>
internal static class NativeOverlays
{
    private static readonly List<NativeOverlay> _open = new();

    /// <summary>The open overlays, oldest first.</summary>
    internal static IReadOnlyList<NativeOverlay> Open => _open.ToArray();

    /// <summary>Records an overlay that is now showing.</summary>
    internal static void Add(NativeOverlay overlay)
    {
        _open.Remove(overlay);
        _open.Add(overlay);
    }

    /// <summary>Forgets an overlay that was dismissed.</summary>
    internal static void Remove(NativeOverlay overlay) => _open.Remove(overlay);

    /// <summary>The open overlay presenting <paramref name="owner"/>, or null.</summary>
    internal static NativeOverlay Find(object owner) => _open.LastOrDefault(o => ReferenceEquals(o.Owner, owner));

    /// <summary>The open overlay of type <typeparamref name="T"/> presenting <paramref name="owner"/>, or null.</summary>
    internal static T Find<T>(object owner)
        where T : NativeOverlay => _open.OfType<T>().LastOrDefault(o => ReferenceEquals(o.Owner, owner));

    /// <summary>
    /// Takes every open overlay down the way Core would (the owner's own close path: the dialog's close
    /// button, the flyout's Hide), then drops whatever is still registered. Test harnesses call it between
    /// scenarios.
    /// </summary>
    internal static void CloseAll()
    {
        foreach (var overlay in _open.ToArray())
        {
            switch (overlay.Owner)
            {
                case Microsoft.UI.Xaml.Controls.ContentDialog dialog:
                    dialog.Hide();
                    break;
                case Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase flyout:
                    flyout.Hide();
                    break;
            }

            if (overlay.IsShowing)
            {
                overlay.CloseFromCore();
            }
        }

        _open.Clear();
    }
}
#endif
