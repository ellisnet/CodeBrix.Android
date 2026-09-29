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
}
