namespace CodeBrix.Android.UI.Policy;

/// <summary>How CodeBrix.Android treats the system's animation setting (the animator duration scale).</summary>
internal enum MotionMode
{
    /// <summary>
    /// The default. Native Material widgets follow the system setting (Android's convention: with the
    /// animator duration scale at 0 their animations are removed); Core Storyboards always run, as WinUI
    /// runs an application's own Storyboards whatever the system's animation-effects setting.
    /// </summary>
    FollowSystem,

    /// <summary>
    /// Everything moves even when the system removed animations: native indeterminate progress indicators
    /// that the system froze are driven by the CodeBrix motion clock instead (kiosk and demo applications,
    /// the UIReqs harness, which removes system animations for stable frames).
    /// </summary>
    AlwaysAnimate,
}

/// <summary>
/// The motion policy (plan 2.11 "Motion", 2.18): Core Storyboards and animations are ticked from the Android
/// frame clock (Choreographer, Platform/Animation/CoreAnimationTicker); the system animation setting decides
/// what native widgets do; Frame transitions map to Material motion (Overlay/Portable/NavigationMotion).
/// </summary>
internal static class MotionPolicy
{
    /// <summary>The motion mode (default <see cref="MotionMode.FollowSystem"/>).</summary>
    internal static MotionMode Mode { get; set; } = MotionMode.FollowSystem;

    /// <summary>
    /// True when Core Storyboards are ticked. Always true: WinUI runs an application's Storyboards whatever
    /// the system animation setting (only theme and system animations consult UISettings.AnimationsEnabled).
    /// </summary>
    internal static bool TicksCoreAnimations => true;

    /// <summary>True when a native animation the system removed must be driven by CodeBrix instead.</summary>
    /// <param name="systemAnimatorsEnabled">What the system says (ValueAnimator.AreAnimatorsEnabled()).</param>
    /// <returns>True when CodeBrix drives the animation itself.</returns>
    internal static bool DrivesFrozenNativeAnimations(bool systemAnimatorsEnabled) =>
        !systemAnimatorsEnabled && Mode == MotionMode.AlwaysAnimate;

    /// <summary>True when native widgets animate at all (their own animators, or the CodeBrix-driven replacement).</summary>
    /// <param name="systemAnimatorsEnabled">What the system says.</param>
    /// <returns>True when native motion is shown.</returns>
    internal static bool NativeMotionShown(bool systemAnimatorsEnabled) =>
        systemAnimatorsEnabled || Mode == MotionMode.AlwaysAnimate;

    /// <summary>
    /// The position of a CodeBrix-driven indeterminate sweep: the fraction of the track the indicator covers
    /// at <paramref name="elapsedMilliseconds"/> into the cycle. It grows from 10 % to 100 % over a cycle and
    /// starts again, so the indicator is always visible and always moving.
    /// </summary>
    /// <param name="elapsedMilliseconds">Time since the sweep started.</param>
    /// <param name="cycleMilliseconds">The cycle length (positive).</param>
    /// <returns>The covered fraction, 0.1 to 1.</returns>
    internal static double SweepFraction(double elapsedMilliseconds, double cycleMilliseconds)
    {
        if (cycleMilliseconds <= 0 || double.IsNaN(elapsedMilliseconds) || elapsedMilliseconds < 0)
        {
            return 0.1;
        }

        var phase = (elapsedMilliseconds % cycleMilliseconds) / cycleMilliseconds;
        return 0.1 + (0.9 * phase);
    }

    /// <summary>Puts the policy back to its defaults.</summary>
    internal static void Reset() => Mode = MotionMode.FollowSystem;
}
