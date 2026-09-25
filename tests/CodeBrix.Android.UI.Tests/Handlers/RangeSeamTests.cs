using Microsoft.UI.Xaml.Controls;
using CodeBrix.Android.UI.Tests.HostFree;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// AP3a fences for the Core behaviour the Slider / ProgressBar handlers write through (RangeBase.Value is a
/// public DP write; Core coerces it into the range and raises ValueChanged).
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public class RangeSeamTests
{
    public RangeSeamTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void A_native_value_written_to_a_Slider_raises_ValueChanged()
    {
        //Arrange
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 20 };
        var changes = 0;
        slider.ValueChanged += (_, _) => changes++;

        //Act
        slider.Value = 46;

        //Assert
        slider.Value.Should().Be(46);
        changes.Should().Be(1);
    }

    [Fact]
    public void A_value_beyond_Maximum_is_coerced_into_the_range()
    {
        //Arrange
        var bar = new ProgressBar { Minimum = 0, Maximum = 10 };

        //Act
        bar.Value = 25;

        //Assert
        bar.Value.Should().Be(10);
    }
}
