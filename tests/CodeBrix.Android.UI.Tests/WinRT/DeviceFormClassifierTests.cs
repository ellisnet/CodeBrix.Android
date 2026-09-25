using System;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Windows.Graphics.Imaging;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class DeviceFormClassifierTests
{
    // Configuration.UiMode = type | night bits; UI_MODE_TYPE_NORMAL = 1, UI_MODE_NIGHT_NO = 0x10.
    private const int NormalDay = 0x11;

    [Theory]
    [InlineData(NormalDay, false, 411, "Mobile")]
    [InlineData(NormalDay, false, 800, "Tablet")]
    [InlineData(NormalDay, true, 411, "Desktop")]
    [InlineData(0x12, false, 800, "Desktop")]
    [InlineData(0x14, false, 960, "Television")]
    [InlineData(0x13, false, 411, "Car")]
    [InlineData(0x16, false, 200, "Watch")]
    [InlineData(0x17, false, 411, "VirtualReality")]
    public void Classify_uses_the_ui_mode_type_then_the_pc_feature_then_the_smallest_width(int uiMode, bool pc, int smallestWidthDp, string expected)
    {
        //Act
        var form = DeviceFormClassifier.Classify(uiMode, pc, smallestWidthDp);

        //Assert
        form.ToString().Should().Be(expected);
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
