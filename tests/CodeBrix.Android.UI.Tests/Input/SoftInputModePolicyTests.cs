using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable.Input;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Input;

/// <summary>
/// [AP8-S item K] The MAUI-style soft-input adjust policy of a CodeBrix activity, host-free: which adjust bits the
/// window gets (app setting, else the declared mode, else Pan), and when the keyboard's height is withheld from the page.
/// </summary>
public class SoftInputModePolicyTests
{
    private const int StateHidden = 0x02;
    private const int StateAlwaysVisible = 0x05;

    [Fact]
    public void The_bits_are_android_s_soft_input_adjust_values()
    {
        //Assert
        SoftInputModePolicy.AdjustUnspecified.Should().Be(0x00);
        SoftInputModePolicy.AdjustResize.Should().Be(0x10);
        SoftInputModePolicy.AdjustPan.Should().Be(0x20);
        SoftInputModePolicy.AdjustNothing.Should().Be(0x30);
        SoftInputModePolicy.MaskAdjust.Should().Be(0xF0);
    }

    [Fact]
    public void The_app_setting_maps_as_maui_does()
    {
        //Assert
        SoftInputModePolicy.ToAdjustBits(SoftInputAdjust.Pan).Should().Be(0x20);
        SoftInputModePolicy.ToAdjustBits(SoftInputAdjust.Resize).Should().Be(0x10);
        SoftInputModePolicy.ToAdjustBits(SoftInputAdjust.Unspecified).Should().Be(0x00);
        SoftInputModePolicy.ToAdjustBits((SoftInputAdjust)42).Should().Be(0x20);
    }

    [Fact]
    public void The_app_setting_defaults_to_pan()
    {
        //Assert
        default(SoftInputAdjust).Should().Be(SoftInputAdjust.Pan);
    }

    [Fact]
    public void An_activity_that_declares_nothing_pans_and_keeps_its_state_bits()
    {
        //Act
        var mode = SoftInputModePolicy.Resolve(StateHidden, 0x00, null);

        //Assert
        mode.Should().Be(StateHidden | 0x20);
    }

    [Theory]
    [InlineData(0x10)]
    [InlineData(0x20)]
    [InlineData(0x30)]
    public void A_declared_adjust_mode_is_kept_while_the_app_sets_none(int declared)
    {
        //Act
        var mode = SoftInputModePolicy.Resolve(StateHidden | declared, StateHidden | declared, null);

        //Assert
        mode.Should().Be(StateHidden | declared);
    }

    [Theory]
    [InlineData(SoftInputAdjust.Pan, 0x20)]
    [InlineData(SoftInputAdjust.Resize, 0x10)]
    [InlineData(SoftInputAdjust.Unspecified, 0x00)]
    public void An_app_setting_wins_over_a_declared_mode(SoftInputAdjust setting, int expected)
    {
        //Act
        var mode = SoftInputModePolicy.Resolve(StateAlwaysVisible | 0x30, StateAlwaysVisible | 0x30, setting);

        //Assert
        mode.Should().Be(StateAlwaysVisible | expected);
    }

    [Fact]
    public void Switching_at_runtime_replaces_only_the_adjust_bits()
    {
        //Arrange
        var resized = SoftInputModePolicy.Resolve(StateHidden, 0x00, SoftInputAdjust.Resize);

        //Act
        var panned = SoftInputModePolicy.Resolve(resized, 0x00, SoftInputAdjust.Pan);

        //Assert
        resized.Should().Be(StateHidden | 0x10);
        panned.Should().Be(StateHidden | 0x20);
    }

    [Theory]
    [InlineData(0x10, true)]
    [InlineData(0x20, false)]
    [InlineData(0x00, false)]
    [InlineData(0x30, false)]
    [InlineData(0x12, true)]
    public void Only_resize_withholds_the_keyboard_from_the_page(int mode, bool expected)
    {
        //Assert
        SoftInputModePolicy.WithholdsKeyboard(mode).Should().Be(expected);
    }

    [Fact]
    public void The_withheld_height_is_the_keyboard_inset_in_dips()
    {
        //Act - a 1080x1920 window at density 1.0 (160 dpi) with a 356 px keyboard; a phone at 2.625
        var docked = SoftInputModePolicy.OcclusionInsetDips(356, 1.0, true);
        var phone = SoftInputModePolicy.OcclusionInsetDips(840, 2.625, true);

        //Assert
        docked.Should().Be(356);
        phone.Should().Be(320);
    }

    [Theory]
    [InlineData(356, 1.0, false)]
    [InlineData(0, 1.0, true)]
    [InlineData(-4, 1.0, true)]
    [InlineData(356, 0, true)]
    public void Nothing_is_withheld_when_the_mode_pans_or_the_keyboard_is_hidden(double keyboardPx, double density, bool withholds)
    {
        //Assert
        SoftInputModePolicy.OcclusionInsetDips(keyboardPx, density, withholds).Should().Be(0);
    }

    [Fact]
    public void The_core_root_seam_the_resize_mode_feeds_exists()
    {
        //Arrange - IRootElement.ContentBottomOcclusionInset (the Platform's own on-screen keyboard seam); a pin bump
        // that removes or renames it must fail here, not silently on a device.
        var type = typeof(Microsoft.UI.Xaml.UIElement).Assembly.GetType("CodeBrix.Platform.UI.Xaml.Core.IRootElement");

        //Act
        var property = type?.GetProperty("ContentBottomOcclusionInset");

        //Assert
        type.Should().NotBeNull();
        property.Should().NotBeNull();
        property.PropertyType.Should().Be(typeof(double));
        property.CanWrite.Should().BeTrue();
    }
}
