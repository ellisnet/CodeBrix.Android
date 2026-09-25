using System;
using System.Drawing;
using Pinta.Brix.Engine;
using SkiaSharp;

//was previously: namespace Pinta.Tools;
namespace Pinta.Brix.Tools;

// That handle is used as second brush to show position of stamp tool origin
public class BrushHandle : IToolHandle
{
	private int brush_width;
	public int BrushWidth {
		get { return brush_width; }
		set {
			if (value > 0)
				brush_width = value;
		}
	}
	public bool Active { get; set; }
	public PointD CanvasPosition { get; set; }
	private readonly IWorkspaceService workspace;

	public BrushHandle (IWorkspaceService workspace)
	{
		this.workspace = workspace;
	}

	public bool ContainsPoint (PointD windowPoint)
		=> ComputeWindowRect ().ContainsPoint (windowPoint);

	/// <summary>
	/// Drawing shape like in GdkExtensions.CreateIconWithShape
	/// </summary>
	public void Draw (SKCanvas canvas)
	{
		double zoom =
		    (PintaCore.Workspace.HasOpenDocuments)
		    ? Math.Min (30d, workspace.GetScale ())
		    : 1d;
		int clampedWidth = (int) Math.Min (800d, brush_width * zoom);

		if (clampedWidth < 3)
			return;

		int halfOfShapeWidth = clampedWidth / 2;

		RectangleF shapeRect = new () {
			X = (float) (CanvasPosition.X * zoom),
			Y = (float) (CanvasPosition.Y * zoom),
			Width = clampedWidth,
			Height = clampedWidth
		};

		SKColor outerColor = new (0xFF, 0xFF, 0xFF, 0xBF);
		SKColor innerColor = new (0x00, 0x00, 0x00, 0xFF);

		SKPoint origin = new (shapeRect.X, shapeRect.Y);

		using SKPaint outer = new () {
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 2,
			Color = outerColor,
			IsAntialias = true,
		};
		canvas.DrawCircle (origin, halfOfShapeWidth, outer);

		using SKPaint inner = new () {
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1,
			Color = innerColor,
			IsAntialias = true,
		};
		canvas.DrawCircle (origin, halfOfShapeWidth - 1, inner);
	}

	public RectangleI InvalidateRect => ComputeWindowRect ().Inflated (2, 2).ToInt ();

	/// <summary>
	/// Bounding rectangle of the handle (in window space). Similar to MoveHandle.
	/// </summary>

	private RectangleD ComputeWindowRect ()
	{
		double diameter = brush_width;
		double radius = diameter / 2.0;

		PointD windowPt = workspace.CanvasPointToView (CanvasPosition);
		return new RectangleD (windowPt.X - radius, windowPt.Y - radius, diameter, diameter);
	}
}
