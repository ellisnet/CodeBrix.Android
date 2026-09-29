using System.Text.Json.Nodes;
using CodeBrix.Android.UIReqs.Protocol;
using CodeBrix.Android.UIReqs.TestTarget;
using Xunit;

namespace CodeBrix.Android.UIReqs.HostFreeTests;

/// <summary>
/// [AP7-B TerminalView RE-GATE 2] Host-free tests of the soft-keyboard mask the host applies to the frames it saves
/// (no device, no emulator): a PNG written from a masked frame has exactly the known rectangle in the mask colour.
/// [AP8-S batch 0] The mask also covers <see cref="ImeMask.ShadowBand"/> rows above the reported rectangle (the keyboard's
/// top shadow row, which the device-reported IME rectangle does not include).
/// </summary>
public class ImeMaskTests
{
    private const int Width = 40;
    private const int Height = 30;

    private static TestFrame Gradient()
    {
        var rgba = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var i = ((y * Width) + x) * 4;
                rgba[i] = (byte)(x * 5);
                rgba[i + 1] = (byte)(y * 7);
                rgba[i + 2] = 200;
                rgba[i + 3] = 255;
            }
        }

        return new TestFrame(rgba, Width, Height, 7);
    }

    private static bool Inside(int x, int y, (int Left, int Top, int Right, int Bottom) r) =>
        x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;

    [Fact]
    public void A_png_of_a_masked_frame_has_exactly_the_known_rect_and_its_shadow_band_in_the_mask_colour()
    {
        //Arrange
        var frame = Gradient();
        var reported = (Left: 0, Top: 18, Right: Width, Bottom: Height);
        var rect = (Left: 0, Top: 18 - ImeMask.ShadowBand, Right: Width, Bottom: Height);
        var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"imemask-{System.Guid.NewGuid():N}.png");

        //Act
        var masked = ImeMask.Apply(frame, reported, out var applied);
        masked.SavePng(png);
        var (decoded, w, h) = PngCodec.Decode(System.IO.File.ReadAllBytes(png));
        System.IO.File.Delete(png);

        //Assert
        Assert.Equal(rect, applied);
        Assert.Equal((Width, Height), (w, h));
        Assert.Equal(7, masked.Sequence);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var i = ((y * Width) + x) * 4;
                var expected = Inside(x, y, rect)
                    ? new[] { ImeMask.Colour.R, ImeMask.Colour.G, ImeMask.Colour.B, ImeMask.Colour.A }
                    : new[] { frame.Rgba[i], frame.Rgba[i + 1], frame.Rgba[i + 2], frame.Rgba[i + 3] };
                Assert.Equal(expected, new[] { decoded[i], decoded[i + 1], decoded[i + 2], decoded[i + 3] });
            }
        }
    }

    [Fact]
    public void The_captured_frame_itself_is_never_changed()
    {
        //Arrange
        var frame = Gradient();
        var before = (byte[])frame.Rgba.Clone();

        //Act
        var masked = ImeMask.Apply(frame, (5, 5, 20, 20), out _);

        //Assert
        Assert.NotSame(frame, masked);
        Assert.Equal(before, frame.Rgba);
    }

    [Fact]
    public void A_rect_reaching_past_the_frame_is_clipped_to_it()
    {
        //Act
        var masked = ImeMask.Apply(Gradient(), (-10, 25, Width + 50, Height + 100), out var applied);

        //Assert
        Assert.Equal((0, 25 - ImeMask.ShadowBand, Width, Height), applied);
        Assert.Equal(ImeMask.Colour.R, masked.Rgba[((29 * Width) + 39) * 4]);
        Assert.Equal(ImeMask.Colour.R, masked.Rgba[((17 * Width) + 0) * 4]);
        Assert.Equal(0, masked.Rgba[((16 * Width) + 0) * 4]);
    }

    [Fact]
    public void A_shadow_band_near_the_top_of_the_frame_is_clipped_to_row_zero()
    {
        //Act
        ImeMask.Apply(Gradient(), (0, 3, Width, 10), out var applied);

        //Assert
        Assert.Equal((0, 0, Width, 10), applied);
    }

    [Fact]
    public void The_shadow_band_covers_the_keyboard_s_top_shadow_row_just_above_the_reported_rect()
    {
        //Arrange - AP7-B CLOSE: the keyboard's 1 px shadow row at y = top - 1 (Landscape: rect top 724, shadow at 723),
        // across the rect's full width, fell outside the unwidened mask.
        var frame = Gradient();

        //Act
        var masked = ImeMask.Apply(frame, (10, 20, 30, Height), out var applied);

        //Assert
        Assert.True(ImeMask.ShadowBand >= 3, "the band must cover the keyboard's 1-3 px top shadow");
        Assert.Equal((10, 20 - ImeMask.ShadowBand, 30, Height), applied);
        for (var x = 10; x < 30; x++)
        {
            Assert.Equal(ImeMask.Colour.R, masked.Rgba[((19 * Width) + x) * 4]);
        }

        Assert.Equal(frame.Rgba[((19 * Width) + 9) * 4], masked.Rgba[((19 * Width) + 9) * 4]);
        Assert.Equal(frame.Rgba[((19 * Width) + 30) * 4], masked.Rgba[((19 * Width) + 30) * 4]);
        Assert.Equal(frame.Rgba[((20 - ImeMask.ShadowBand - 1) * Width + 15) * 4], masked.Rgba[((20 - ImeMask.ShadowBand - 1) * Width + 15) * 4]);
    }

    [Fact]
    public void A_rect_outside_the_frame_masks_nothing()
    {
        //Arrange
        var frame = Gradient();

        //Act
        var masked = ImeMask.Apply(frame, (0, Height + ImeMask.ShadowBand, Width, Height + 400), out var applied);

        //Assert
        Assert.Null(applied);
        Assert.Same(frame, masked);
    }

    [Fact]
    public void The_capture_request_carries_the_rect_or_nothing()
    {
        //Assert
        Assert.Equal((0, 1100, 1080, 1920), ImeMask.ReadRect(new JsonObject { ["request"] = "capture", ["seq"] = 3, ["ime"] = new JsonArray(0, 1100, 1080, 1920) }));
        Assert.Null(ImeMask.ReadRect(new JsonObject { ["request"] = "capture", ["seq"] = 3 }));
    }
}
