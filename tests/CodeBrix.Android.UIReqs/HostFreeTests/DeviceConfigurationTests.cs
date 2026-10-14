using CodeBrix.Android.UIReqs.Hosting;
using Xunit;

namespace CodeBrix.Android.UIReqs.HostFreeTests;

/// <summary>Fences the host boundary so a scenario cannot change an existing device's configuration.</summary>
public sealed class DeviceConfigurationTests
{
    /// <summary>Only read-only display queries cross the shell boundary in preservation mode.</summary>
    [Theory]
    [InlineData("wm size", true)]
    [InlineData("wm density", true)]
    [InlineData("wm size reset", false)]
    [InlineData("wm size 1080x1920", false)]
    [InlineData("wm density 160", false)]
    [InlineData("wm density reset", false)]
    [InlineData("ime reset", false)]
    [InlineData("input tap 1 1", false)]
    [InlineData("settings put global animator_duration_scale 0", false)]
    [InlineData("wm size; reboot", false)]
    [InlineData(null, false)]
    public void ConfigurationPreservingShellAllowsOnlyQueries(string? command, bool expected) =>
        Assert.Equal(expected, DeviceSession.IsAllowedShell(command, preserveConfiguration: true));

    /// <summary>A scenario may inject a gamepad button or D-pad key through the system, and nothing else of the input tool.</summary>
    [Theory]
    [InlineData("input gamepad keyevent KEYCODE_BUTTON_A", true)]
    [InlineData("input gamepad keyevent KEYCODE_BUTTON_START", true)]
    [InlineData("input gamepad keyevent KEYCODE_BUTTON_THUMBR", true)]
    [InlineData("input gamepad keyevent KEYCODE_DPAD_LEFT", true)]
    [InlineData("input gamepad keyevent KEYCODE_DPAD_CENTER", true)]
    [InlineData("input tap 1 1", true)]
    [InlineData("input gamepad keyevent KEYCODE_BACK", false)]
    [InlineData("input gamepad keyevent 96", false)]
    [InlineData("input keyevent KEYCODE_BUTTON_A", false)]
    [InlineData("input gamepad keyevent KEYCODE_BUTTON_A; reboot", false)]
    [InlineData("input gamepad text hello", false)]
    public void ShellAllowsGamepadKeyInjectionOnly(string? command, bool expected) =>
        Assert.Equal(expected, DeviceSession.IsAllowedShell(command));
}
