using System;

namespace CodeBrix.Android.Portable;

/// <summary>
/// The Android release as the <see cref="Version"/> AnalyticsInfo.VersionInfo.DeviceFamilyVersion encodes
/// (CodeBrix.Android.Android.AnalyticsVersionInfoAndroidExtension): major and minor from Build.VERSION.RELEASE
/// ("14" is 14.0, "4.4.2" is 4.4, "4.4W" is 4.4), the API level (Build.VERSION.SDK_INT) in the build component and 0 in
/// the revision. A release without a leading number (a preview such as "Baklava" or "Q", or an empty string) has no
/// version: null, which the framework reports as "0" (cannot say).
/// </summary>
internal static class AndroidOsVersion
{
    /// <summary>The version of a release string and API level, or null when the release carries no number.</summary>
    /// <param name="release">Build.VERSION.RELEASE.</param>
    /// <param name="sdkInt">Build.VERSION.SDK_INT (a negative value counts as 0).</param>
    /// <returns>The version, or null.</returns>
    internal static Version FromRelease(string release, int sdkInt)
    {
        if (string.IsNullOrEmpty(release))
        {
            return null;
        }

        var parts = release.Split('.');
        var major = LeadingNumber(parts[0]);
        if (major < 0)
        {
            return null;
        }

        var minor = parts.Length > 1 ? Math.Max(0, LeadingNumber(parts[1])) : 0;
        return new Version(major, minor, Math.Max(0, sdkInt), 0);
    }

    // The leading decimal digits of a release component ("4W" is 4), or -1 when it starts with none.
    private static int LeadingNumber(string component)
    {
        var length = 0;
        while (length < component.Length && length < 5 && char.IsAsciiDigit(component[length]))
        {
            length++;
        }

        return length == 0 ? -1 : int.Parse(component.AsSpan(0, length), System.Globalization.CultureInfo.InvariantCulture);
    }
}
