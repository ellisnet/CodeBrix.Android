using CodeBrix.Android.UI.Portable.TextInput;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.TextInput;

/// <summary>
/// [AP8-S item L] Where the soft-keyboard session's focus view is laid out for a custom text control's caret (host-free):
/// on the caret, in the focus layer's pixels, clipped to the part of the control that shows it - or parked when the caret
/// is not in view (Android pans only to a rectangle inside the focused view's own visible bounds).
/// </summary>
public class CaretPlacementTests
{
    [Fact]
    public void The_view_is_laid_out_on_the_caret_in_pixels()
    {
        //Act
        var placed = CaretPlacement.TryPlace((100, 200, 1, 18), (0, 0, 800, 600), 2.0, 0, 0, out var rect);

        //Assert
        placed.Should().BeTrue();
        rect.Should().Be(new CaretPixels(200, 400, 2, 36));
    }

    [Fact]
    public void Fractional_edges_round_outwards_so_the_whole_caret_is_covered()
    {
        //Act
        var placed = CaretPlacement.TryPlace((10.3, 20.6, 1, 17.2), (0, 0, 800, 600), 1.5, 0, 0, out var rect);

        //Assert (15.45 -> 15, 30.9 -> 30; 16.95 -> 17, 56.7 -> 57)
        placed.Should().BeTrue();
        rect.Should().Be(new CaretPixels(15, 30, 2, 27));
    }

    [Fact]
    public void The_content_origin_in_the_focus_layer_is_added()
    {
        //Act
        CaretPlacement.TryPlace((10, 10, 1, 10), (0, 0, 100, 100), 1.0, 5, 24, out var rect);

        //Assert
        rect.Left.Should().Be(15);
        rect.Top.Should().Be(34);
    }

    [Fact]
    public void A_zero_wide_caret_still_gets_one_pixel()
    {
        //Act
        var placed = CaretPlacement.TryPlace((50, 50, 0, 20), (0, 0, 100, 100), 1.0, 0, 0, out var rect);

        //Assert
        placed.Should().BeTrue();
        rect.Width.Should().Be(1);
        rect.Height.Should().Be(20);
    }

    [Fact]
    public void A_caret_on_the_right_edge_of_the_control_is_placed()
    {
        //Act
        var placed = CaretPlacement.TryPlace((100, 10, 1, 20), (0, 0, 100, 100), 1.0, 0, 0, out var rect);

        //Assert
        placed.Should().BeTrue();
        rect.Should().Be(new CaretPixels(100, 10, 1, 20));
    }

    [Fact]
    public void A_caret_partly_scrolled_out_is_clipped_to_the_visible_part()
    {
        //Act (the control shows y 100..300; the caret line is 290..310)
        var placed = CaretPlacement.TryPlace((40, 290, 1, 20), (0, 100, 400, 200), 1.0, 0, 0, out var rect);

        //Assert
        placed.Should().BeTrue();
        rect.Should().Be(new CaretPixels(40, 290, 1, 10));
    }

    [Theory]
    [InlineData(40, 320)]
    [InlineData(40, 60)]
    [InlineData(500, 150)]
    public void A_caret_outside_the_visible_part_is_not_placed(double x, double y)
    {
        //Act
        var placed = CaretPlacement.TryPlace((x, y, 1, 20), (0, 100, 400, 200), 1.0, 0, 0, out _);

        //Assert
        placed.Should().BeFalse();
    }

    [Fact]
    public void An_empty_or_invalid_caret_or_control_is_not_placed()
    {
        //Assert
        CaretPlacement.TryPlace((10, 10, 1, 0), (0, 0, 100, 100), 1.0, 0, 0, out _).Should().BeFalse();
        CaretPlacement.TryPlace((10, 10, 1, 10), (0, 0, 0, 100), 1.0, 0, 0, out _).Should().BeFalse();
        CaretPlacement.TryPlace((10, 10, 1, 10), (0, 0, 100, 100), 0, 0, 0, out _).Should().BeFalse();
        CaretPlacement.TryPlace((double.NaN, 10, 1, 10), (0, 0, 100, 100), 1.0, 0, 0, out _).Should().BeFalse();
        CaretPlacement.TryPlace((10, 10, 1, double.PositiveInfinity), (0, 0, 100, 100), 1.0, 0, 0, out _).Should().BeFalse();
    }
}
