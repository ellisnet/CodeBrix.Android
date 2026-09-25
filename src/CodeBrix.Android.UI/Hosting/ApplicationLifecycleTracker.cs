using System;
using Microsoft.Extensions.Logging;
using AActivity = global::Android.App.Activity;
using AApplication = global::Android.App.Application;
using ABundle = global::Android.OS.Bundle;
using MUX = Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// Maps the Android activity lifecycle to the XAML Application lifecycle: when the last
/// visible CodeBrix activity stops the application enters the background and is
/// suspended; when one starts again it leaves the background and resumes.
/// </summary>
internal sealed class ApplicationLifecycleTracker : Java.Lang.Object, AApplication.IActivityLifecycleCallbacks
{
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Hosting.Lifecycle");
    private int _started;
    private bool _inBackground;

    /// <inheritdoc />
    public void OnActivityCreated(AActivity activity, ABundle savedInstanceState)
    {
    }

    /// <inheritdoc />
    public void OnActivityStarted(AActivity activity)
    {
        if (activity is not CodeBrixActivity)
        {
            return;
        }

        _started++;
        if (_started == 1 && _inBackground && MUX.Application.Current is { } application)
        {
            _inBackground = false;
            Log("Application leaving the background and resuming.");
            application.RaiseLeavingBackground(() => { });
            application.RaiseResuming();
        }
    }

    /// <inheritdoc />
    public void OnActivityResumed(AActivity activity)
    {
    }

    /// <inheritdoc />
    public void OnActivityPaused(AActivity activity)
    {
    }

    /// <inheritdoc />
    public void OnActivityStopped(AActivity activity)
    {
        if (activity is not CodeBrixActivity)
        {
            return;
        }

        _started = Math.Max(0, _started - 1);
        if (_started == 0 && !_inBackground && MUX.Application.Current is { } application)
        {
            _inBackground = true;
            Log("Application entering the background and suspending.");
            application.RaiseEnteredBackground(() => { });
            application.RaiseSuspending();
        }
    }

    /// <inheritdoc />
    public void OnActivitySaveInstanceState(AActivity activity, ABundle outState)
    {
    }

    /// <inheritdoc />
    public void OnActivityDestroyed(AActivity activity)
    {
    }

    private void Log(string message)
    {
        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation(message);
        }
    }
}
