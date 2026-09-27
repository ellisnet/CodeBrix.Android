using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CodeBrix.Platform.UI.AddIn.TerminalView.UIReqs.Support;

/// <summary>
/// The bytes a scenario feeds a terminal, by name. A terminal has no content API at all - it is
/// fed a stream and it draws what the stream says - so a fixture here is a C# string literal of
/// VT output, and a feature file names it rather than spelling escape codes out.
/// <para>
/// Every script that draws starts by clearing the screen and homing the cursor, so what a
/// scenario asserts about a cell is what THAT script put there and not what a previous one left
/// behind. "M" is the sample glyph throughout: in a monospaced face it fills its cell, which is
/// what makes a cell-sized colour claim decisive.
/// </para>
/// </summary>
public static class TerminalScripts
{
	/// <summary>
	/// The monospace face the terminal draws with, as its own default names it. A proportional
	/// face would misalign every column, so no scenario substitutes the application font.
	/// </summary>
	public const string TerminalFont = "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf";

	/// <summary>The escape character every control sequence starts with.</summary>
	public const string Escape = "\u001b";

	/// <summary>How a feature file writes <see cref="Escape"/> inside a quoted string.</summary>
	public const string EscapeWord = "ESC";

	/// <summary>How many numbered lines <see cref="LongOutput"/> is, which is several screens.</summary>
	public const int LongOutputLines = 200;

	/// <summary>Clears the whole screen and puts the cursor back in its top left corner.</summary>
	public const string ClearAndHome = Escape + "[2J" + Escape + "[H";

	/// <summary>DECTCEM off: the cursor stops being drawn at all, blink and outline alike.</summary>
	public const string HideCursor = Escape + "[?25l";

	/// <summary>A line of ordinary text in the terminal's own default foreground.</summary>
	public const string Banner = ClearAndHome + "HELLO WORLD";

	/// <summary>Ten sample glyphs, each filling its own cell, starting at the top left corner.</summary>
	public const string SampleGlyphs = ClearAndHome + "MMMMMMMMMM";

	/// <summary>Four sample glyphs in the palette's red, then the default foreground again.</summary>
	public const string SgrRed = ClearAndHome + Escape + "[31mMMMM" + Escape + "[0m";

	/// <summary>
	/// Four INVERSE blanks: a run with no glyph in it, whose ground is the foreground colour.
	/// Blanks rather than letters on purpose - an inverted cell holding a letter is the
	/// foreground colour with the letter punched out of it in the background colour, and a
	/// scenario that wants to say "the cell is filled with the foreground colour" means a cell
	/// with nothing punched out of it.
	/// </summary>
	public const string Inverse = ClearAndHome + Escape + "[7m    " + Escape + "[0m";

	/// <summary>
	/// The same four glyphs in the palette's red twice: plain on the first row and BOLD on the
	/// second. Bold promotes a dark palette colour to its bright twin, so the two rows are the
	/// same text in two different colours.
	/// </summary>
	public const string Attributes = ClearAndHome
		+ Escape + "[31mMMMM" + Escape + "[0m\r\n"
		+ Escape + "[1;31mMMMM" + Escape + "[0m";

	/// <summary>An OSC 0 sequence that renames the session and draws nothing.</summary>
	public const string OscTitle = Escape + "]0;Session\u0007";

	/// <summary>The title <see cref="OscTitle"/> asks for.</summary>
	public const string OscTitleText = "Session";

	private static readonly Lazy<string> LongOutputText = new(BuildLongOutput);

	private static readonly string[] FixtureNames =
	[
		nameof(Attributes), nameof(Banner), nameof(HideCursor), nameof(Inverse),
		nameof(LongOutput), nameof(OscTitle), nameof(SampleGlyphs), nameof(SgrRed),
	];

	/// <summary>
	/// Two hundred numbered lines, which is several screens on any panel this runs on and so
	/// fills the scrollback. Line endings are explicit CR + LF: the control converts a bare LF
	/// only when it is told to.
	/// </summary>
	public static string LongOutput => LongOutputText.Value;

	/// <summary>The names a feature file may write, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names => FixtureNames;

	/// <summary>The script a feature file named.</summary>
	/// <param name="name">The fixture name, as a scenario writes it.</param>
	/// <returns>The VT output to feed.</returns>
	/// <exception cref="NotSupportedException">No fixture of that name exists.</exception>
	public static string Build(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return name.Trim().Trim('"') switch
		{
			nameof(Attributes) => Attributes,
			nameof(Banner) => Banner,
			nameof(HideCursor) => HideCursor,
			nameof(Inverse) => Inverse,
			nameof(LongOutput) => LongOutput,
			nameof(OscTitle) => OscTitle,
			nameof(SampleGlyphs) => SampleGlyphs,
			nameof(SgrRed) => SgrRed,
			_ => throw new NotSupportedException(
				$"There is no terminal script named \"{name}\". The scenarios have: "
				+ string.Join(", ", Names) + "."),
		};
	}

	/// <summary>
	/// Reads the input a scenario expects a terminal to have emitted. A feature file cannot
	/// write a raw escape character between quotes, so it writes the word <c>ESC</c> where one
	/// belongs and everything else literally: <c>"ESC[A"</c> is the three characters an arrow
	/// key sends.
	/// </summary>
	/// <param name="text">The text a feature file wrote.</param>
	/// <returns>The bytes it stands for.</returns>
	public static string ReadSequence(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return text.Replace(EscapeWord, Escape, StringComparison.Ordinal);
	}

	/// <summary>
	/// Writes bytes the way a feature file would, so that a failure message about an escape
	/// sequence is readable rather than a row of invisible characters.
	/// </summary>
	/// <param name="text">The bytes.</param>
	/// <returns>The text a scenario would write for them.</returns>
	public static string DescribeSequence(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return text.Replace(Escape, EscapeWord, StringComparison.Ordinal);
	}

	private static string BuildLongOutput()
	{
		var lines = new StringBuilder(ClearAndHome);
		for (var line = 1; line <= LongOutputLines; line++)
		{
			// Each line carries a bar of sample glyphs whose length is its own, so that one
			// screenful of this output looks nothing like any other screenful of it. A page of
			// text that differed only in a line number would be nearly the same picture, and a
			// scenario about scrolling would then be claiming something no eye could confirm.
			var bar = new string('M', 1 + (line * 7 % 37));
			lines.Append(CultureInfo.InvariantCulture, $"line {line} {bar}\r\n");
		}

		return lines.ToString();
	}
}
