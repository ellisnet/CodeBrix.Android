using CodeBrix.Android.UI.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Input;

/// <summary>
/// The process-wide gamepads, host-free: starting and stopping are idempotent and (with no Android
/// behind them) only flip the flag, and a virtual gamepad is listed whether or not they are started.
/// </summary>
/// <remarks>The only test class that touches the static <see cref="Gamepads"/>; its tests run one after another.</remarks>
[Collection("Gamepads static state")]
public class GamepadsTests
{
    [Fact]
    public void Start_and_stop_are_idempotent_and_keep_virtual_gamepads()
    {
        //Arrange
        Gamepads.IsStarted.Should().BeFalse();
        var connected = 0;
        Gamepads.GamepadConnected += OnConnected;
        void OnConnected(object sender, GamepadEventArgs e) => connected++;

        try
        {
            //Act
            var pad = Gamepads.AddVirtual("Scripted");
            Gamepads.Start();
            Gamepads.Start();

            //Assert
            Gamepads.IsStarted.Should().BeTrue();
            Gamepads.ConnectedGamepads.Should().Equal(pad.Gamepad);
            connected.Should().Be(1);

            //Act
            Gamepads.Stop();
            Gamepads.Stop();

            //Assert
            Gamepads.IsStarted.Should().BeFalse();
            Gamepads.ConnectedGamepads.Should().Equal(pad.Gamepad);

            //Act
            pad.Disconnect();

            //Assert
            Gamepads.ConnectedGamepads.Should().BeEmpty();
        }
        finally
        {
            Gamepads.GamepadConnected -= OnConnected;
            Gamepads.Stop();
        }
    }

    [Fact]
    public void State_changes_of_a_virtual_gamepad_reach_the_static_event()
    {
        //Arrange
        var changes = 0;
        Gamepads.GamepadStateChanged += OnChanged;
        void OnChanged(object sender, GamepadEventArgs e) => changes++;
        var pad = Gamepads.AddVirtual();

        try
        {
            //Act
            pad.Press(GamepadButtons.Y);

            //Assert
            changes.Should().Be(1);
            Gamepads.ConnectedGamepads[0].IsPressed(GamepadButtons.Y).Should().BeTrue();
        }
        finally
        {
            Gamepads.GamepadStateChanged -= OnChanged;
            pad.Disconnect();
        }
    }
}
