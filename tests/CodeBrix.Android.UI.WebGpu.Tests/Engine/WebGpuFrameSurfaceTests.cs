// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/WebGpuFrameSurfaceTests.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly.

#nullable enable

using System.Collections.Generic;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.UI.Xaml.Media;
using SilverAssertions;
using SkiaSharp;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// The paint side, on raster canvases: the frame lands where the stretch says, the overlay lands on top of it, and the
/// picture is handed over safely.
/// </summary>
public class WebGpuFrameSurfaceTests
{
	private static readonly Size Area = new(100, 80);

	private static SKImage SolidImage(int width, int height, SKColor color)
	{
		using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
		bitmap.Erase(color);
		bitmap.SetImmutable();
		return SKImage.FromBitmap(bitmap);
	}

	private static SKBitmap Paint(WebGpuFrameSurface surface, SKColor background)
	{
		var bitmap = new SKBitmap(new SKImageInfo((int)Area.Width, (int)Area.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
		using var canvas = new SKCanvas(bitmap);
		canvas.Clear(background);
		surface.Paint(canvas, Area);
		canvas.Flush();
		return bitmap;
	}

	[Fact]
	public void with_no_frame_what_is_behind_the_view_shows_through()
	{
		//Arrange
		var surface = new WebGpuFrameSurface(() => { }, null);

		//Act
		using var painted = Paint(surface, SKColors.Blue);

		//Assert
		painted.GetPixel(50, 40).Should().Be(SKColors.Blue);
	}

	[Fact]
	public void a_translucent_frame_is_composited_over_what_is_behind()
	{
		//Arrange
		var surface = new WebGpuFrameSurface(() => { }, null);
		surface.Present(SolidImage(100, 80, SKColors.Transparent));

		//Act
		using var painted = Paint(surface, SKColors.Blue);

		//Assert
		painted.GetPixel(50, 40).Should().Be(SKColors.Blue);
	}

	[Fact]
	public void a_presented_frame_fills_the_area_under_fill()
	{
		//Arrange
		var surface = new WebGpuFrameSurface(() => { }, null);
		surface.Present(SolidImage(100, 80, SKColors.Red));

		//Act
		using var painted = Paint(surface, SKColors.Blue);

		//Assert
		painted.GetPixel(0, 0).Should().Be(SKColors.Red);
		painted.GetPixel(99, 79).Should().Be(SKColors.Red);
	}

	[Fact]
	public void the_overlay_draws_over_the_frame()
	{
		//Arrange
		var order = new List<string>();
		var surface = new WebGpuFrameSurface(() => { }, (canvas, size) =>
		{
			order.Add($"overlay {size.Width}x{size.Height}");
			using var paint = new SKPaint { Color = SKColors.Lime };
			canvas.DrawRect(new SKRect(10, 10, 30, 30), paint);
		});
		surface.Present(SolidImage(100, 80, SKColors.Red));

		//Act
		using var painted = Paint(surface, SKColors.Blue);

		//Assert
		painted.GetPixel(20, 20).Should().Be(SKColors.Lime);
		painted.GetPixel(60, 60).Should().Be(SKColors.Red);
		order.Should().ContainSingle().Which.Should().Be("overlay 100x80");
	}

	[Fact]
	public void the_overlay_runs_even_before_the_first_frame()
	{
		//Arrange
		var calls = 0;
		var surface = new WebGpuFrameSurface(() => { }, (_, _) => calls++);

		//Act
		using var painted = Paint(surface, SKColors.Blue);

		//Assert
		calls.Should().Be(1);
	}

	[Fact]
	public void the_overlay_is_clipped_to_the_area()
	{
		//Arrange
		var surface = new WebGpuFrameSurface(() => { }, (canvas, _) => canvas.Clear(SKColors.Lime));
		var bitmap = new SKBitmap(new SKImageInfo(200, 200, SKColorType.Bgra8888, SKAlphaType.Premul));
		using var canvas = new SKCanvas(bitmap);
		canvas.Clear(SKColors.Blue);

		//Act
		surface.Paint(canvas, Area);

		//Assert
		bitmap.GetPixel(150, 150).Should().Be(SKColors.Blue);
		bitmap.Dispose();
	}

	[Fact]
	public void a_present_asks_for_a_repaint_except_while_resizing()
	{
		//Arrange
		var repaints = 0;
		var surface = new WebGpuFrameSurface(() => repaints++, null);

		//Act
		surface.Present(SolidImage(4, 4, SKColors.Red));
		surface.IsResizing = true;
		surface.Present(SolidImage(4, 4, SKColors.Red));

		//Assert
		repaints.Should().Be(1);
		surface.FramesPresented.Should().Be(2);
	}

	[Fact]
	public void clearing_drops_the_picture_and_repaints()
	{
		//Arrange
		var repaints = 0;
		var surface = new WebGpuFrameSurface(() => repaints++, null);
		surface.Present(SolidImage(4, 4, SKColors.Red));

		//Act
		surface.ClearFrame();

		//Assert
		surface.HasFrame.Should().BeFalse();
		surface.CaptureFrame().Should().BeNull();
		repaints.Should().Be(2);
	}

	[Fact]
	public void a_capture_is_an_independent_copy()
	{
		//Arrange
		var surface = new WebGpuFrameSurface(() => { }, null);
		surface.Present(SolidImage(4, 4, SKColors.Red));

		//Act
		using var capture = surface.CaptureFrame();
		surface.ClearFrame();

		//Assert
		capture.Should().NotBeNull();
		using var bitmap = SKBitmap.FromImage(capture!);
		bitmap.GetPixel(1, 1).Should().Be(SKColors.Red);
	}

	[Theory]
	[InlineData(Stretch.Fill, 0f, 0f, 100f, 80f)]
	[InlineData(Stretch.None, 30f, 25f, 40f, 30f)]
	[InlineData(Stretch.Uniform, 0f, 2.5f, 100f, 75f)]
	[InlineData(Stretch.UniformToFill, -3.333f, 0f, 106.666f, 80f)]
	public void the_destination_follows_the_stretch(Stretch stretch, float left, float top, float width, float height)
	{
		//Act
		var rect = WebGpuFrameSurface.ComputeDestinationRect(40, 30, Area, stretch);

		//Assert
		rect.Left.Should().BeApproximately(left, 0.01f);
		rect.Top.Should().BeApproximately(top, 0.01f);
		rect.Width.Should().BeApproximately(width, 0.01f);
		rect.Height.Should().BeApproximately(height, 0.01f);
	}
}
