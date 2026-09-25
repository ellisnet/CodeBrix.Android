using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

public class MaterialRoleMapTests
{
    [Fact]
    public void Entries_name_each_curated_key_once()
    {
        //Assert
        MaterialRoleMap.Entries.Select(e => e.Key).Should().OnlyHaveUniqueItems();
        MaterialRoleMap.Entries.Should().Contain(e => e.Key == "ApplicationPageBackgroundThemeBrush" && e.Role == "colorSurface");
        MaterialRoleMap.Entries.Should().Contain(e => e.Key == "SystemControlForegroundBaseLowBrush" && e.Role == "colorOutlineVariant");
    }

    [Fact]
    public void Entries_make_the_default_Button_tonal_and_the_accent_Button_filled()
    {
        //Assert
        MaterialRoleMap.Entries.Single(e => e.Key == "ButtonBackground").Role.Should().Be("colorSecondaryContainer");
        MaterialRoleMap.Entries.Single(e => e.Key == "AccentButtonBackground").Role.Should().Be("colorPrimary");
        MaterialRoleMap.Entries.Where(e => e.Key.StartsWith("Button") || e.Key.StartsWith("AccentButton")).Should().OnlyContain(e => e.Write == RoleWrite.OwnBrush);
    }

    [Fact]
    public void Roles_lists_every_attribute_the_entries_read()
    {
        //Act
        var roles = MaterialRoleMap.Roles();

        //Assert
        roles.Should().Contain(new[] { "colorPrimary", "colorOnPrimary", "colorSurface", "colorOnSurface", "colorOutline", "colorOutlineVariant", "colorSecondaryContainer", "colorOnSecondaryContainer" });
    }

    [Fact]
    public void ColorOf_applies_the_alpha_of_the_entry()
    {
        //Arrange
        var entry = new MaterialRoleEntry("TextFillColorDisabledBrush", "colorOnSurface", 0.38, null, 0, RoleWrite.InPlace);
        var roles = new Dictionary<string, int> { ["colorOnSurface"] = unchecked((int)0xFF1D1B20) };

        //Act
        var color = MaterialRoleMap.ColorOf(entry, roles);

        //Assert (0xFF * 0.38 = 96.9 -> 0x61)
        color.Should().Be(0x611D1B20);
    }

    [Fact]
    public void ColorOf_lays_the_state_layer_over_the_role()
    {
        //Arrange
        var entry = new MaterialRoleEntry("ButtonBackgroundPointerOver", "colorSecondaryContainer", 1, "colorOnSecondaryContainer", 0.5, RoleWrite.OwnBrush);
        var roles = new Dictionary<string, int>
        {
            ["colorSecondaryContainer"] = unchecked((int)0xFFFFFFFF),
            ["colorOnSecondaryContainer"] = unchecked((int)0xFF000000),
        };

        //Act
        var color = MaterialRoleMap.ColorOf(entry, roles);

        //Assert (halfway between white and black, rounded)
        color.Should().Be(unchecked((int)0xFF808080));
    }

    [Fact]
    public void ColorOf_is_null_when_a_role_is_missing() =>
        MaterialRoleMap.ColorOf(MaterialRoleMap.Entries[0], new Dictionary<string, int>()).Should().BeNull();

    [Theory]
    [InlineData(unchecked((int)0xFF102030), 1.0, unchecked((int)0xFF102030))]
    [InlineData(unchecked((int)0xFF102030), 0.5, unchecked((int)0x80102030))]
    [InlineData(unchecked((int)0xFF102030), 0.0, 0x00102030)]
    public void WithAlpha_multiplies_the_alpha(int argb, double alpha, int expected) =>
        MaterialRoleMap.WithAlpha(argb, alpha).Should().Be(expected);
}
