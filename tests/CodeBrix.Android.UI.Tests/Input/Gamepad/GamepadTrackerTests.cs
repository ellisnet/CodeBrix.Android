using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Input;

/// <summary>
/// The gamepad state machine, driven with the plain key codes and axis values the activity would hand it:
/// which events it consumes, how buttons, sticks, triggers and the directional pad are read, and its events.
/// </summary>
public class GamepadTrackerTests
{
    private const int Device = 7;

    private static GamepadDeviceInfo ModernController() => new(
        "Test controller",
        GamepadOrigin.Device,
        GamepadAxisProfile.FromAxes(new[]
        {
            new GamepadAxisRange(AndroidGamepadCodes.AxisX, 0.1f), new GamepadAxisRange(AndroidGamepadCodes.AxisY, 0.1f),
            new GamepadAxisRange(AndroidGamepadCodes.AxisZ, 0.1f), new GamepadAxisRange(AndroidGamepadCodes.AxisRZ, 0.1f),
            new GamepadAxisRange(AndroidGamepadCodes.AxisLeftTrigger), new GamepadAxisRange(AndroidGamepadCodes.AxisRightTrigger),
            new GamepadAxisRange(AndroidGamepadCodes.AxisHatX), new GamepadAxisRange(AndroidGamepadCodes.AxisHatY),
        }),
        vendorId: 0x045E,
        productId: 0x0B13,
        descriptor: "abc");

    private static GamepadDeviceInfo LegacyController() => new(
        "Legacy controller",
        GamepadOrigin.Device,
        GamepadAxisProfile.FromAxes(new[]
        {
            new GamepadAxisRange(AndroidGamepadCodes.AxisX), new GamepadAxisRange(AndroidGamepadCodes.AxisY),
            new GamepadAxisRange(AndroidGamepadCodes.AxisRX), new GamepadAxisRange(AndroidGamepadCodes.AxisRY),
            new GamepadAxisRange(AndroidGamepadCodes.AxisBrake), new GamepadAxisRange(AndroidGamepadCodes.AxisGas),
        }));

    private static GamepadAxisValue[] Axes(params (int Axis, float Value)[] values)
    {
        var result = new GamepadAxisValue[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = new GamepadAxisValue(values[i].Axis, values[i].Value);
        }

        return result;
    }

    [Fact]
    public void Adding_a_device_lists_it_and_raises_connected()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var connected = new List<Gamepad>();
        tracker.GamepadConnected += (_, e) => connected.Add(e.Gamepad);

