using CodeBrix.Android.UI.Platform.Recycler.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Lists;

public class UniformGridMathTests
{
    [Theory]
    [InlineData(400, 48, 0, 0, 8)]
    [InlineData(520, 48, 0, 0, 10)]
    [InlineData(1000, 230, 14, 0, 4)]
    [InlineData(100, 230, 14, 0, 1)]
    [InlineData(400, 48, 0, 3, 3)]
    public void span_count_fits_items_and_spacing(double available, double item, double spacing, int maximum, int expected) =>
        UniformGridMath.SpanCount(available, item, spacing, maximum).Should().Be(expected);

    [Fact]
    public void span_count_of_an_unknown_size_is_one_or_the_maximum()
    {
        UniformGridMath.SpanCount(double.PositiveInfinity, 48, 0, 0).Should().Be(1);
        UniformGridMath.SpanCount(400, double.NaN, 0, 5).Should().Be(5);
    }

    [Fact]
    public void extra_end_padding_leaves_exact_cells()
    {
        // 8 cells of 48 in 400: 16 left over.
        UniformGridMath.ExtraEndPadding(400, 48, 0, 8).Should().Be(16);

        // 4 cells of 230 + 14 in 1000 (+14 trailing): 1014 - 976 = 38.
        UniformGridMath.ExtraEndPadding(1000, 230, 14, 4).Should().Be(38);
    }

    [Fact]
    public void layout_spec_defaults_are_the_list_grid_and_pager()
    {
        ItemsLayoutSpec.VerticalList.Kind.Should().Be(ItemsLayoutKind.Linear);
        ItemsLayoutSpec.VerticalGrid.ScrollsHorizontally.Should().BeFalse();
        ItemsLayoutSpec.HorizontalPager.ScrollsHorizontally.Should().BeTrue();
    }

    [Fact]
    public void cross_extent_is_cores_uniform_grid_extent_without_trailing_spacing()
    {
        // [AP8-S batch 2] the Kenney catalog: 3 cards of 230 + 14 fit 732 DIPs; Core reports 3 x 244 - 14 = 718.
        var span = UniformGridMath.SpanCount(732, 230, 14, 0);
        span.Should().Be(3);
        UniformGridMath.CrossExtent(230, 14, span).Should().Be(718);

        // Arranged at that extent the span holds (it does not drop to 2, the defect).
        UniformGridMath.SpanCount(718, 230, 14, 0).Should().Be(3);
        UniformGridMath.CrossExtent(48, 0, 8).Should().Be(384);
        UniformGridMath.CrossExtent(double.NaN, 14, 3).Should().Be(0);
        UniformGridMath.CrossExtent(230, 14, 0).Should().Be(0);
    }

    [Fact]
    public void column_offset_moves_items_from_equal_cells_to_cores_columns()
    {
        // 718 DIPs in 3 equal cells = 239.33 each; Core's columns start at 0, 244, 488.
        var cell = 718.0 / 3;
        UniformGridMath.ColumnOffset(0, 230, 14, cell).Should().Be(0);
        (cell + UniformGridMath.ColumnOffset(1, 230, 14, cell)).Should().BeApproximately(244, 1e-9);
        (2 * cell + UniformGridMath.ColumnOffset(2, 230, 14, cell)).Should().BeApproximately(488, 1e-9);

        // Exact cells (item + spacing) need no offset; an item never leaves its cell.
        UniformGridMath.ColumnOffset(2, 230, 14, 244).Should().Be(0);
        UniformGridMath.ColumnOffset(5, 230, 14, 232).Should().Be(2);
    }
}
