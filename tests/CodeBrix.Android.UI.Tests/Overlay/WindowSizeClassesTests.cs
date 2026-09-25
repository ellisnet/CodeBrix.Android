using CodeBrix.Android.UI.Overlay;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

public class WindowSizeClassesTests
{
    [Theory]
    [InlineData(411, "Compact")]
    [InlineData(599.9, "Compact")]
    [InlineData(600, "Medium")]
    [InlineData(839, "Medium")]
    [InlineData(840, "Expanded")]
    [InlineData(1920, "Expanded")]
    public void The_material_breakpoints_classify_the_width(double width, string expected)
    {
        //Act & Assert
        WindowSizeClasses.FromWidth(width).ToString().Should().Be(expected);
    }

    [Fact]
    public void An_unknown_width_counts_as_expanded()
    {
        //Act & Assert
        WindowSizeClasses.FromWidth(0).Should().Be(WindowWidthClass.Expanded);
        WindowSizeClasses.FromWidth(double.NaN).Should().Be(WindowWidthClass.Expanded);
    }

    [Fact]
    public void The_override_wins_over_the_width()
    {
        //Arrange
        OverlayPresentation.WidthClassOverride = WindowWidthClass.Compact;
        try
        {
            //Act & Assert
            OverlayPresentation.WidthClassFor(1920).Should().Be(WindowWidthClass.Compact);
        }
        finally
        {
            OverlayPresentation.Reset();
        }
    }
}
