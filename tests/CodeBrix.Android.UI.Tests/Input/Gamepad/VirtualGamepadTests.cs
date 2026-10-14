using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Input;

/// <summary>A scripted gamepad (tests, PlayTests, synthetic input) changes the same state a controller would.</summary>
public class VirtualGamepadTests
{
    [Fact]
    public void A_virtual_gamepad_is_listed_like_a_device()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var connected = new List<Gamepad>();
        tracker.GamepadConnected += (_, e) => connected.Add(e.Gamepad);

        //Act
        var first = tracker.AddVirtual("Player 1");
        var second = tracker.AddVirtual();

        //Assert
        tracker.ConnectedGamepads.Should().Equal(first.Gamepad, second.Gamepad);
        connected.Should().Equal(first.Gamepad, second.Gamepad);
        first.Gamepad.Id.Should().Be("virtual:0");
        second.Gamepad.Id.Should().Be("virtual:1");
        first.Gamepad.DeviceId.Should().BeLessThan(0);
        first.Gamepad.Name.Should().Be("Player 1");
        second.Gamepad.Name.Should().Be("Virtual gamepad");
        first.Gamepad.Info.Origin.Should().Be(GamepadOrigin.Virtual);
    }

    [Fact]
    public void Buttons_sticks_and_triggers_follow_the_script_and_raise_state_changed()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var pad = tracker.AddVirtual();
        var changes = 0;
        tracker.GamepadStateChanged += (_, e) => { e.Gamepad.Should().BeSameAs(pad.Gamepad); changes++; };

        //Act
        pad.Press(GamepadButtons.A);
        pad.Press(GamepadButtons.LeftShoulder);
        pad.SetLeftStick(-1f, 0.5f);
        pad.SetRightStick(0.25f, -0.25f);
        pad.SetLeftTrigger(0.5f);
        pad.SetRightTrigger(1f);

        //Assert
        pad.Gamepad.PressedButtons.Should().BeEquivalentTo(new[] { GamepadButtons.A, GamepadButtons.LeftShoulder });
        pad.Gamepad.LeftStick.Should().Be(new GamepadStick(-1f, 0.5f));
        pad.Gamepad.RightStick.Should().Be(new GamepadStick(0.25f, -0.25f));
        pad.Gamepad.LeftTrigger.Should().Be(0.5f);
        pad.Gamepad.RightTrigger.Should().Be(1f);
        changes.Should().Be(6);

        //Act
        pad.Release(GamepadButtons.A);
        pad.Release(GamepadButtons.A);

        //Assert
        pad.Gamepad.PressedButtons.Should().Equal(GamepadButtons.LeftShoulder);
        changes.Should().Be(7);

        //Act
        pad.ReleaseAll();

        //Assert
        pad.Gamepad.PressedButtons.Should().BeEmpty();
        pad.Gamepad.LeftStick.Should().Be(GamepadStick.Centered);
        pad.Gamepad.RightStick.Should().Be(GamepadStick.Centered);
        pad.Gamepad.LeftTrigger.Should().Be(0f);
        pad.Gamepad.RightTrigger.Should().Be(0f);
        changes.Should().Be(8);
        pad.ReleaseAll();
        changes.Should().Be(8);
    }

    [Fact]
    public void A_tap_presses_then_releases()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var pad = tracker.AddVirtual();
        var seen = new List<bool>();
        tracker.GamepadStateChanged += (_, e) => seen.Add(e.Gamepad.IsPressed(GamepadButtons.Start));

        //Act
        pad.Tap(GamepadButtons.Start);

        //Assert
        seen.Should().Equal(true, false);
        pad.Gamepad.PressedButtons.Should().BeEmpty();
    }

    [Fact]
    public void An_unknown_button_name_is_refused()
    {
        //Arrange
        var pad = new GamepadTracker().AddVirtual();

        //Act / Assert
        Assert.Throws<ArgumentException>(() => pad.Press("Cross"));
        Assert.Throws<ArgumentException>(() => pad.Press("a"));
        Assert.Throws<ArgumentNullException>(() => pad.Release(null));
    }

    [Fact]
    public void Disconnecting_removes_it_and_refuses_further_input()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var pad = tracker.AddVirtual();
        pad.Press(GamepadButtons.B);
        var disconnected = new List<Gamepad>();
        tracker.GamepadDisconnected += (_, e) => disconnected.Add(e.Gamepad);

        //Act
        pad.Disconnect();

        //Assert
        tracker.ConnectedGamepads.Should().BeEmpty();
        disconnected.Should().Equal(pad.Gamepad);
        pad.Gamepad.IsConnected.Should().BeFalse();
        pad.Gamepad.PressedButtons.Should().BeEmpty();
        Assert.Throws<InvalidOperationException>(() => pad.Press(GamepadButtons.B));
    }

    [Fact]
    public void Stick_values_are_clamped_to_the_unit_range()
    {
        //Arrange
        var pad = new GamepadTracker().AddVirtual();

        //Act
        pad.SetLeftStick(3f, -3f);
        pad.SetLeftTrigger(2f);

        //Assert
        pad.Gamepad.LeftStick.Should().Be(new GamepadStick(1f, -1f));
        pad.Gamepad.LeftStick.Magnitude.Should().BeGreaterThan(1.41f);
        pad.Gamepad.LeftTrigger.Should().Be(1f);
    }
}
