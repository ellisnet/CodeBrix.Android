using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.Android;
using Windows.Devices.Haptics;
using AContext = global::Android.Content.Context;
using AVibratorManager = global::Android.OS.VibratorManager;

namespace CodeBrix.Android.Services;

/// <summary>
/// SimpleHapticsController on Android: the short system haptics of the view hierarchy
/// (View.PerformHapticFeedback - no VIBRATE permission): Click, Press, Release, Hover, Success and Error
/// (<see cref="HapticWaveforms"/>). The continuous waveforms are not offered.
/// </summary>
internal sealed class SimpleHapticsControllerAndroidExtension : ISimpleHapticsControllerExtension
{
    private static readonly IReadOnlyList<SimpleHapticsControllerFeedback> _supported =
        HapticWaveforms.Supported.Select(w => new SimpleHapticsControllerFeedback(w, TimeSpan.Zero)).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<SimpleHapticsControllerFeedback> SupportedFeedback => _supported;

    /// <inheritdoc />
    public void SendHapticFeedback(SimpleHapticsControllerFeedback feedback)
    {
        if (feedback == null || HapticWaveforms.ToAndroid(feedback.Waveform) is not { } constant)
        {
            return;
        }

        var view = AndroidActivityBridge.Current?.CurrentActivity?.Window?.DecorView;
        view?.PerformHapticFeedback((global::Android.Views.FeedbackConstants)constant);
    }
}

/// <summary>VibrationDevice on Android: always allowed; available when the device has a vibrator.</summary>
internal sealed class VibrationDeviceAndroidExtension : IVibrationDeviceExtension
{
    /// <inheritdoc />
    public VibrationAccessStatus AccessStatus => VibrationAccessStatus.Allowed;

    /// <inheritdoc />
    public bool IsAvailable =>
        AndroidContext.Current.GetSystemService(AContext.VibratorManagerService) is AVibratorManager manager
        && manager.DefaultVibrator.HasVibrator;
}
