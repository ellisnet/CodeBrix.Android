using CodeBrix.Android.UI.Portable.Projection;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Projection;

public class ProjectionColorTests
{
    [Fact]
    public void ToArgb_packs_the_channels()
    {
        //Act
        var argb = ProjectionColor.ToArgb(0xFF, 0xB8, 0x55, 0x55, 1);

        //Assert
        argb.Should().Be(unchecked((int)0xFFB85555));
    }

    [Fact]
    public void ToArgb_multiplies_alpha_by_the_opacity()
    {
        //Act
        var half = ProjectionColor.ToArgb(0xFF, 0x10, 0x20, 0x30, 0.5);
        var clamped = ProjectionColor.ToArgb(0x80, 0, 0, 0, 3);
        var nan = ProjectionColor.ToArgb(0x40, 0, 0, 0, double.NaN);

        //Assert
        ((half >> 24) & 0xFF).Should().Be(128);
        ((clamped >> 24) & 0xFF).Should().Be(0x80);
        ((nan >> 24) & 0xFF).Should().Be(0x40);
    }

    [Fact]
    public void IsTransparent_is_true_only_for_alpha_zero()
    {
        //Act
        var transparent = ProjectionColor.IsTransparent(ProjectionColor.ToArgb(0xFF, 1, 2, 3, 0));
        var opaque = ProjectionColor.IsTransparent(ProjectionColor.ToArgb(0x01, 1, 2, 3, 1));

        //Assert
        transparent.Should().BeTrue();
        opaque.Should().BeFalse();
    }
}
