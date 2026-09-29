using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CodeBrix.Platform.UI.AddIn.AdvancedTextEdit.UIReqs.Support;

/// <summary>
/// The documents a scenario opens in the editor, and the font it draws them with. Everything
/// here is a C# literal or generated in C#: this add-in reads no file, downloads nothing and
/// needs no fixture on disk.
/// </summary>
/// <remarks>
/// The font is named by URI, never by a generic family alias. The editor's own default family
/// is the generic name "monospace", which resolves to nothing on Linux - and the family rule
/// forbids a system-font fallback, so an editor left on its default would draw nothing at all
/// and every "has ink" claim would fail for a reason that has nothing to do with the editor.
/// Every scenario therefore names this URI, which the font package puts beside the test
/// executable.
/// </remarks>
public static class EditorSamples
{
	/// <summary>The monospaced face every scenario draws with, as an application URI.</summary>
	public const string MonospaceFont = "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf";

	/// <summary>The folder beside the test executable the font package's assembly name gives.</summary>
	public const string MonospaceFontPackage = "CodeBrix.Platform.Fonts.RobotoMono";

	/// <summary>The folder inside that one the faces land in.</summary>
	public const string FontsFolderName = "Fonts";

	/// <summary>The face the font URI resolves to.</summary>
	public const string MonospaceFontFile = "RobotoMono.ttf";

	/// <summary>
	/// The manifest that sits beside the face. It is how the text engine resolves a weight to
	/// the right file: without it every weight is drawn with the one face the URI names, and a
	/// bold keyword would be indistinguishable from plain text.
	/// </summary>
	public const string MonospaceFontManifest = MonospaceFontFile + ".manifest";

	/// <summary>The escape a feature file writes for a line break inside a quoted string.</summary>
	public const string LineBreakEscape = @"\n";

	/// <summary>Three short lines, the smallest document with more than one line in it.</summary>
	public const string ThreeLines = "one\ntwo\nthree";

	/// <summary>
	/// Five lines of C# holding one keyword, one comment and one plain statement, so that a
	/// highlighting scenario has three colours to tell apart: "if" on line 1 is a keyword,
	/// "// count it" on line 3 is a comment, and "total" on line 4 is neither.
	/// </summary>
	public const string CSharpSnippet = "if (ready)\n{\n    // count it\n    total = total + more;\n}";

	/// <summary>
	/// Two lines holding the same word four times, for the search scenarios: two matches on each
	/// line, so a marked frame shows the marking is per match and not per line.
	/// </summary>
	public const string RepeatedWords = "alpha beta alpha\ngamma alpha delta alpha";

	private static readonly Lazy<string> LongLineText = new(BuildLongLine);

	private static readonly Lazy<string> FiveHundredLinesText = new(BuildFiveHundredLines);

	/// <summary>
	/// One line of 399 characters - eighty short words separated by spaces - which is far wider
	/// than any editor a scenario builds. The words are real words rather than one unbroken run
	/// of characters, because what the WordWrap requirement is about is where a line is allowed
	/// to break.
	/// </summary>
	public static string LongLine => LongLineText.Value;

	/// <summary>
	/// Five hundred numbered lines, generated rather than written out: a document far taller
	/// than any editor a scenario builds, so that only some of its lines can be drawn at once.
	/// </summary>
	public static string FiveHundredLines => FiveHundredLinesText.Value;

	/// <summary>The sample names a feature file may write, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names
	{
		get
		{
			var names = new List<string>(Samples.Keys);
			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names;
		}
	}

	private static Dictionary<string, Func<string>> Samples { get; } =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["three lines"] = () => ThreeLines,
			["a C# snippet"] = () => CSharpSnippet,
			["repeated words"] = () => RepeatedWords,
			["one long line"] = () => LongLine,
			["five hundred lines"] = () => FiveHundredLines,
		};

	/// <summary>The document a feature file asked for by name.</summary>
	/// <param name="name">The sample name, as a feature file writes it.</param>
	/// <returns>The text of the sample.</returns>
	/// <exception cref="NotSupportedException">There is no sample of that name.</exception>
	public static string Named(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return Samples.TryGetValue(name.Trim().Trim('"'), out var sample)
			? sample()
			: throw new NotSupportedException(
				$"There is no sample document called \"{name}\". The samples are: "
				+ string.Join(", ", Names) + ".");
	}

	/// <summary>
	/// Turns the two characters a feature file writes for a line break into a real one. A
	/// quoted Gherkin value cannot hold a line break, and a document a scenario is about
	/// usually has several.
	/// </summary>
	/// <param name="text">The text as the feature file wrote it.</param>
	/// <returns>The text with every escape replaced by a line break.</returns>
	public static string Unescape(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return text.Replace(LineBreakEscape, "\n", StringComparison.Ordinal);
	}

	/// <summary>
	/// Turns real line breaks back into the escape a feature file writes, so that a failure
	/// message about a multi-line document stays on one line.
	/// </summary>
	/// <param name="text">The text as the document holds it.</param>
	/// <returns>The text with every line break replaced by the escape.</returns>
	public static string Escape(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return text.Replace("\r\n", LineBreakEscape, StringComparison.Ordinal)
			.Replace("\n", LineBreakEscape, StringComparison.Ordinal);
	}

	private static string BuildLongLine() => string.Join(" ", Enumerable.Repeat("wrap", 80));

	private static string BuildFiveHundredLines() => string.Join("\n",
		Enumerable.Range(1, 500).Select(number =>
			"line " + number.ToString(CultureInfo.InvariantCulture)));
}
