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
}
