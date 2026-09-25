using CodeBrix.Platform.Helpers.Theming;
using Windows.Graphics.Display;
using AConfiguration = global::Android.Content.Res.Configuration;

namespace CodeBrix.Android.Android;

/// <summary>
/// Entry points the CodeBrix.Android.UI hosting calls when Android reports a change that
/// the WinRT-surface Core must hear about (reached through this assembly's
/// InternalsVisibleTo grant to CodeBrix.Android.UI).
/// </summary>
internal static class AndroidPlatformNotifications
{
    private static float? _lastDensity;
    private static int? _lastOrientation;

    /// <summary>
    /// Called from Activity.OnConfigurationChanged: refreshes the system theme and tells
    /// DisplayInformation when the density or the orientation changed.
    /// </summary>
    internal static void OnConfigurationChanged(AConfiguration configuration)
    {
        SystemThemeHelperAndroidExtension.Instance?.OnConfigurationChanged(configuration);
        SystemThemeHelper.RefreshSystemTheme();

        var displayInformation = DisplayInformation.GetForCurrentViewSafe();
        if (displayInformation == null || configuration == null)
        {
            return;
        }

        var density = configuration.DensityDpi / 160f;
        if (_lastDensity != density)
        {
            var notify = _lastDensity != null;
            _lastDensity = density;
            if (notify)
            {
                displayInformation.NotifyDpiChanged();
            }
        }

        var orientation = (int)configuration.Orientation;
        if (_lastOrientation != orientation)
        {
            var notify = _lastOrientation != null;
            _lastOrientation = orientation;
            if (notify)
            {
                displayInformation.NotifyOrientationChanged();
            }
        }
    }
}
