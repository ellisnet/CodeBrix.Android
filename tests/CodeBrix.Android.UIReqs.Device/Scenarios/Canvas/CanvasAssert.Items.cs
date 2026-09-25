using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The vocabulary the Items group's requirements need. A list's item is not a flat rectangle of
/// one colour - it carries a label, a selection background drawn inside its margins and, in a
/// multi-select list, a check box - so "this item is the selected one" is a statement about how
/// much of the item the selection colour covers rather than about every pixel of it.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>
	/// The share of an item's rectangle a colour must cover for the item to count as filled
	/// with it. A selected item's background is drawn inside the item's margins and behind its
	/// label, so it never reaches the whole rectangle.
	/// </summary>
	public const double FilledFraction = 0.25;

	/// <summary>Asserts that a colour covers most of a region.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="color">The colour it must be filled with.</param>
	/// <param name="minFraction">How much of it the colour must cover; the default is a quarter.</param>
	public static void IsFilledWith(this Region region, Color color, double minFraction = FilledFraction) =>
		Contains(region, color, minFraction);
}
