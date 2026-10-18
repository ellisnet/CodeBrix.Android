namespace CodeBrix.Android.UI.Portable;

/// <summary>What the host does with the XAML window when a new activity attaches to it.</summary>
internal enum AttachedActivityWindowAction
{
    /// <summary>The window was never shown (the app has not activated it yet): wait for the app's Activate.</summary>
    WaitForActivation,

    /// <summary>
    /// The window is shown (a previous activity showed it and nothing closed it - backing out of the app does not close
    /// it): show it again on this activity.
    /// </summary>
    ShowAgain,

    /// <summary>
    /// The app closed the window (Window.Close: Core raised Closed and made the single window re-showable) and the process
    /// lived on until a new launch: activate the same window again (Core keeps a closed window's content on a
    /// single-window platform).
    /// </summary>
    Reactivate,
}

/// <summary>
/// The single XAML window's lifecycle across activities (a CodeBrix app has ONE window; the activities that show it come
/// and go). The host never closes the window when an activity is destroyed: backing out of the app finishes the activity
/// on newer Android versions (seen on API 36 and 37) but leaves the process and the XAML application alive, exactly as
/// API 33 does by moving the task to the back - so Window.Closed is raised only for the app's own Window.Close (once, by
/// Core). A launch from the launcher then creates a new activity, which must show the same window - never a splash or
/// blank page.
/// </summary>
internal static class WindowLifecyclePolicy
{
    /// <summary>Decides what happens to the window when a new activity attaches to it.</summary>
    /// <param name="wasShown">The window is (still) shown: a previous activity showed it and it was not closed.</param>
    /// <param name="closedByApp">The app closed the window (Window.Close) while a previous activity showed it.</param>
    /// <returns>The action.</returns>
    internal static AttachedActivityWindowAction OnActivityAttached(bool wasShown, bool closedByApp)
    {
        if (wasShown)
        {
            return AttachedActivityWindowAction.ShowAgain;
        }

        return closedByApp ? AttachedActivityWindowAction.Reactivate : AttachedActivityWindowAction.WaitForActivation;
    }
}
