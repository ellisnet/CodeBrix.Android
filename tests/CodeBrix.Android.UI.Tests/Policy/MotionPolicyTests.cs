using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

public class MotionPolicyTests
{
    public MotionPolicyTests() => MotionPolicy.Reset();

    [Fact]
    public void TicksCoreAnimations_is_always_true_as_WinUI_runs_an_apps_Storyboards() => MotionPolicy.TicksCoreAnimations.Should().BeTrue();

    [Fact]
    public void DrivesFrozenNativeAnimations_only_when_the_system_removed_them_and_the_policy_says_always_animate()
    {
        //Assert
        MotionPolicy.DrivesFrozenNativeAnimations(systemAnimatorsEnabled: false).Should().BeFalse("FollowSystem leaves them still");
        MotionPolicy.Mode = MotionMode.AlwaysAnimate;
        MotionPolicy.DrivesFrozenNativeAnimations(systemAnimatorsEnabled: false).Should().BeTrue();
        MotionPolicy.DrivesFrozenNativeAnimations(systemAnimatorsEnabled: true).Should().BeFalse("the system animates them itself");
        MotionPolicy.Reset();
    }

    [Fact]
    public void NativeMotionShown_follows_the_system_unless_always_animate()
    {
        //Assert
        MotionPolicy.NativeMotionShown(true).Should().BeTrue();
        MotionPolicy.NativeMotionShown(false).Should().BeFalse();
        MotionPolicy.Mode = MotionMode.AlwaysAnimate;
        MotionPolicy.NativeMotionShown(false).Should().BeTrue();
        MotionPolicy.Reset();
    }

    [Theory]
    [InlineData(0, 0.1)]
    [InlineData(750, 0.55)]
    [InlineData(1500, 0.1)]
    [InlineData(2250, 0.55)]
    public void SweepFraction_grows_from_a_tenth_to_all_of_the_track_each_cycle(double elapsed, double expected) =>
        MotionPolicy.SweepFraction(elapsed, 1500).Should().BeApproximately(expected, 1e-9);

    [Theory]
    [InlineData(-5, 1500)]
    [InlineData(100, 0)]
    [InlineData(double.NaN, 1500)]
    public void SweepFraction_starts_at_a_tenth_for_meaningless_input(double elapsed, double cycle) =>
        MotionPolicy.SweepFraction(elapsed, cycle).Should().Be(0.1);
}
