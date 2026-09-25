using CodeBrix.Android.Portable;
using Windows.System.Profile.Internal;
using APackageManager = global::Android.Content.PM.PackageManager;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the AnalyticsInfo registry extension: the device form
/// from the UI mode type, the "android.hardware.type.pc" system feature and the smallest
/// screen width (see <see cref="DeviceFormClassifier"/>).
/// </summary>
internal sealed class AnalyticsInfoAndroidExtension : IAnalyticsInfoExtension
{
    /// <summary>The Android system feature of PC-class devices (Android desktops).</summary>
    internal const string PcFeature = "android.hardware.type.pc";

    /// <inheritdoc />
    public CodeBrixDeviceForm GetDeviceForm()
    {
        var context = AndroidContext.Current;
        var configuration = context.Resources.Configuration;
        var hasPcFeature = context.PackageManager?.HasSystemFeature(PcFeature) == true;
        return (CodeBrixDeviceForm)(int)DeviceFormClassifier.Classify(
            (int)configuration.UiMode,
            hasPcFeature,
            configuration.SmallestScreenWidthDp);
    }
}
