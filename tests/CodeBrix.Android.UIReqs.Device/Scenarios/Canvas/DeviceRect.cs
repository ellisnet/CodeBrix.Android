using System;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// A rectangle of device pixels. Geometry always comes from the visual tree, never from
/// coordinates guessed in a feature file: an element's rectangle is
/// <c>TransformToVisual(null).TransformBounds(0, 0, ActualWidth, ActualHeight)</c>. Display
/// scale is fixed at 1.0, so a logical pixel is a device pixel.
/// </summary>
/// <param name="X">The left edge, in device pixels.</param>
/// <param name="Y">The top edge, in device pixels.</param>
/// <param name="Width">The width, in device pixels.</param>
/// <param name="Height">The height, in device pixels.</param>
public readonly record struct DeviceRect(int X, int Y, int Width, int Height)
{
	/// <summary>
	/// How far every edge is pulled in, so that the antialiased pixels an edge always has do
	/// not decide what a region is "uniformly" painted.
	/// </summary>
	public const int DefaultInset = 1;

	/// <summary>The first column outside the rectangle.</summary>
	public int Right => X + Width;

	/// <summary>The first row below the rectangle.</summary>
	public int Bottom => Y + Height;

	/// <summary>Whether the rectangle holds no pixels.</summary>
	public bool IsEmpty => Width <= 0 || Height <= 0;

	/// <summary>The centre pixel of the rectangle - where a tap on the element lands.</summary>
	public (int X, int Y) Center => (X + (Width / 2), Y + (Height / 2));

	/// <summary>Whether a pixel lies inside the rectangle.</summary>
	/// <param name="x">The pixel's x coordinate.</param>
	/// <param name="y">The pixel's y coordinate.</param>
	/// <returns><c>true</c> when the pixel is inside.</returns>
	public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

	/// <summary>The largest rectangle that lies inside both this one and another.</summary>
	/// <param name="other">The rectangle to intersect with.</param>
	/// <returns>The intersection, which may be empty.</returns>
	public DeviceRect Intersect(DeviceRect other)
	{
		var left = Math.Max(X, other.X);
		var top = Math.Max(Y, other.Y);
		var right = Math.Min(Right, other.Right);
		var bottom = Math.Min(Bottom, other.Bottom);
		return new DeviceRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
	}

	/// <summary>
	/// The same rectangle with every edge pulled in by the same number of pixels - the way a
	/// rectangle an engine computed is turned into one whose antialiased edges are outside it.
	/// <see cref="FromBounds"/> does this for an element's transformed bounds; this does it for a
	/// rectangle that was worked out some other way.
	/// </summary>
	/// <param name="inset">How far to pull each edge in. A negative value grows the rectangle.</param>
	/// <returns>The inset rectangle, which may be empty.</returns>
	public DeviceRect Inset(int inset) => new(
		X + inset,
		Y + inset,
		Math.Max(0, Width - (2 * inset)),
		Math.Max(0, Height - (2 * inset)));

	/// <summary>How far this rectangle's edges are from another's, edge by edge.</summary>
	/// <param name="other">The rectangle to compare with.</param>
	/// <returns>The largest edge difference, in pixels.</returns>
	public int LargestEdgeDifference(DeviceRect other) => Math.Max(
		Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y)),
		Math.Max(Math.Abs(Right - other.Right), Math.Abs(Bottom - other.Bottom)));

	/// <summary>
	/// Rounds a floating-point rectangle INWARD to whole device pixels and pulls every edge in
	/// by <paramref name="inset"/>, so nothing an edge antialiased into is inside the result.
	/// </summary>
	/// <param name="bounds">The transformed bounds of an element.</param>
	/// <param name="inset">How far to pull each edge in.</param>
	/// <returns>The device rectangle.</returns>
	public static DeviceRect FromBounds(Rect bounds, int inset = DefaultInset)
	{
		var left = (int) Math.Ceiling(bounds.Left) + inset;
		var top = (int) Math.Ceiling(bounds.Top) + inset;
		var right = (int) Math.Floor(bounds.Right) - inset;
		var bottom = (int) Math.Floor(bounds.Bottom) - inset;
		return new DeviceRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
	}

	/// <summary>
	/// The device rectangle of an element, read from the live visual tree on the UI thread.
	/// </summary>
	/// <param name="element">The element to measure.</param>
	/// <param name="inset">How far to pull each edge in; the default excludes edge antialiasing.</param>
	/// <returns>The element's device rectangle.</returns>
	/// <exception cref="InvalidOperationException">The element has no size, so it is not laid out.</exception>
	public static async Task<DeviceRect> OfAsync(FrameworkElement element, int inset = DefaultInset)
	{
		ArgumentNullException.ThrowIfNull(element);

		var bounds = default(Rect);
		var name = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			name = element.Name;
			bounds = element
				.TransformToVisual(null)
				.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
		}).ConfigureAwait(false);

		if (bounds.Width <= 0 || bounds.Height <= 0)
		{
			throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
				$"Element \"{name}\" has no laid-out size ({bounds.Width} x {bounds.Height}), so it has no device rectangle. Has the tree been laid out?"));
		}

		return FromBounds(bounds, inset);
	}

	/// <summary>The rectangle as "x,y w x h".</summary>
	/// <returns>The description.</returns>
	public override string ToString() => string.Create(CultureInfo.InvariantCulture,
		$"({X},{Y}) {Width} x {Height}");
}
