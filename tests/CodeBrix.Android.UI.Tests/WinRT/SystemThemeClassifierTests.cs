using CodeBrix.Android.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class SystemThemeClassifierTests
{
    [Theory]
    [InlineData(0x21, true)]
    [InlineData(0x11, false)]
    [InlineData(0x01, false)]
    [InlineData(0x31, false)]
    public void IsDark_reads_the_night_bits_of_the_ui_mode(int uiMode, bool expected)
    {
        //Act
        var dark = SystemThemeClassifier.IsDark(uiMode);

        //Assert
        dark.Should().Be(expected);
    }
}
