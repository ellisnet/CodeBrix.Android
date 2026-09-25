using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.Portable.Layout;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP2-M2: the room a native navigation container's chrome and content take with the system bars absorbed.</summary>
public class NavigationChromeInsetsTests
{
    private static readonly SafeAreaPadding Bars = new(0, 24, 0, 48);

    [Fact]
    public void Without_a_safe_area_the_content_gets_the_room_the_chrome_leaves()
    {
        //Act
        var bottom = NavigationChromeInsets.Content(NavigationContainer.BottomBar, 320, SafeAreaPadding.Empty, contentAbsorbs: false);
        var rail = NavigationChromeInsets.Content(NavigationContainer.Rail, 320, SafeAreaPadding.Empty, contentAbsorbs: false);
        var drawer = NavigationChromeInsets.Content(NavigationContainer.PersistentDrawer, 320, SafeAreaPadding.Empty, contentAbsorbs: false);
        var modal = NavigationChromeInsets.Content(NavigationContainer.ModalDrawer, 320, SafeAreaPadding.Empty, contentAbsorbs: false);

        //Assert
        bottom.Should().Be(new SafeAreaPadding(0, 0, 0, 80));
        rail.Should().Be(new SafeAreaPadding(80, 0, 0, 0));
        drawer.Should().Be(new SafeAreaPadding(320, 0, 0, 0));
        modal.Should().Be(new SafeAreaPadding(0, 64, 0, 0));
    }

    [Fact]
    public void A_bottom_bar_grows_by_the_navigation_bar_and_the_content_starts_under_the_status_bar()
    {
        //Act
        var content = NavigationChromeInsets.Content(NavigationContainer.BottomBar, 320, Bars, contentAbsorbs: false);
        var padding = NavigationChromeInsets.ChromePadding(NavigationContainer.BottomBar, Bars);

        //Assert
        content.Should().Be(new SafeAreaPadding(0, 24, 0, 128));
        padding.Should().Be(new SafeAreaPadding(0, 0, 0, 48));
    }

    [Fact]
    public void A_rail_pads_its_destinations_below_the_status_bar_and_the_content_is_clear_of_both_bars()
    {
        //Act
        var content = NavigationChromeInsets.Content(NavigationContainer.Rail, 320, Bars, contentAbsorbs: false);
        var padding = NavigationChromeInsets.ChromePadding(NavigationContainer.Rail, Bars);

        //Assert
        content.Should().Be(new SafeAreaPadding(80, 24, 0, 48));
        padding.Should().Be(new SafeAreaPadding(0, 24, 0, 48));
    }

    [Fact]
    public void A_left_cutout_widens_the_drawer_and_moves_the_content()
    {
        //Arrange
        var cutout = new SafeAreaPadding(30, 24, 0, 0);

        //Act
        var content = NavigationChromeInsets.Content(NavigationContainer.PersistentDrawer, 320, cutout, contentAbsorbs: false);
        var padding = NavigationChromeInsets.ChromePadding(NavigationContainer.PersistentDrawer, cutout);

        //Assert
        content.Should().Be(new SafeAreaPadding(350, 24, 0, 0));
        padding.Should().Be(new SafeAreaPadding(30, 24, 0, 0));
    }

    [Fact]
    public void The_modal_top_bar_grows_by_the_status_bar()
    {
        //Act
        var content = NavigationChromeInsets.Content(NavigationContainer.ModalDrawer, 320, Bars, contentAbsorbs: false);
        var padding = NavigationChromeInsets.ChromePadding(NavigationContainer.ModalDrawer, Bars);

        //Assert
        content.Should().Be(new SafeAreaPadding(0, 88, 0, 48));
        padding.Should().Be(new SafeAreaPadding(0, 24, 0, 0));
    }

    [Fact]
    public void A_page_content_absorbs_the_edges_the_chrome_does_not_take_itself()
    {
        //Act
        var bottom = NavigationChromeInsets.Content(NavigationContainer.BottomBar, 320, Bars, contentAbsorbs: true);
        var rail = NavigationChromeInsets.Content(NavigationContainer.Rail, 320, Bars, contentAbsorbs: true);

        //Assert
        bottom.Should().Be(new SafeAreaPadding(0, 0, 0, 128));
        rail.Should().Be(new SafeAreaPadding(80, 0, 0, 0));
    }

    [Fact]
    public void The_template_takes_no_room()
    {
        //Act
        var content = NavigationChromeInsets.Content(NavigationContainer.Template, 320, Bars, contentAbsorbs: false);

        //Assert
        content.Should().Be(SafeAreaPadding.Empty);
    }
}
