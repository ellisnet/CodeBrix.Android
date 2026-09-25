using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

public class WindowSizeClassTests
{
    [Theory]
    [InlineData(411, "Compact")]
    [InlineData(599.9, "Compact")]
    [InlineData(600, "Medium")]
    [InlineData(839.9, "Medium")]
    [InlineData(840, "Expanded")]
    [InlineData(1920, "Expanded")]
    public void Width_follows_the_Material_breakpoints(double widthDp, string expectedName) =>
        new WindowSizeClass(widthDp, 800, false).Width.ToString().Should().Be(expectedName);

    [Theory]
    [InlineData(479.9, "Compact")]
    [InlineData(480, "Medium")]
    [InlineData(899.9, "Medium")]
    [InlineData(900, "Expanded")]
    public void HeightFrom_follows_the_Material_breakpoints(double heightDp, string expectedName) =>
        WindowSizeClass.HeightFrom(heightDp).ToString().Should().Be(expectedName);

    [Fact]
    public void Unknown_is_Expanded_without_a_fine_pointer()
    {
        //Act
        var unknown = WindowSizeClass.Unknown;

        //Assert
        unknown.Width.Should().Be(WindowWidthClass.Expanded);
        unknown.Height.Should().Be(WindowHeightClass.Expanded);
        unknown.FinePointer.Should().BeFalse();
    }

    [Fact]
    public void SameClassesAs_ignores_a_resize_within_the_classes()
    {
        //Arrange
        var before = new WindowSizeClass(1080, 1920, false);

        //Assert
        before.SameClassesAs(new WindowSizeClass(1200, 1000, false)).Should().BeTrue("both are Expanded wide and Expanded tall");
        before.SameClassesAs(new WindowSizeClass(1920, 1080, false)).Should().BeTrue("a rotated 15-inch window is Expanded both ways");
        before.Equals(new WindowSizeClass(1200, 1000, false)).Should().BeFalse("the sizes differ");
    }

    [Fact]
    public void SameClassesAs_sees_a_class_or_pointer_change()
    {
        //Arrange
        var docked = new WindowSizeClass(1920, 1080, true);

        //Assert
        docked.SameClassesAs(new WindowSizeClass(1920, 1080, false)).Should().BeFalse("the fine pointer is part of the classes");
        docked.SameClassesAs(new WindowSizeClass(411, 915, true)).Should().BeFalse("Compact is another width class");
    }

    [Fact]
    public void ToString_names_the_classes_and_the_size() =>
        new WindowSizeClass(411, 915, true).ToString().Should().Be("Compact x Expanded (411 x 915 dp, fine pointer)");
}
