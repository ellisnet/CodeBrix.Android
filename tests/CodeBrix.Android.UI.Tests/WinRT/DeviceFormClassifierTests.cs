using System;
using CodeBrix.Android.Portable;
using CodeBrix.Android.UI.Overlay;
using SilverAssertions;
using Windows.Graphics.Imaging;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class DeviceFormClassifierTests
{
    // Configuration.UiMode = type | night bits; UI_MODE_TYPE_NORMAL = 1, UI_MODE_NIGHT_NO = 0x10.
    private const int NormalDay = 0x11;

    [Theory]
    [InlineData(NormalDay, 411, "Mobile")]
    [InlineData(NormalDay, 599.9, "Mobile")]
    [InlineData(NormalDay, 600, "Tablet")]
    [InlineData(NormalDay, 839.9, "Tablet")]
    [InlineData(NormalDay, 840, "Desktop")]
    [InlineData(NormalDay, 1920, "Desktop")]
    [InlineData(0x12, 411, "Mobile")]
    [InlineData(0x12, 1280, "Desktop")]
    [InlineData(0x14, 960, "Television")]
    [InlineData(0x13, 411, "Car")]
    [InlineData(0x16, 200, "Watch")]
    [InlineData(0x17, 411, "VirtualReality")]
    public void Classify_uses_the_special_ui_mode_types_then_the_window_width_size_class(int uiMode, double windowWidthDp, string expected)
    {
        //Act
        var form = DeviceFormClassifier.Classify(uiMode, windowWidthDp);

        //Assert
        form.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void An_unknown_window_width_counts_as_expanded_and_gives_desktop(double windowWidthDp)
    {
        //Act
        var form = DeviceFormClassifier.Classify(NormalDay, windowWidthDp);

        //Assert
        form.Should().Be(DeviceFormKind.Desktop);
    }

    [Theory]
    [InlineData(400, "Android.Mobile")]
    [InlineData(700, "Android.Tablet")]
    [InlineData(1080, "Android.Desktop")]
    public void The_device_family_of_each_width_size_class_is_Android_dot_the_form(double windowWidthDp, string expected)
    {
        //Act
        var family = DeviceFormClassifier.DeviceFamilyOf(DeviceFormClassifier.FromWindowWidth(windowWidthDp));

        //Assert
        family.Should().Be(expected);
    }

    [Fact]
    public void The_form_follows_the_size_class_services_width_class_at_every_width()
    {
        //Act + Assert (WindowSizeClasses is the size-class service's own classification, CodeBrix.Android.UI)
        for (var widthDp = 0.0; widthDp <= 2000; widthDp += 0.5)
        {
            var expected = WindowSizeClasses.FromWidth(widthDp) switch
            {
                WindowWidthClass.Compact => DeviceFormKind.Mobile,
                WindowWidthClass.Medium => DeviceFormKind.Tablet,
                _ => DeviceFormKind.Desktop,
            };
            DeviceFormClassifier.FromWindowWidth(widthDp).Should().Be(expected, "the width {0} dp", widthDp);
        }

        DeviceFormClassifier.OperatingSystemFamily.Should().Be("Android");
    }

    [Fact]
    public void DeviceFormKind_values_match_the_core_enum()
    {
        //Arrange (the Core enum is internal to CodeBrix.Platform.Core, which grants this test assembly no access)
        var coreEnum = typeof(BitmapEncoder).Assembly.GetType("Windows.System.Profile.Internal.CodeBrixDeviceForm", throwOnError: true);

        //Act + Assert
        foreach (DeviceFormKind kind in Enum.GetValues<DeviceFormKind>())
        {
            var coreValue = Convert.ToInt32(Enum.Parse(coreEnum, kind.ToString()));
            coreValue.Should().Be((int)kind);
        }

        Enum.GetNames(coreEnum).Length.Should().Be(Enum.GetNames<DeviceFormKind>().Length);
    }
}
