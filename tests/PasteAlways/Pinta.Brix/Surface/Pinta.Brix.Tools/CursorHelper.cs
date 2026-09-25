// CursorHelper.cs
//
// Ported from the upstream toolkit-extension helper that composes tool
// cursors: a named icon plus a brush-size outline (ellipse or rectangle),
// rendered with the engine's drawing layer instead of toolkit surfaces.

using System;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using Drawing = Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Tools;

public static class CursorHelper
{
	public static ImageSurface CreateIconWithShape (
		string imgName,
		CursorShape shape,
		double scale,
		int shapeWidth,
		int imgToShapeX,
		int imgToShapeY,
		out int shapeX,
		out int shapeY)
	{
		return CreateIconWithShape (imgName, shape, scale, shapeWidth, shapeWidth, 0, imgToShapeX, imgToShapeY, out shapeX, out shapeY);
	}

	public static ImageSurface CreateIconWithShape (
		string imgName,
		CursorShape shape,
		double scale,
		int shapeWidth,
		int shapeHeight,
		int shapeAngle,
		int imgToShapeX,
		int imgToShapeY,
		out int shapeX,
		out int shapeY)
	{
		ImageSurface img = PintaCore.Resources.GetIcon (imgName);

		double zoom =
			(PintaCore.Workspace.HasOpenDocuments)
			? Math.Min (30d, scale)
			: 1d;

		int clampedWidth = (int) Math.Min (800d, shapeWidth * zoom);
		int halfOfShapeWidth = clampedWidth / 2;
		int clampedHeight = (int) Math.Min (800d, shapeHeight * zoom);
		int halfOfShapeHeight = clampedHeight / 2;

		// Calculate bounding boxes around the both image and shape
		// relative to the image top-left corner.

		RectangleI imgBBox = new (0, 0, img.Width, img.Height);

		RectangleI initialShapeBBox = new (
			imgToShapeX - Math.Max (halfOfShapeWidth, halfOfShapeHeight),
			imgToShapeY - Math.Max (halfOfShapeWidth, halfOfShapeHeight),
			Math.Max (clampedWidth, clampedHeight),
			Math.Max (clampedWidth, clampedHeight));

		// Inflate shape bounding box to allow for anti-aliasing
		RectangleI inflatedBBox = initialShapeBBox.Inflated (2, 2);

		// To determine required size of icon,
		// find union of the image and shape bounding boxes
		// (still relative to image top-left corner)
		RectangleI iconBBox = imgBBox.Union (inflatedBBox);

		// Image top-left corner in icon coordinates
		int imgX = imgBBox.Left - iconBBox.Left;
		int imgY = imgBBox.Top - iconBBox.Top;

		// Shape center point in icon coordinates
		shapeX = imgToShapeX - iconBBox.Left;
		shapeY = imgToShapeY - iconBBox.Top;

		ImageSurface i = CairoExtensions.CreateImageSurface (
			Drawing.Format.Argb32,
			iconBBox.Width,
			iconBBox.Height);

		using Drawing.Context g = new (i);

		// Don't show shape if shapeWidth less than 3,
		if (clampedHeight > 3) {

			int diam = Math.Max (1, clampedWidth - 2);

			RectangleD shapeRect = new (
				shapeX - halfOfShapeWidth,
				shapeY - halfOfShapeHeight,
				diam,
				clampedHeight);

			Drawing.Color outerColor = new (255, 255, 255, 0.75);
			Drawing.Color innerColor = new (0, 0, 0);

			switch (shape) {
				case CursorShape.Ellipse:
					g.DrawEllipse (shapeRect, outerColor, 2);
					shapeRect = shapeRect.Inflated (-1, -1);
					g.DrawEllipse (shapeRect, innerColor, 1);
					break;
				case CursorShape.Rectangle:
					if (shapeAngle == 0) {
						g.DrawRectangle (shapeRect, outerColor, 1);
						shapeRect = shapeRect.Inflated (-1, -1);
						g.DrawRectangle (shapeRect, innerColor, 1);
					} else {
						PointD[] pointsOfRotatedRectangle = RotateRectangle (shapeRect, shapeAngle);
						shapeRect = shapeRect.Inflated (-1, -1);
						PointD[] pointsOfInflatedRotatedRectangle = RotateRectangle (shapeRect, shapeAngle);
						g.DrawPolygonal (new ReadOnlySpan<PointD> (pointsOfRotatedRectangle), outerColor, Drawing.LineCap.Butt);
						g.DrawPolygonal (new ReadOnlySpan<PointD> ([.. pointsOfInflatedRotatedRectangle, pointsOfInflatedRotatedRectangle[0]]), innerColor, Drawing.LineCap.Butt);
					}
					break;
			}
		}

		// Draw the image
		g.SetSourceSurface (img, imgX, imgY);
		g.Paint ();

		return i;
	}

	private static PointD[] RotateRectangle (RectangleD rectangle, int angle_in_degrees)
	{
		float angle_in_radians = float.DegreesToRadians (-angle_in_degrees);
		Matrix3x2D rotation = Matrix3x2D.CreateRotation (new RadiansAngle (angle_in_radians), rectangle.GetCenter ());
		return [
			rectangle.Location ().Transformed (rotation),
			new PointD (rectangle.Location ().X, rectangle.EndLocation ().Y).Transformed (rotation),
			rectangle.EndLocation ().Transformed (rotation),
			new PointD (rectangle.EndLocation ().X, rectangle.Location ().Y).Transformed (rotation)
		];
	}
}
