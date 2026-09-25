using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using SilverAssertions;
using Windows.Foundation;
using Xunit;
using Color = Windows.UI.Color;

namespace CodeBrix.Android.UI.Tests.Drawing;

[Collection(HostFreeCoreCollection.Name)]
public class BrushPaintTests
{
    public BrushPaintTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void A_solid_brush_is_its_colour_times_its_opacity()
    {
        //Act
        var paint = BrushPaint.From(new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0x20, 0x30)) { Opacity = 0.5 }, 10, 10, 1);

        //Assert
        paint.Kind.Should().Be(BrushPaintKind.Solid);
        paint.Color.Should().Be(unchecked((int)0x80102030));
    }

    [Fact]
    public void A_transparent_or_missing_brush_paints_nothing()
    {
        //Act
        var none = BrushPaint.From(null, 10, 10, 1);
        var transparent = BrushPaint.From(new SolidColorBrush(Colors.Transparent), 10, 10, 1);

        //Assert
        none.Kind.Should().Be(BrushPaintKind.None);
        transparent.Kind.Should().Be(BrushPaintKind.None);
    }

    [Fact]
    public void An_acrylic_brush_paints_its_fallback_colour()
    {
        //Arrange
        var brush = new AcrylicBrush { FallbackColor = Color.FromArgb(0xFF, 0x2C, 0x2C, 0x2C), TintColor = Colors.Red, Opacity = 0.5 };

        //Act
        var paint = BrushPaint.From(brush, 10, 10, 1);

        //Assert
        paint.Kind.Should().Be(BrushPaintKind.Solid);
        paint.Color.Should().Be(unchecked((int)0x802C2C2C));
    }

    [Fact]
    public void An_acrylic_brush_with_a_transparent_fallback_paints_nothing()
    {
        //Act
        var paint = BrushPaint.From(new AcrylicBrush { FallbackColor = Colors.Transparent }, 10, 10, 1);

        //Assert
        paint.Kind.Should().Be(BrushPaintKind.None);
    }

    [Fact]
    public void A_relative_linear_gradient_maps_to_the_box_in_pixels()
    {
        //Arrange
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = { new GradientStop { Color = Colors.Blue, Offset = 1 }, new GradientStop { Color = Colors.Red, Offset = 0 } },
        };

        //Act
        var paint = BrushPaint.From(brush, 400, 100, 2);

        //Assert (stops sorted by offset)
        paint.Kind.Should().Be(BrushPaintKind.Linear);
        paint.Start.Should().Be(new Point(0, 50));
        paint.End.Should().Be(new Point(400, 50));
        paint.Colors.Should().Equal(unchecked((int)0xFFFF0000), unchecked((int)0xFF0000FF));
        paint.Offsets.Should().Equal(0f, 1f);
    }

    [Fact]
    public void An_absolute_radial_gradient_scales_with_the_density()
    {
        //Arrange
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(50, 50),
            RadiusX = 20,
            RadiusY = 10,
            GradientStops = { new GradientStop { Color = Colors.Red, Offset = 0 }, new GradientStop { Color = Colors.Blue, Offset = 1 } },
        };

        //Act
        var paint = BrushPaint.From(brush, 400, 400, 2);

        //Assert
        paint.Kind.Should().Be(BrushPaintKind.Radial);
        paint.Center.Should().Be(new Point(100, 100));
        paint.RadiusX.Should().Be(40);
        paint.RadiusY.Should().Be(20);
    }

    [Fact]
    public void A_single_stop_gradient_becomes_two_stops_of_that_colour()
    {
        //Arrange
        var brush = new LinearGradientBrush { GradientStops = { new GradientStop { Color = Colors.Lime, Offset = 0.3 } } };

        //Act
        var paint = BrushPaint.From(brush, 10, 10, 1);

        //Assert
        paint.Colors.Should().HaveCount(2);
        paint.Offsets.Should().Equal(0f, 1f);
    }

    [Fact]
    public void SingleColor_of_a_gradient_is_its_first_stop_and_of_null_the_fallback()
    {
        //Arrange
        var brush = new LinearGradientBrush { GradientStops = { new GradientStop { Color = Colors.Red, Offset = 0 }, new GradientStop { Color = Colors.Blue, Offset = 1 } } };

        //Act
        var gradient = BrushPaint.SingleColor(brush, 7);
        var none = BrushPaint.SingleColor(null, 7);

        //Assert
        gradient.Should().Be(unchecked((int)0xFFFF0000));
        none.Should().Be(7);
    }
}
