using CodeBrix.Android.Portable;
using CodeBrix.Platform.Contracts;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the optional IDeviceFamilyPlatform contract (WPE1-5 B3): the operating-system
/// family Core puts in front of the device form in AnalyticsInfo.VersionInfo.DeviceFamily instead of
/// Environment.OSVersion.Platform ("Unix"). Decision D2 (ruled by Jeremy 2026-09-26): DeviceFamily is
/// "Android.&lt;form&gt;" with the form from the window size class - "Android.Mobile" (Compact),
/// "Android.Tablet" (Medium), "Android.Desktop" (Expanded) - see <see cref="DeviceFormClassifier"/> and
/// AnalyticsInfoAndroidExtension (the form part).
/// </summary>
internal sealed class DeviceFamilyAndroidPlatform : IDeviceFamilyPlatform
{
    /// <summary>The operating-system family of every CodeBrix.Android app (decision D2).</summary>
    internal const string Family = DeviceFormClassifier.OperatingSystemFamily;

    /// <inheritdoc />
    public string OperatingSystemFamily => Family;
}
