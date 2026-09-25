using System;
using CodeBrix.Android.Portable;
using CodeBrix.Platform.Helpers.Theming;
using AConfiguration = global::Android.Content.Res.Configuration;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the system-theme registry extension: dark when the
/// configuration's UI mode says night. <see cref="OnConfigurationChanged"/> is called by
/// the CodeBrix.Android.UI hosting (Activity.OnConfigurationChanged) and raises
/// SystemThemeChanged when the night bit flips.
/// </summary>
internal sealed class SystemThemeHelperAndroidExtension : ISystemThemeHelperExtension
{
    private static readonly object _gate = new();
    private static SystemThemeHelperAndroidExtension _instance;
    private bool? _lastIsDark;

    /// <inheritdoc />
    public event EventHandler SystemThemeChanged;

    /// <summary>The instance Core created (null until Core asked for the extension).</summary>
    internal static SystemThemeHelperAndroidExtension Instance
    {
        get
        {
            lock (_gate)
            {
                return _instance;
            }
        }
    }

    /// <summary>Creates the extension (called by the registration lambda) and remembers it.</summary>
    internal static SystemThemeHelperAndroidExtension Create()
    {
        lock (_gate)
        {
            return _instance ??= new SystemThemeHelperAndroidExtension();
        }
    }

    /// <inheritdoc />
    public SystemTheme GetSystemTheme()
    {
        var isDark = IsDark(AndroidContext.Current.Resources.Configuration);
        _lastIsDark = isDark;
        return isDark ? SystemTheme.Dark : SystemTheme.Light;
    }

    /// <summary>Raises SystemThemeChanged when the new configuration flips the night bit.</summary>
    internal void OnConfigurationChanged(AConfiguration configuration)
    {
        var isDark = IsDark(configuration);
        if (_lastIsDark != isDark)
        {
            _lastIsDark = isDark;
            SystemThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsDark(AConfiguration configuration) =>
        configuration != null && SystemThemeClassifier.IsDark((int)configuration.UiMode);
}
