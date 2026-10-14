using CodeBrix.Android.UI.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Input;

/// <summary>The button names are the standard layout's, spelled exactly as a desktop controller backend reports them.</summary>
public class GamepadButtonsTests
{
    [Fact]
    public void The_names_are_the_standard_layout_in_its_order()
    {
        //Assert
        GamepadButtons.All.Should().Equal(
            "A", "B", "X", "Y",
            "Back", "Guide", "Start",
            "LeftStick", "RightStick",
            "LeftShoulder", "RightShoulder",
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight");
    }

    [Fact]
    public void Every_constant_is_in_the_list()
    {
        //Assert
        GamepadButtons.All.Should().Contain(GamepadButtons.A);
        GamepadButtons.All.Should().Contain(GamepadButtons.B);
        GamepadButtons.All.Should().Contain(GamepadButtons.X);
        GamepadButtons.All.Should().Contain(GamepadButtons.Y);
        GamepadButtons.All.Should().Contain(GamepadButtons.Back);
        GamepadButtons.All.Should().Contain(GamepadButtons.Guide);
        GamepadButtons.All.Should().Contain(GamepadButtons.Start);
        GamepadButtons.All.Should().Contain(GamepadButtons.LeftStick);
        GamepadButtons.All.Should().Contain(GamepadButtons.RightStick);
        GamepadButtons.All.Should().Contain(GamepadButtons.LeftShoulder);
        GamepadButtons.All.Should().Contain(GamepadButtons.RightShoulder);
        GamepadButtons.All.Should().Contain(GamepadButtons.DPadUp);
        GamepadButtons.All.Should().Contain(GamepadButtons.DPadDown);
        GamepadButtons.All.Should().Contain(GamepadButtons.DPadLeft);
        GamepadButtons.All.Should().Contain(GamepadButtons.DPadRight);
        GamepadButtons.All.Count.Should().Be(15);
    }
}
