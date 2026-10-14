using System;
using System.Linq;
using Android.App;
using Android.OS;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Input;
using CodeBrix.Platform.Extensions;
using Microsoft.Extensions.Logging;

namespace HelloPaste;

/// <summary>
/// The launcher activity. Reads the <c>page</c> intent extra (see <see cref="StartPages"/>)
/// before the XAML application starts, and schedules the on-device self-check once. With the
/// <c>gamepad</c> extra (<c>--ez gamepad true</c>) it also starts the gamepads and logs every
/// connection and state change as a <c>GAMEPAD ...</c> logcat line, for checking a controller.
/// </summary>
[Activity(
    Name = "com.codebrix.hellopaste.MainActivity",
    Label = "HelloPaste",
    MainLauncher = true,
    Theme = "@style/Theme.Material3.DayNight.NoActionBar",
    ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
public class MainActivity : CodeBrixActivity
{
    private bool _selfCheckScheduled;
    private bool _gamepadLogging;

    /// <inheritdoc />
    protected override void OnCreate(Bundle savedInstanceState)
    {
        var page = Intent?.GetStringExtra("page");
        if (!string.IsNullOrEmpty(page))
        {
            StartPages.Requested = page;
        }

        base.OnCreate(savedInstanceState);
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        if (!_selfCheckScheduled)
        {
            _selfCheckScheduled = true;
            SelfCheck.Runner.Schedule(this);
        }

        if (!_gamepadLogging && Intent?.GetBooleanExtra("gamepad", false) == true)
        {
            _gamepadLogging = true;
            StartGamepadLogging();
        }
    }

    /// <summary>
    /// Starts the gamepads and logs them: one <c>GAMEPAD CONNECTED</c> line per controller (its name, ids
    /// and axis layout - what tells a controller's firmware generation apart), <c>GAMEPAD DISCONNECTED</c>,
    /// and a <c>GAMEPAD STATE</c> line for every change (the buttons down, both sticks, both triggers).
    /// </summary>
    private static void StartGamepadLogging()
    {
        var log = LogExtensionPoint.AmbientLoggerFactory.CreateLogger("HelloPaste.Gamepad");
        Gamepads.GamepadConnected += (_, e) => log.LogInformation("GAMEPAD CONNECTED {Id}: {Description}", e.Gamepad.Id, e.Gamepad.Info.Describe());
        Gamepads.GamepadDisconnected += (_, e) => log.LogInformation("GAMEPAD DISCONNECTED {Id}: {Name}", e.Gamepad.Id, e.Gamepad.Name);
        Gamepads.GamepadStateChanged += (_, e) => log.LogInformation(
            "GAMEPAD STATE {Id}: buttons=[{Buttons}] left={Left} right={Right} lt={LeftTrigger:0.00} rt={RightTrigger:0.00}",
            e.Gamepad.Id,
            string.Join(",", e.Gamepad.PressedButtons.OrderBy(b => b, StringComparer.Ordinal)),
            e.Gamepad.LeftStick,
            e.Gamepad.RightStick,
            e.Gamepad.LeftTrigger,
            e.Gamepad.RightTrigger);
        Gamepads.Start();
        log.LogInformation("GAMEPAD STARTED: {Count} connected", Gamepads.ConnectedGamepads.Count);
    }
}
