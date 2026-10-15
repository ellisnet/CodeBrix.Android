using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Hosting;

/// <summary>
/// ActivityInstancePolicy: the host's single-instance decisions. The first activity starts the XAML application; a
/// re-created activity (or one replacing a finishing host) shows the existing window; a second instance created while
/// another live activity shows the window forwards its launch and finishes; the host's own bring-to-front intent is
/// never surfaced as a launch.
/// </summary>
public sealed class ActivityInstancePolicyTests
{
    [Fact]
    public void the_first_activity_starts_the_xaml_application()
    {
        //Act
        var action = ActivityInstancePolicy.Decide(windowExists: false, windowHostedByAnotherLiveActivity: false);

        //Assert
        action.Should().Be(ActivityCreateAction.StartApplication);
    }

    [Fact]
    public void a_second_instance_while_another_live_activity_hosts_the_window_forwards_and_finishes()
    {
        //Arrange
        var hostedElsewhere = ActivityInstancePolicy.IsHostedByAnotherLiveActivity(
            hasHost: true, hostIsThisActivity: false, hostIsDestroyed: false, hostIsFinishing: false);

        //Act
        var action = ActivityInstancePolicy.Decide(windowExists: true, hostedElsewhere);

        //Assert
        hostedElsewhere.Should().BeTrue();
        action.Should().Be(ActivityCreateAction.ForwardToRunningInstance);
    }

    [Fact]
    public void a_re_created_activity_shows_the_existing_window_when_no_activity_hosts_it()
    {
        //Arrange - the previous activity was destroyed and let go of the window (DetachActivity)
        var hostedElsewhere = ActivityInstancePolicy.IsHostedByAnotherLiveActivity(
            hasHost: false, hostIsThisActivity: false, hostIsDestroyed: false, hostIsFinishing: false);

        //Act
        var action = ActivityInstancePolicy.Decide(windowExists: true, hostedElsewhere);

        //Assert
        action.Should().Be(ActivityCreateAction.ShowExistingWindow);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void a_host_that_is_destroyed_or_finishing_does_not_count_so_the_new_activity_shows_the_window(bool destroyed, bool finishing)
    {
        //Arrange
        var hostedElsewhere = ActivityInstancePolicy.IsHostedByAnotherLiveActivity(
            hasHost: true, hostIsThisActivity: false, hostIsDestroyed: destroyed, hostIsFinishing: finishing);

        //Act
        var action = ActivityInstancePolicy.Decide(windowExists: true, hostedElsewhere);

        //Assert
        hostedElsewhere.Should().BeFalse();
        action.Should().Be(ActivityCreateAction.ShowExistingWindow);
    }

    [Fact]
    public void the_activity_that_already_hosts_the_window_is_not_another_host()
    {
        //Act
        var hostedElsewhere = ActivityInstancePolicy.IsHostedByAnotherLiveActivity(
            hasHost: true, hostIsThisActivity: true, hostIsDestroyed: false, hostIsFinishing: false);

        //Assert
        hostedElsewhere.Should().BeFalse();
    }

    [Fact]
    public void a_launch_intent_is_surfaced_but_the_hosts_own_bring_to_front_intent_is_not()
    {
        //Act + Assert
        ActivityInstancePolicy.SurfacesAsLaunch(isBringToFrontIntent: false).Should().BeTrue();
        ActivityInstancePolicy.SurfacesAsLaunch(isBringToFrontIntent: true).Should().BeFalse();
    }

    [Fact]
    public void the_bring_to_front_marker_is_namespaced_to_the_host()
    {
        //Assert - an app's own extras never collide with it
        ActivityInstancePolicy.BringToFrontExtra.Should().StartWith("com.codebrix.android.host.");
    }
}