        //Act
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Assert
        tracker.ConnectedGamepads.Should().Equal(gamepad);
        connected.Should().Equal(gamepad);
        gamepad.Id.Should().Be("android:7");
        gamepad.DeviceId.Should().Be(Device);
        gamepad.Name.Should().Be("Test controller");
        gamepad.IsConnected.Should().BeTrue();
        gamepad.PressedButtons.Should().BeEmpty();
        gamepad.LeftStick.Should().Be(GamepadStick.Centered);
        gamepad.LeftTrigger.Should().Be(0f);
        gamepad.Info.Describe().Should().Be("Test controller [Device, vendor 0x045E product 0x0B13]: " + gamepad.Info.AxisProfile.Describe());
        tracker.TryGetGamepad(Device, out var found).Should().BeTrue();
        found.Should().BeSameAs(gamepad);
    }

    [Fact]
    public void Adding_the_same_device_twice_returns_the_same_gamepad()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var first = tracker.AddDevice(Device, ModernController());

        //Act
        var second = tracker.AddDevice(Device, LegacyController());

        //Assert
        second.Should().BeSameAs(first);
        tracker.ConnectedGamepads.Count.Should().Be(1);
    }

    [Fact]
    public void Removing_a_device_clears_its_state_and_raises_disconnected()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyButtonA, isDown: true, isRepeat: false, fromGamepadSource: true);
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisX, 1f), (AndroidGamepadCodes.AxisY, 0f)));
        var disconnected = new List<Gamepad>();
        tracker.GamepadDisconnected += (_, e) => disconnected.Add(e.Gamepad);

        //Act
        var removed = tracker.RemoveDevice(Device);

        //Assert
        removed.Should().BeTrue();
        tracker.ConnectedGamepads.Should().BeEmpty();
        disconnected.Should().Equal(gamepad);
        gamepad.IsConnected.Should().BeFalse();
        gamepad.PressedButtons.Should().BeEmpty();
        gamepad.LeftStick.Should().Be(GamepadStick.Centered);
        tracker.RemoveDevice(Device).Should().BeFalse();
        tracker.TryGetGamepad(Device, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(AndroidGamepadCodes.KeyButtonA, GamepadButtons.A)]
    [InlineData(AndroidGamepadCodes.KeyDPadCenter, GamepadButtons.A)]
    [InlineData(AndroidGamepadCodes.KeyButtonB, GamepadButtons.B)]
    [InlineData(AndroidGamepadCodes.KeyButtonX, GamepadButtons.X)]
    [InlineData(AndroidGamepadCodes.KeyButtonY, GamepadButtons.Y)]
    [InlineData(AndroidGamepadCodes.KeyButtonSelect, GamepadButtons.Back)]
    [InlineData(AndroidGamepadCodes.KeyButtonMode, GamepadButtons.Guide)]
    [InlineData(AndroidGamepadCodes.KeyButtonStart, GamepadButtons.Start)]
    [InlineData(AndroidGamepadCodes.KeyButtonThumbLeft, GamepadButtons.LeftStick)]
    [InlineData(AndroidGamepadCodes.KeyButtonThumbRight, GamepadButtons.RightStick)]
    [InlineData(AndroidGamepadCodes.KeyButtonL1, GamepadButtons.LeftShoulder)]
    [InlineData(AndroidGamepadCodes.KeyButtonR1, GamepadButtons.RightShoulder)]
    [InlineData(AndroidGamepadCodes.KeyDPadUp, GamepadButtons.DPadUp)]
    [InlineData(AndroidGamepadCodes.KeyDPadDown, GamepadButtons.DPadDown)]
    [InlineData(AndroidGamepadCodes.KeyDPadLeft, GamepadButtons.DPadLeft)]
    [InlineData(AndroidGamepadCodes.KeyDPadRight, GamepadButtons.DPadRight)]
    public void A_button_key_presses_and_releases_its_named_button(int keyCode, string button)
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());
        var changes = 0;
        tracker.GamepadStateChanged += (_, e) => { e.Gamepad.Should().BeSameAs(gamepad); changes++; };

        //Act
        var downConsumed = tracker.HandleKey(Device, keyCode, isDown: true, isRepeat: false, fromGamepadSource: true);

        //Assert
        downConsumed.Should().BeTrue();
        gamepad.PressedButtons.Should().Equal(button);
        gamepad.IsPressed(button).Should().BeTrue();
        changes.Should().Be(1);
        GamepadTracker.ButtonOfKeyCode(keyCode).Should().Be(button);

        //Act
        var upConsumed = tracker.HandleKey(Device, keyCode, isDown: false, isRepeat: false, fromGamepadSource: true);

        //Assert
        upConsumed.Should().BeTrue();
        gamepad.PressedButtons.Should().BeEmpty();
        changes.Should().Be(2);
    }

    [Fact]
    public void A_repeated_key_down_is_consumed_without_a_change()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());
        var changes = 0;
        tracker.GamepadStateChanged += (_, _) => changes++;
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyButtonA, isDown: true, isRepeat: false, fromGamepadSource: true);

        //Act
        var consumed = tracker.HandleKey(Device, AndroidGamepadCodes.KeyButtonA, isDown: true, isRepeat: true, fromGamepadSource: true);

        //Assert
        consumed.Should().BeTrue();
        gamepad.PressedButtons.Should().Equal(GamepadButtons.A);
        changes.Should().Be(1);
    }

    [Theory]
    [InlineData(29)]   // KEYCODE_A
    [InlineData(4)]    // KEYCODE_BACK
    [InlineData(66)]   // KEYCODE_ENTER
    [InlineData(AndroidGamepadCodes.KeyButtonC)]
    [InlineData(AndroidGamepadCodes.KeyButtonZ)]
    public void A_key_that_is_not_a_standard_button_is_not_consumed(int keyCode)
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        var consumed = tracker.HandleKey(Device, keyCode, isDown: true, isRepeat: false, fromGamepadSource: true);

        //Assert
        consumed.Should().BeFalse();
        gamepad.PressedButtons.Should().BeEmpty();
        GamepadTracker.IsGamepadKeyCode(keyCode).Should().BeFalse();
        GamepadTracker.ButtonOfKeyCode(keyCode).Should().BeNull();
    }

    [Fact]
    public void A_keyboard_s_arrow_keys_are_not_a_gamepad_s_d_pad()
    {
        //Arrange
        var tracker = new GamepadTracker();
        const int keyboard = 3;

        //Act
        var consumed = tracker.HandleKey(keyboard, AndroidGamepadCodes.KeyDPadLeft, isDown: true, isRepeat: false, fromGamepadSource: false);

        //Assert
        consumed.Should().BeFalse();
        tracker.ConnectedGamepads.Should().BeEmpty();
    }

    [Fact]
    public void A_gamepad_sourced_key_from_an_unknown_device_adds_an_injected_gamepad()
    {
        //Arrange
        var tracker = new GamepadTracker();
        const int injected = -1;

        //Act
        var consumed = tracker.HandleKey(injected, AndroidGamepadCodes.KeyButtonStart, isDown: true, isRepeat: false, fromGamepadSource: true);

        //Assert
        consumed.Should().BeTrue();
        tracker.ConnectedGamepads.Count.Should().Be(1);
        var gamepad = tracker.ConnectedGamepads[0];
        gamepad.Info.Origin.Should().Be(GamepadOrigin.InjectedInput);
        gamepad.Info.AxisProfile.IsEmpty.Should().BeTrue();
        gamepad.PressedButtons.Should().Equal(GamepadButtons.Start);
    }

    [Fact]
    public void The_trigger_buttons_read_as_a_fully_pulled_trigger()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyButtonL2, isDown: true, isRepeat: false, fromGamepadSource: true).Should().BeTrue();
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyButtonR2, isDown: true, isRepeat: false, fromGamepadSource: true).Should().BeTrue();

        //Assert
        gamepad.LeftTrigger.Should().Be(1f);
        gamepad.RightTrigger.Should().Be(1f);
        gamepad.PressedButtons.Should().BeEmpty();

        //Act
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyButtonL2, isDown: false, isRepeat: false, fromGamepadSource: true);

        //Assert
        gamepad.LeftTrigger.Should().Be(0f);
        gamepad.RightTrigger.Should().Be(1f);
    }

    [Fact]
    public void The_left_stick_s_y_axis_is_flipped_to_point_up()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        var consumed = tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisX, 0.5f), (AndroidGamepadCodes.AxisY, -1f)));

        //Assert
        consumed.Should().BeTrue();
        gamepad.LeftStick.X.Should().Be(0.5f);
        gamepad.LeftStick.Y.Should().Be(1f);
        gamepad.LeftStick.IsEngaged().Should().BeTrue();
    }

    [Fact]
    public void A_centred_stick_never_reads_as_negative_zero()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisX, 0f), (AndroidGamepadCodes.AxisY, 0f)));

        //Assert
        gamepad.LeftStick.ToString().Should().Be("(0.00, 0.00)");
        float.IsNegative(gamepad.LeftStick.Y).Should().BeFalse();
        new GamepadStick(float.NaN, -0f).ToString().Should().Be("(0.00, 0.00)");
    }

    [Fact]
    public void A_modern_controller_s_right_stick_and_triggers_come_from_z_rz_and_the_trigger_axes()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        tracker.HandleMotion(Device, Axes(
            (AndroidGamepadCodes.AxisX, 0f), (AndroidGamepadCodes.AxisY, 0f),
            (AndroidGamepadCodes.AxisZ, -0.75f), (AndroidGamepadCodes.AxisRZ, 0.25f),
            (AndroidGamepadCodes.AxisRX, 0.9f), (AndroidGamepadCodes.AxisRY, 0.9f),
            (AndroidGamepadCodes.AxisLeftTrigger, 0.3f), (AndroidGamepadCodes.AxisRightTrigger, 0.8f),
            (AndroidGamepadCodes.AxisBrake, 1f), (AndroidGamepadCodes.AxisGas, 1f),
            (AndroidGamepadCodes.AxisHatX, 0f), (AndroidGamepadCodes.AxisHatY, 0f)));

        //Assert
        gamepad.RightStick.X.Should().Be(-0.75f);
        gamepad.RightStick.Y.Should().Be(-0.25f);
        gamepad.LeftTrigger.Should().Be(0.3f);
        gamepad.RightTrigger.Should().Be(0.8f);
    }

    [Fact]
    public void A_legacy_controller_s_right_stick_and_triggers_come_from_rx_ry_and_brake_gas()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, LegacyController());

        //Act
        tracker.HandleMotion(Device, Axes(
            (AndroidGamepadCodes.AxisX, 0f), (AndroidGamepadCodes.AxisY, 0f),
            (AndroidGamepadCodes.AxisRX, 0.5f), (AndroidGamepadCodes.AxisRY, -0.5f),
            (AndroidGamepadCodes.AxisBrake, 0.4f), (AndroidGamepadCodes.AxisGas, 0.6f)));

        //Assert
        gamepad.RightStick.X.Should().Be(0.5f);
        gamepad.RightStick.Y.Should().Be(0.5f);
        gamepad.LeftTrigger.Should().Be(0.4f);
        gamepad.RightTrigger.Should().Be(0.6f);
    }

    [Fact]
    public void An_axis_the_profile_lacks_is_ignored()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, LegacyController());

        //Act
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisZ, 1f), (AndroidGamepadCodes.AxisRZ, 1f), (AndroidGamepadCodes.AxisHatX, 1f), (AndroidGamepadCodes.AxisHatY, 0f)));

        //Assert
        gamepad.RightStick.Should().Be(GamepadStick.Centered);
        gamepad.PressedButtons.Should().BeEmpty();
    }

    [Fact]
    public void An_event_that_omits_an_axis_of_the_profile_leaves_that_stick_alone()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisX, 1f), (AndroidGamepadCodes.AxisY, 0f)));

        //Act
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisZ, 1f)));

        //Assert
        gamepad.LeftStick.X.Should().Be(1f);
        gamepad.RightStick.Should().Be(GamepadStick.Centered);
    }

    [Fact]
    public void The_device_s_dead_zone_centres_small_readings()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());
        var changes = 0;
        tracker.GamepadStateChanged += (_, _) => changes++;

        //Act
        var consumed = tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisX, 0.05f), (AndroidGamepadCodes.AxisY, -0.1f)));

        //Assert
        consumed.Should().BeTrue();
        gamepad.LeftStick.Should().Be(GamepadStick.Centered);
        changes.Should().Be(0);

        //Act
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisX, 0.11f), (AndroidGamepadCodes.AxisY, 0f)));

        //Assert
        gamepad.LeftStick.X.Should().Be(0.11f);
        changes.Should().Be(1);
    }

    [Fact]
    public void The_hat_axes_press_the_d_pad_and_combine_with_its_keys()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisHatX, -1f), (AndroidGamepadCodes.AxisHatY, -1f)));

        //Assert
        gamepad.IsPressed(GamepadButtons.DPadLeft).Should().BeTrue();
        gamepad.IsPressed(GamepadButtons.DPadUp).Should().BeTrue();
        gamepad.PressedButtons.Count.Should().Be(2);

        //Act: the same direction also arrives as a key, then the hat centres while the key is still down.
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyDPadUp, isDown: true, isRepeat: false, fromGamepadSource: true);
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisHatX, 0f), (AndroidGamepadCodes.AxisHatY, 0f)));

        //Assert
        gamepad.PressedButtons.Should().Equal(GamepadButtons.DPadUp);

        //Act
        tracker.HandleKey(Device, AndroidGamepadCodes.KeyDPadUp, isDown: false, isRepeat: false, fromGamepadSource: true);
        tracker.HandleMotion(Device, Axes((AndroidGamepadCodes.AxisHatX, 1f), (AndroidGamepadCodes.AxisHatY, 1f)));

        //Assert
        gamepad.PressedButtons.Should().BeEquivalentTo(new[] { GamepadButtons.DPadRight, GamepadButtons.DPadDown });
    }

    [Fact]
    public void A_profile_without_axes_reads_whatever_the_event_carries_in_preference_order()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, new GamepadDeviceInfo("Bare", GamepadOrigin.InjectedInput));

        //Act
        tracker.HandleMotion(Device, Axes(
            (AndroidGamepadCodes.AxisX, 0.2f), (AndroidGamepadCodes.AxisY, 0.2f),
            (AndroidGamepadCodes.AxisRX, 0.7f), (AndroidGamepadCodes.AxisRY, 0.7f),
            (AndroidGamepadCodes.AxisBrake, 0.5f), (AndroidGamepadCodes.AxisGas, 0.9f)));

        //Assert
        gamepad.LeftStick.X.Should().Be(0.2f);
        gamepad.LeftStick.Y.Should().Be(-0.2f);
        gamepad.RightStick.X.Should().Be(0.7f);
        gamepad.RightStick.Y.Should().Be(-0.7f);
        gamepad.LeftTrigger.Should().Be(0.5f);
        gamepad.RightTrigger.Should().Be(0.9f);
    }

    [Fact]
    public void Motion_from_an_unknown_device_adds_an_injected_gamepad()
    {
        //Arrange
        var tracker = new GamepadTracker();

        //Act
        var consumed = tracker.HandleMotion(42, Axes((AndroidGamepadCodes.AxisX, 1f), (AndroidGamepadCodes.AxisY, 0f)));

        //Assert
        consumed.Should().BeTrue();
        tracker.ConnectedGamepads.Count.Should().Be(1);
        tracker.ConnectedGamepads[0].Info.Origin.Should().Be(GamepadOrigin.InjectedInput);
        tracker.ConnectedGamepads[0].LeftStick.X.Should().Be(1f);
    }

    [Fact]
    public void Out_of_range_and_non_finite_values_are_clamped()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var gamepad = tracker.AddDevice(Device, ModernController());

        //Act
        tracker.HandleMotion(Device, Axes(
            (AndroidGamepadCodes.AxisX, 5f), (AndroidGamepadCodes.AxisY, float.NaN),
            (AndroidGamepadCodes.AxisLeftTrigger, -1f), (AndroidGamepadCodes.AxisRightTrigger, 3f)));

        //Assert
        gamepad.LeftStick.X.Should().Be(1f);
        gamepad.LeftStick.Y.Should().Be(0f);
        gamepad.LeftTrigger.Should().Be(0f);
        gamepad.RightTrigger.Should().Be(1f);
    }

    [Fact]
    public void Remove_all_of_an_origin_leaves_the_others()
    {
        //Arrange
        var tracker = new GamepadTracker();
        var device = tracker.AddDevice(Device, ModernController());
        tracker.HandleKey(-1, AndroidGamepadCodes.KeyButtonA, isDown: true, isRepeat: false, fromGamepadSource: true);
        var virtualPad = tracker.AddVirtual("Scripted");

        //Act
        tracker.RemoveAll(GamepadOrigin.InjectedInput);

        //Assert
        tracker.ConnectedGamepads.Should().Equal(device, virtualPad.Gamepad);

        //Act
        tracker.RemoveAll(GamepadOrigin.Device);

        //Assert
        tracker.ConnectedGamepads.Should().Equal(virtualPad.Gamepad);
        device.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Adding_a_device_requires_its_description()
    {
        //Arrange
        var tracker = new GamepadTracker();

        //Act / Assert
        Assert.Throws<ArgumentNullException>(() => tracker.AddDevice(Device, null));
    }
}
