using System;
using System.IO;
using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

/// <summary>
/// ThemeBridgeRefresh: a configuration change re-applies the theme bridge's Material roles only for a day/night flip of a
/// palette the bridge already wrote - a rotation (same night bits) never does, and a Fluent-coloured app is never
/// switched to the Material roles by a configuration change (the PainDiagram page that turned the Material surface
/// colour after its first rotation).
/// </summary>
public sealed class ThemeBridgeRefreshTests
{
    private const int NightNo = 0x10;
    private const int NightYes = 0x20;

    [Fact]
    public void a_rotation_never_reapplies_the_roles()
    {
        //Act
        var reapply = ThemeBridgeRefresh.ShouldReapply(NightNo, NightNo, rolesApplied: true);

        //Assert
        reapply.Should().BeFalse();
    }

    [Fact]
    public void a_day_night_flip_reapplies_roles_the_bridge_wrote()
    {
        //Act
        var reapply = ThemeBridgeRefresh.ShouldReapply(NightNo, NightYes, rolesApplied: true);

        //Assert
        reapply.Should().BeTrue();
    }

    [Theory]
    [InlineData(NightNo, NightNo)]
    [InlineData(NightNo, NightYes)]
    [InlineData(NightYes, NightNo)]
    public void a_fluent_coloured_app_is_never_switched_to_the_material_roles(int last, int night)
    {
        //Act
        var reapply = ThemeBridgeRefresh.ShouldReapply(last, night, rolesApplied: false);

        //Assert
        reapply.Should().BeFalse();
    }

    [Fact]
    public void the_first_change_without_a_known_night_state_does_not_reapply()
    {
        //Act
        var reapply = ThemeBridgeRefresh.ShouldReapply(null, NightYes, rolesApplied: true);

        //Assert
        reapply.Should().BeFalse();
    }

    [Fact]
    public void the_bridge_callback_starts_from_the_installed_configuration_and_asks_the_decision()
    {
        //Arrange - ThemeBridge is Android-only (#if __ANDROID__), so its source is the fence
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Policy", "ThemeBridge.cs"));

        //Assert
        source.Should().Contain("new ConfigurationCallbacks(context.Resources?.Configuration)");
        source.Should().Contain("ThemeBridgeRefresh.ShouldReapply(_lastNight, night, _ownBrushes.Count > 0)");
        source.Should().NotContain("(AUiMode)(-1)");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        return directory.FullName;
    }
}
