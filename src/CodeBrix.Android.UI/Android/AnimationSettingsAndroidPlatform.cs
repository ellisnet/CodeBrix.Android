using CodeBrix.Platform.Contracts;
using AValueAnimator = global::Android.Animation.ValueAnimator;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of the optional IAnimationSettingsPlatform contract (pin 1.0.268.12, WPE1-1 C0c):
/// UISettings.AnimationsEnabled follows the system's animator duration scale (Settings &gt; Accessibility &gt;
/// Remove animations, the developer options' animator scale) - false while it is 0. Read live on every call, so a
/// change the <see cref="Platform.Animation.AnimatorScaleMonitor"/> sees is also what Core reads.
/// </summary>
internal sealed class AnimationSettingsAndroidPlatform : IAnimationSettingsPlatform
{
    /// <inheritdoc />
    public bool AnimationsEnabled => AValueAnimator.AreAnimatorsEnabled();
}
