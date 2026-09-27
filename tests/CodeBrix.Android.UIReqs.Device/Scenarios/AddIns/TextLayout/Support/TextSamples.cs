namespace CodeBrix.Platform.UI.AddIn.TextLayout.UIReqs.Support;

/// <summary>
/// The text a scenario lays out, and the font it lays it out in. Everything here is a C#
/// literal: this add-in reads no file, downloads nothing and needs no fixture on disk.
/// </summary>
/// <remarks>
/// The font is named by URI, never by a generic family alias. A generic alias resolves to
/// whatever the machine happens to have - which is exactly what a pixel assertion cannot live
/// with, and what the family rule about system fonts forbids. This is the same URI the virtual
/// application draws every other piece of text with, so it is already warm before the first
/// scenario and the shapes a scenario measures are the shapes it paints.
/// </remarks>
public static class TextSamples
{
	/// <summary>The application's own font, as an application URI.</summary>
	public const string ApplicationFont = "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf";

	/// <summary>One short line, with an ascender, a descender and a round glyph in it.</summary>
	public const string OneLine = "Requirement";

	/// <summary>A line long enough to fill a 900 pixel canvas at 48 points and to wrap at 300.</summary>
	public const string LongLine = "The engine measures and paints every word of this line";

	/// <summary>Two lines, written with the escape a feature file spells a line break with.</summary>
	public const string TwoLines = @"First\nSecond";

	/// <summary>Five characters whose middle one is a space, for the advance-without-ink case.</summary>
	public const string Spaced = "AB CD";

	/// <summary>
	/// A Hebrew word followed by an English one: the shortest piece of text whose base direction
	/// the engine resolves as right to left, and whose two halves it therefore has to place in
	/// the opposite order to the one they are written in. The application's font carries both
	/// scripts, so nothing falls back to another face.
	/// </summary>
	public const string RightToLeft = "שלום world";

	/// <summary>The escape a feature file writes for a line break inside a quoted string.</summary>
	public const string LineBreakEscape = @"\n";
}
