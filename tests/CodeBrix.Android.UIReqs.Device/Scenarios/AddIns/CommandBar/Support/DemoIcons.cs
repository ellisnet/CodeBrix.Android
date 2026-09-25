using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CodeBrix.Platform.UI.AddIn.CommandBar.UIReqs.Support;

/// <summary>
/// The artwork the tool bar scenarios put on their buttons. Everything a scenario can express as
/// a document written inline is a C# string here rather than a file, so that what the scenario
/// expects to see on the panel and what the artwork holds cannot drift apart.
/// <para>
/// Every shape is a plain rectangle with square corners and no stroke, which is what makes an
/// assertion about the icon's colour a statement about the TINT rather than about how a curve was
/// antialiased. The two artworks a scenario names to prove a theme swap are the exception: an
/// icon source picks its dark artwork by URI and there is no "dark markup", so those two live in
/// <c>Assets/</c> and are reached as file URIs beside the running assembly.
/// </para>
/// </summary>
public static class DemoIcons
{
	/// <summary>A full square painted in <c>currentColor</c>: the artwork a tint is meant to reach.</summary>
	public const string Block = "block";

	/// <summary>A full square that states its own colour, which a tint must leave alone.</summary>
	public const string Stated = "stated";

	/// <summary>A small centred square in <c>currentColor</c>, for a button whose background matters.</summary>
	public const string Dot = "dot";

	/// <summary>The colour <see cref="Stated"/> paints itself, whatever a button's tint says.</summary>
	public const string StatedColor = "#00AA00";

	/// <summary>The file name of the artwork the light theme shows.</summary>
	public const string LightArtworkFile = "icon-light.svg";

	/// <summary>The file name of the artwork the dark theme shows.</summary>
	public const string DarkArtworkFile = "icon-dark.svg";

	private static readonly Dictionary<string, string> Documents = BuildDocuments();

	/// <summary>The names a feature file may write in an Icon column, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names
	{
		get
		{
			var names = new List<string>(Documents.Keys);
			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names;
		}
	}

	/// <summary>The folder the URI-loaded artwork was copied into beside this assembly.</summary>
	// ANDROID PORT (b): an APK has no folder beside its assemblies, so the two artwork files of Assets/ (copied
	// verbatim next to this folder) are written, byte for byte, into a folder of the app's temporary directory the
	// first time the scenarios ask for it; the icons then load them as file URIs exactly as on the desktop.
	public static string AssetFolder { get; } = WriteArtwork(Path.Combine(Path.GetTempPath(), "uireqs-commandbar", "Assets"));

	private static string WriteArtwork(string folder)
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, LightArtworkFile), "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\"><rect x=\"0\" y=\"0\" width=\"24\" height=\"24\" fill=\"#0000FF\"/></svg>");
		File.WriteAllText(Path.Combine(folder, DarkArtworkFile), "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\"><rect x=\"0\" y=\"0\" width=\"24\" height=\"24\" fill=\"#FFA500\"/></svg>");
		return folder;
	}

	/// <summary>The SVG document a feature file named.</summary>
	/// <param name="name">One of <see cref="Names"/>.</param>
	/// <returns>The document, as an SVG parser reads it.</returns>
	/// <exception cref="NotSupportedException">No fixture goes by that name.</exception>
	public static string Document(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return Documents.TryGetValue(name, out var document)
			? document
			: throw new NotSupportedException(
				$"There is no icon fixture called \"{name}\". Write one of: {string.Join(", ", Names)}.");
	}

	/// <summary>The URI of one of the two artworks the theme scenario swaps between.</summary>
	/// <param name="fileName">
	/// <see cref="LightArtworkFile"/> or <see cref="DarkArtworkFile"/>.
	/// </param>
	/// <returns>A file URI beside the running assembly.</returns>
	/// <exception cref="FileNotFoundException">The build did not copy the artwork.</exception>
	public static Uri ArtworkUri(string fileName)
	{
		ArgumentException.ThrowIfNullOrEmpty(fileName);

		var path = Path.Combine(AssetFolder, fileName);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException(
				$"The artwork \"{fileName}\" is not beside the scenarios, so no icon can load it. "
				+ "The Landscape project's Assets folder is copied to the output of both projects "
				+ $"of the pair by UIReqs.Common.targets; it was looked for in \"{AssetFolder}\".",
				path);
		}

		return new Uri(path);
	}

	/// <summary>
	/// One rectangle, written the way this project's fixtures are written: no stroke, integer
	/// coordinates, square corners, and one flat colour.
	/// </summary>
	/// <param name="inset">How far in from the 24 by 24 edge the rectangle starts.</param>
	/// <param name="ink">The fill, either <c>currentColor</c> or a stated colour.</param>
	/// <returns>The SVG document.</returns>
	public static string Svg(int inset, string ink)
	{
		ArgumentException.ThrowIfNullOrEmpty(ink);

		var side = 24 - (2 * inset);
		return string.Create(CultureInfo.InvariantCulture,
			$"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\">"
			+ $"<rect x=\"{inset}\" y=\"{inset}\" width=\"{side}\" height=\"{side}\" fill=\"{ink}\"/></svg>");
	}

	private static Dictionary<string, string> BuildDocuments()
	{
		// "currentColor" is the hook a tint resolves through: the add-in hands the parser a
		// stylesheet that sets `color`, and only artwork that asked for currentColor follows it.
		var documents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Block] = Svg(0, "currentColor"),
			[Stated] = Svg(0, StatedColor),
			[Dot] = Svg(6, "currentColor"),
		};

		return documents;
	}
}
