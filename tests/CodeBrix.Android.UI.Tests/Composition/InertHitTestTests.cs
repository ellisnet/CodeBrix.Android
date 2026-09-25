using CodeBrix.Android.UI.Composition.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Composition;

public class InertHitTestTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(50, 20, true)]
    [InlineData(100, 40, true)]
    [InlineData(-1, 10, false)]
    [InlineData(10, 41, false)]
    public void IsInside_is_a_bounds_test_of_the_visual_size(double x, double y, bool expected)
    {
        //Act
        var hit = InertHitTest.IsInside(x, y, 100, 40);

        //Assert
        hit.Should().Be(expected);
    }

    [Fact]
    public void IsInside_never_hits_an_empty_visual()
    {
        //Act
        var hit = InertHitTest.IsInside(0, 0, 0, 10);

        //Assert
        hit.Should().BeFalse();
    }
}
