using System;
using CodeBrix.Android.Portable;
using Windows.System.Profile.Internal;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the AnalyticsInfo registry extension: the device form from the UI mode type
/// (television, car, watch, VR headset) and otherwise from the width size class of the current window
/// (decision D2; see <see cref="DeviceFormClassifier"/>). It is computed on every query, so AnalyticsInfo.DeviceForm
/// follows a docked phone between size classes.
/// </summary>
internal sealed class AnalyticsInfoAndroidExtension : IAnalyticsInfoExtension
{
    /// <summary>
    /// The width in dp of the current window as the size-class service sees it (CodeBrix.Android.UI installs it:
    /// the window of the current activity, or the size-class override while one is set), or NaN when there is no
    /// window. Null (no UI assembly) or NaN falls back to the configuration's screen width (the app window's
    /// current width in dp).
    /// </summary>
    internal static Func<double> CurrentWindowWidthDp { get; set; }

    /// <inheritdoc />
    public CodeBrixDeviceForm GetDeviceForm()
    {
        var configuration = AndroidContext.Current.Resources.Configuration;
        var widthDp = CurrentWindowWidthDp?.Invoke() ?? double.NaN;
        if (double.IsNaN(widthDp) || widthDp <= 0)
        {
            // Configuration.SCREEN_WIDTH_DP_UNDEFINED is 0, which counts as unknown.
            widthDp = configuration.ScreenWidthDp;
        }

        return (CodeBrixDeviceForm)(int)DeviceFormClassifier.Classify((int)configuration.UiMode, widthDp);
    }
}
