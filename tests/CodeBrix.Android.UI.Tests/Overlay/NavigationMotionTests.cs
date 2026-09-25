using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

[Collection(HostFreeCoreCollection.Name)]
public class NavigationMotionTests
{
    public NavigationMotionTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void The_first_page_arrives_without_motion()
    {
        //Act
        var motion = NavigationMotion.For(new EntranceNavigationTransitionInfo(), NavigationMode.New, hasCurrentPage: false);

        //Assert
        motion.Kind.Should().Be(NavigationMotionKind.None);
    }

    [Fact]
    public void The_default_motion_is_the_shared_axis_on_z()
    {
        //Act
        var motion = NavigationMotion.For(null, NavigationMode.New, hasCurrentPage: true);

        //Assert
        motion.Should().Be(new NavigationMotion(NavigationMotionKind.SharedAxisZ, Forward: true));
    }

    [Fact]
    public void Slide_is_the_shared_axis_on_x_and_back_plays_it_in_reverse()
    {
        //Act
        var motion = NavigationMotion.For(new SlideNavigationTransitionInfo(), NavigationMode.Back, hasCurrentPage: true);

        //Assert
        motion.Should().Be(new NavigationMotion(NavigationMotionKind.SharedAxisX, Forward: false));
    }

    [Fact]
    public void Drill_in_is_the_fade_through()
    {
        //Act
        var motion = NavigationMotion.For(new DrillInNavigationTransitionInfo(), NavigationMode.New, hasCurrentPage: true);

        //Assert
        motion.Kind.Should().Be(NavigationMotionKind.FadeThrough);
    }

    [Fact]
    public void Suppress_has_no_motion()
    {
        //Act
        var motion = NavigationMotion.For(new SuppressNavigationTransitionInfo(), NavigationMode.New, hasCurrentPage: true);

        //Assert
        motion.Kind.Should().Be(NavigationMotionKind.None);
    }
}
