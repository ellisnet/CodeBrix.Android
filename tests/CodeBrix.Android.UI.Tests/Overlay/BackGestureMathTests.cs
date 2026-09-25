using CodeBrix.Android.UI.Overlay;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

public class BackGestureMathTests
{
    [Fact]
    public void No_progress_is_no_preview()
    {
        //Act
        var (scale, translation) = BackGestureMath.Preview(0f, fromLeftEdge: true, widthPx: 1080, density: 1);

        //Assert
        scale.Should().Be(1f);
        translation.Should().Be(0f);
    }

    [Fact]
    public void Full_progress_from_the_left_edge_shrinks_to_ninety_percent_and_shifts_right()
    {
        //Act (1080 px at density 2: 1080 / 20 - 8 * 2 = 38 px)
        var (scale, translation) = BackGestureMath.Preview(1f, fromLeftEdge: true, widthPx: 1080, density: 2);

        //Assert
        scale.Should().BeApproximately(0.9f, 0.0001f);
        translation.Should().BeApproximately(38f, 0.0001f);
    }

    [Fact]
    public void A_swipe_from_the_right_edge_shifts_left()
    {
        //Act
        var (_, translation) = BackGestureMath.Preview(0.5f, fromLeftEdge: false, widthPx: 1000, density: 1);

        //Assert (half of 1000 / 20 - 8 = 42)
        translation.Should().BeApproximately(-21f, 0.0001f);
    }

    [Fact]
    public void Progress_outside_zero_to_one_is_clamped()
    {
        //Act
        var (scale, _) = BackGestureMath.Preview(3f, fromLeftEdge: true, widthPx: 1000, density: 1);

        //Assert
        scale.Should().BeApproximately(0.9f, 0.0001f);
    }
}
