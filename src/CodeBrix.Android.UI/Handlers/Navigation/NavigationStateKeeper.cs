using System;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using AActivity = global::Android.App.Activity;
using ABundle = global::Android.OS.Bundle;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Keeps the navigation state of a window's root Frame across process death (plan 2.7 / risk 11.13):
/// Frame.GetNavigationState() goes into the activity's saved instance state; when Android re-creates the
/// activity with that state in a new process, the state is given back to the root frame
/// (Frame.SetNavigationState) after the app's own first navigation - unless the app already arrived at the
/// same state. Fragments are never involved (D-P5). A state that cannot be written (a navigation parameter
/// that is not serializable) or read back is logged and skipped.
/// </summary>
internal static class NavigationStateKeeper
{
    /// <summary>The saved-instance-state key.</summary>
    internal const string StateKey = "codebrix.frame.navigationState";

    private static readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Navigation");
    private static string _pending;

    /// <summary>An activity was created (with the saved state of an earlier process, or null).</summary>
    internal static void OnActivityCreated(AActivity activity, ABundle savedInstanceState)
    {
        if (activity is CodeBrixActivity && savedInstanceState?.GetString(StateKey) is { Length: > 0 } state)
        {
            _pending = state;
        }
    }

    /// <summary>An activity saves its instance state.</summary>
    internal static void OnSaveInstanceState(AActivity activity, ABundle outState)
    {
        if (activity is not CodeBrixActivity codeBrixActivity || outState == null || RootFrame(codeBrixActivity) is not { } frame)
        {
            return;
        }

        try
        {
            outState.PutString(StateKey, frame.GetNavigationState());
        }
        catch (Exception exception)
        {
            _log.LogWarning(exception, "The root frame's navigation state could not be saved (a navigation parameter that is not serializable?).");
        }
    }

    /// <summary>A frame connected.</summary>
    internal static void OnFrameConnected(FrameHandler handler)
    {
    }

    /// <summary>A frame navigated: gives a pending restored state to the root frame after its first navigation.</summary>
    internal static void OnFrameNavigated(FrameHandler handler)
    {
        if (_pending is not { } state || handler.Element is not Frame frame || handler.Context is not CodeBrixActivity activity || !ReferenceEquals(RootFrame(activity), frame))
        {
            return;
        }

        _pending = null;
        frame.DispatcherQueue?.TryEnqueue(() =>
        {
            try
            {
                if (frame.GetNavigationState() != state)
                {
                    frame.SetNavigationState(state);
                }
            }
            catch (Exception exception)
            {
                _log.LogWarning(exception, "The saved navigation state could not be restored; the app keeps its own first page.");
            }
        });
    }

    private static Frame RootFrame(CodeBrixActivity activity) => activity.XamlWindow?.Content as Frame;
}
