using CodeBrix.Platform.Contracts;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the optional IDeviceFamilyPlatform contract (pin 1.0.268.12, WPE1-5 B3): the
/// operating-system family Core puts in front of the device form in AnalyticsInfo.VersionInfo.DeviceFamily
/// ("Android.Mobile", "Android.Tablet", ...) instead of Environment.OSVersion.Platform ("Unix").
/// </summary>
/// <remarks>
/// PROVISIONAL: the exact strings are decision D2 (Jeremy's, WPE1_REPORT section 10); the recommendation
/// "Android.&lt;form&gt;" is used until then. The family is this one constant, so the decision changes one line.
/// </remarks>
internal sealed class DeviceFamilyAndroidPlatform : IDeviceFamilyPlatform
{
    /// <summary>The operating-system family of every CodeBrix.Android app (decision D2, provisional).</summary>
    internal const string Family = "Android";

    /// <inheritdoc />
    public string OperatingSystemFamily => Family;
}
