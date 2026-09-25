namespace CodeBrix.Android.Portable;

/// <summary>
/// Reads the night-mode bits of Android's Configuration.UiMode.
/// </summary>
internal static class SystemThemeClassifier
{
    /// <summary>UI_MODE_NIGHT_MASK.</summary>
    internal const int UiModeNightMask = 0x30;

    /// <summary>UI_MODE_NIGHT_YES.</summary>
    internal const int UiModeNightYes = 0x20;

    /// <summary>Returns true when the configuration's UI mode says night (dark theme).</summary>
    internal static bool IsDark(int uiMode) => (uiMode & UiModeNightMask) == UiModeNightYes;
}
