namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// The pure decision of the theme bridge's configuration callback: whether an Android configuration change re-reads and
/// re-writes the Material roles. Only a change of the UI mode's night bits does (a day/night flip re-derives the roles;
/// a rotation, a resize, a density or font-scale change never touches the palette), and only when the bridge's roles are
/// in place already - a configuration change refreshes the palette the app shows, it never switches an app that shows
/// the framework's Fluent colours over to Material.
/// </summary>
internal static class ThemeBridgeRefresh
{
    /// <summary>Decides whether a configuration change re-applies the Material roles.</summary>
    /// <param name="lastNightMask">The UI mode's night bits last seen (null when none was seen yet).</param>
    /// <param name="nightMask">The night bits of the new configuration (UiMode &amp; UiMode.NightMask).</param>
    /// <param name="rolesApplied">The bridge's own brushes are in the framework theme dictionaries.</param>
    /// <returns>True when the roles are re-applied.</returns>
    internal static bool ShouldReapply(int? lastNightMask, int nightMask, bool rolesApplied) =>
        rolesApplied && lastNightMask.HasValue && lastNightMask.Value != nightMask;
}
