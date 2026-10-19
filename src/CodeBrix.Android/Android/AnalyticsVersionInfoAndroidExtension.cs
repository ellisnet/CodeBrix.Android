using CodeBrix.Android.Portable;
using Windows.System.Profile.Internal;
using ABuild = global::Android.OS.Build;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the AnalyticsVersionInfo registry extension: DeviceFamilyVersion is the Android release
/// (Build.VERSION.RELEASE major and minor, the API level in the build component - see <see cref="AndroidOsVersion"/>),
/// encoded the WinUI way by the framework's own encoder. Without it the framework would encode the host's
/// Environment.OSVersion, which on Android is the Linux kernel's version. A preview release without a number is "cannot
/// say" (null), which the framework reports as "0".
/// </summary>
internal sealed class AnalyticsVersionInfoAndroidExtension : IAnalyticsVersionInfoExtension
{
    /// <inheritdoc />
    public string GetDeviceFamilyVersion()
    {
        var version = AndroidOsVersion.FromRelease(ABuild.VERSION.Release, (int)ABuild.VERSION.SdkInt);
        return version == null ? null : VersionHelpers.ToDeviceFamilyVersion(version);
    }
}
