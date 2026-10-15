namespace CodeBrix.Android.UI.Portable;

/// <summary>What a new CodeBrixActivity does in OnCreate (<see cref="ActivityInstancePolicy.Decide"/>).</summary>
internal enum ActivityCreateAction
{
    /// <summary>No XAML window exists yet: this is the first activity, it starts the XAML application.</summary>
    StartApplication,

    /// <summary>
    /// The XAML window exists and no other live activity shows it (Android re-created the activity, or the one that
    /// showed it is finishing): this activity shows the window.
    /// </summary>
    ShowExistingWindow,

    /// <summary>
    /// The XAML window is shown by ANOTHER live activity (an app whose main activity is not single-task was launched
    /// again, and Android stacked a second instance): this activity forwards its launch intent to the running one,
    /// brings that one to the front and finishes - it never builds a second, blank window.
    /// </summary>
    ForwardToRunningInstance,
}

/// <summary>
/// The pure decisions behind the Android host's single-instance rule. A CodeBrix app has ONE XAML window, hosted by
/// one activity; its main activity is declared single-task, so a repeated launch reaches the running activity as an
/// intent (OnNewIntent -> CodeBrixApplication.OnLaunchIntent). An app that forgot the launch mode still never shows a
/// second, blank activity: the second instance forwards its intent and finishes.
/// </summary>
internal static class ActivityInstancePolicy
{
    /// <summary>
    /// The intent extra that marks the host's own "bring the running activity to the front" intent. The running
    /// activity receives that intent in OnNewIntent and does not surface it as a launch: the launch it stands for was
    /// already forwarded.
    /// </summary>
    internal const string BringToFrontExtra = "com.codebrix.android.host.BRING_TO_FRONT";

    /// <summary>Decides what a new activity does in OnCreate.</summary>
    /// <param name="windowExists">The XAML application already created its window.</param>
    /// <param name="windowHostedByAnotherLiveActivity">
    /// Another activity shows that window and is neither destroyed nor finishing.
    /// </param>
    /// <returns>The action.</returns>
    internal static ActivityCreateAction Decide(bool windowExists, bool windowHostedByAnotherLiveActivity)
    {
        if (!windowExists)
        {
            return ActivityCreateAction.StartApplication;
        }

        return windowHostedByAnotherLiveActivity
            ? ActivityCreateAction.ForwardToRunningInstance
            : ActivityCreateAction.ShowExistingWindow;
    }

    /// <summary>
    /// Whether the activity that shows the window counts as another live host for <see cref="Decide"/>.
    /// </summary>
    /// <param name="hasHost">An activity shows the window.</param>
    /// <param name="hostIsThisActivity">That activity is the one being created.</param>
    /// <param name="hostIsDestroyed">That activity is destroyed.</param>
    /// <param name="hostIsFinishing">That activity is finishing.</param>
    /// <returns>True when the window is hosted by another activity that stays.</returns>
    internal static bool IsHostedByAnotherLiveActivity(bool hasHost, bool hostIsThisActivity, bool hostIsDestroyed, bool hostIsFinishing) =>
        hasHost && !hostIsThisActivity && !hostIsDestroyed && !hostIsFinishing;

    /// <summary>
    /// Whether an intent delivered to the running activity (OnNewIntent) is a launch the application hears about:
    /// every intent except the host's own bring-to-front intent.
    /// </summary>
    /// <param name="isBringToFrontIntent">The intent carries <see cref="BringToFrontExtra"/>.</param>
    /// <returns>True when the intent is surfaced through CodeBrixApplication.OnLaunchIntent.</returns>
    internal static bool SurfacesAsLaunch(bool isBringToFrontIntent) => !isBringToFrontIntent;
}
