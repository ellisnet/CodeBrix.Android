using CodeBrix.Android.UI.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Input;

/// <summary>Which axes a controller reports decides how its sticks and triggers are read.</summary>
public class GamepadAxisProfileTests
{
    [Fact]
    public void An_empty_profile_has_nothing()
    {
        //Assert
        GamepadAxisProfile.Empty.IsEmpty.Should().BeTrue();
        GamepadAxisProfile.Empty.HasLeftStick.Should().BeFalse();
        GamepadAxisProfile.Empty.HasRightStick.Should().BeFalse();
        GamepadAxisProfile.Empty.HasTriggerAxes.Should().BeFalse();
        GamepadAxisProfile.Empty.HasHat.Should().BeFalse();
        GamepadAxisProfile.Empty.Describe().Should().Be("no axes reported");
    }

    [Fact]
    public void A_full_modern_layout_prefers_z_rz_and_the_trigger_axes()
    {
        //Arrange
        var profile = GamepadAxisProfile.FromAxes(new[]
        {
            new GamepadAxisRange(AndroidGamepadCodes.AxisX, 0.1f), new GamepadAxisRange(AndroidGamepadCodes.AxisY, 0.1f),
            new GamepadAxisRange(AndroidGamepadCodes.AxisZ, 0.1f), new GamepadAxisRange(AndroidGamepadCodes.AxisRZ, 0.1f),
            new GamepadAxisRange(AndroidGamepadCodes.AxisRX), new GamepadAxisRange(AndroidGamepadCodes.AxisRY),
            new GamepadAxisRange(AndroidGamepadCodes.AxisLeftTrigger), new GamepadAxisRange(AndroidGamepadCodes.AxisRightTrigger),
            new GamepadAxisRange(AndroidGamepadCodes.AxisBrake), new GamepadAxisRange(AndroidGamepadCodes.AxisGas),
            new GamepadAxisRange(AndroidGamepadCodes.AxisHatX), new GamepadAxisRange(AndroidGamepadCodes.AxisHatY),
        });

        //Assert
        profile.IsEmpty.Should().BeFalse();
        profile.HasLeftStick.Should().BeTrue();
        profile.RightStickUsesZ.Should().BeTrue();
        profile.RightStickUsesRxRy.Should().BeFalse();
        profile.TriggersUseTriggerAxes.Should().BeTrue();
        profile.TriggersUseGasBrake.Should().BeFalse();
        profile.HasHat.Should().BeTrue();
        profile.FlatOf(AndroidGamepadCodes.AxisX).Should().Be(0.1f);
        profile.FlatOf(AndroidGamepadCodes.AxisRX).Should().Be(0f);
        profile.FlatOf(1234).Should().Be(0f);
        profile.Describe().Should().StartWith("left stick X/Y; right stick Z/RZ; triggers LTRIGGER/RTRIGGER; hat yes; axes ");
    }

    [Fact]
    public void A_layout_without_z_rz_or_trigger_axes_falls_back_to_rx_ry_and_brake_gas()
    {
        //Arrange
        var profile = GamepadAxisProfile.FromAxes(new[]
        {
            new GamepadAxisRange(AndroidGamepadCodes.AxisX), new GamepadAxisRange(AndroidGamepadCodes.AxisY),
            new GamepadAxisRange(AndroidGamepadCodes.AxisRX), new GamepadAxisRange(AndroidGamepadCodes.AxisRY),
            new GamepadAxisRange(AndroidGamepadCodes.AxisBrake), new GamepadAxisRange(AndroidGamepadCodes.AxisGas),
        });

        //Assert
        profile.RightStickUsesZ.Should().BeFalse();
        profile.RightStickUsesRxRy.Should().BeTrue();
        profile.HasRightStick.Should().BeTrue();
        profile.TriggersUseTriggerAxes.Should().BeFalse();
        profile.TriggersUseGasBrake.Should().BeTrue();
        profile.HasTriggerAxes.Should().BeTrue();
        profile.HasHat.Should().BeFalse();
        profile.Describe().Should().StartWith("left stick X/Y; right stick RX/RY; triggers BRAKE/GAS; hat no; axes ");
    }

    [Fact]
    public void A_negative_or_non_finite_flat_is_zero()
    {
        //Assert
        new GamepadAxisRange(AndroidGamepadCodes.AxisX, -0.5f).Flat.Should().Be(0f);
        new GamepadAxisRange(AndroidGamepadCodes.AxisX, float.NaN).Flat.Should().Be(0f);
        new GamepadAxisRange(AndroidGamepadCodes.AxisX, 0.25f).Flat.Should().Be(0.25f);
    }

    [Fact]
    public void A_repeated_axis_keeps_the_last_flat()
    {
        //Arrange
        var profile = GamepadAxisProfile.FromAxes(new[]
        {
            new GamepadAxisRange(AndroidGamepadCodes.AxisX, 0.1f),
            new GamepadAxisRange(AndroidGamepadCodes.AxisX, 0.3f),
        });

        //Assert
        profile.Axes.Count.Should().Be(1);
        profile.FlatOf(AndroidGamepadCodes.AxisX).Should().Be(0.3f);
    }
}
