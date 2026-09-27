using CodeBrix.Android.UI.Handlers;
using SilverAssertions;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.Toolkit.Tests.Handlers;

public class TriPaneTouchTargetTests
{
    [Fact]
    public void A_thin_side_divider_grows_left_and_right_to_the_minimum_centred_on_itself()
    {
        //Arrange
        var bounds = new Rect(100, 0, 6, 500);

        //Act
        var target = TriPaneTouchTarget.Inflate(bounds, isVertical: true, 48);

        //Assert
        target.Should().Be(new Rect(79, 0, 48, 500));
    }

    [Fact]
    public void A_thin_stack_divider_grows_up_and_down_to_the_minimum_centred_on_itself()
    {
        //Arrange
        var bounds = new Rect(0, 200, 400, 6);

        //Act
        var target = TriPaneTouchTarget.Inflate(bounds, isVertical: false, 48);

        //Assert
        target.Should().Be(new Rect(0, 179, 400, 48));
    }

    [Fact]
    public void A_divider_thicker_than_the_minimum_keeps_its_bounds()
    {
        //Arrange
        var bounds = new Rect(10, 0, 60, 300);

        //Act
        var target = TriPaneTouchTarget.Inflate(bounds, isVertical: true, 48);

        //Assert
        target.Should().Be(bounds);
    }

    [Fact]
    public void An_empty_bounds_stays_empty()
    {
        //Arrange
        //Act
        var target = TriPaneTouchTarget.Inflate(Rect.Empty, isVertical: true, 48);

        //Assert
        target.IsEmpty.Should().BeTrue();
    }
}
