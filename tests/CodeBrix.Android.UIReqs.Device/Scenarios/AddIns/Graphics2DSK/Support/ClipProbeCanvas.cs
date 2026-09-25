using SkiaSharp;
using Size = Windows.Foundation.Size;

namespace CodeBrix.Platform.UI.AddIn.Graphics2DSK.UIReqs.Support;

/// <summary>
/// The fixture element that tries to draw where it is not allowed to. It paints the stripes its
/// Fill names, exactly as a <see cref="FillCanvas"/> does, and then paints four
/// <see cref="ProbeName"/> bands that lie WHOLLY outside the (0, 0, area.Width, area.Height)
/// rectangle it was given - one starting at -200, -200 above it, one below it, one to its left
/// and one to its right, each reaching 200 pixels further out than the element goes.
/// <para>
/// Nothing of those four bands may ever appear on the panel. They are the visible half of the
/// requirement "drawing outside the area is clipped away": a scenario shows this element inside
/// a container far bigger than itself and states that the container holds no
/// <see cref="ProbeName"/> anywhere.
/// </para>
/// </summary>
public sealed class ClipProbeCanvas : SKCanvasFixture
{
	/// <summary>How far outside its own area the element reaches when it draws the probes.</summary>
	public const int ProbeReach = 200;

	/// <summary>The colour name a scenario uses for the ink that must never be seen.</summary>
	public const string ProbeName = "Magenta";

	/// <summary>The colour the probes are painted in - the colour <see cref="ProbeName"/> stands for.</summary>
	public static SKColor ProbeColor => new(0xFF, 0x00, 0xFF);

	/// <summary>
	/// Paints the stripes, then reaches outside the drawing area on all four sides. Called by
	/// the framework on the UI thread, inside the compositor's painting session.
	/// </summary>
	/// <param name="canvas">The canvas to draw on, with the origin at this element's top left.</param>
	/// <param name="area">The area this element was arranged at.</param>
	protected override void RenderOverride(SKCanvas canvas, Size area)
	{
		base.RenderOverride(canvas, area);

		var width = WholePixels(area.Width);
		var height = WholePixels(area.Height);

		using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = ProbeColor };

		// Above, starting at (-200, -200) and ending on the element's own top edge; below, from
		// the bottom edge down; and one band off each side. Every one of them is entirely
		// outside the rectangle the element was given, so every one of them must be clipped.
		canvas.DrawRect(SKRect.Create(-ProbeReach, -ProbeReach, width + (2 * ProbeReach), ProbeReach), paint);
		canvas.DrawRect(SKRect.Create(-ProbeReach, height, width + (2 * ProbeReach), ProbeReach), paint);
		canvas.DrawRect(SKRect.Create(-ProbeReach, 0, ProbeReach, height), paint);
		canvas.DrawRect(SKRect.Create(width, 0, ProbeReach, height), paint);
	}
}
