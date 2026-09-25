using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Windows.Storage.Streams;

namespace CodeBrix.Platform.UI.AddIn.Svg.UIReqs.Support;

/// <summary>
/// The SVG documents the scenarios draw. They are written here as text rather than carried as
/// files, so that what a scenario expects to see on the panel and what the document says cannot
/// drift apart, and so that the Portrait twin has nothing to link but source.
/// <para>
/// Every one of them is flat colour on integer boundaries: an anti-aliased edge or a gradient
/// would make "the leftmost 190 pixels are uniformly Red" a claim about the renderer's edge
/// handling rather than about where the document put its colours.
/// </para>
/// </summary>
public static class TestSvg
{
	/// <summary>
	/// The name of the two-colour document: 40 by 20, its left half Red and its right half Blue.
	/// It is the SVG twin of the picture the core suite's Image scenarios use, so the Stretch
	/// claims here can be read beside those.
	/// </summary>
	public const string TwoHalves = "TwoHalves";

	/// <summary>
	/// The name of the tall document: 20 by 40, its top half Green and its bottom half Blue.
	/// Its shape is the other way up from <see cref="TwoHalves"/> and it shares no colour with
	/// its left half, so a scenario that swaps one document for the other can say both that the
	/// new picture arrived and that the old one is gone.
	/// </summary>
	public const string TallGreen = "TallGreen";

	/// <summary>
	/// The name of the themed document: a 24 by 24 square painted with <c>currentColor</c>, so
	/// the colour it comes out in is decided by the stylesheet rather than by the document.
	/// </summary>
	public const string CurrentColor = "CurrentColor";

	/// <summary>
	/// The name of the document no parser can read: an opening tag and nothing that closes it.
	/// </summary>
	public const string Malformed = "Malformed";

	private const string TwoHalvesDocument =
		"""
		<svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" viewBox="0 0 40 20">
			<rect x="0" y="0" width="20" height="20" fill="#FF0000" />
			<rect x="20" y="0" width="20" height="20" fill="#0000FF" />
		</svg>
		""";

	private const string TallGreenDocument =
		"""
		<svg xmlns="http://www.w3.org/2000/svg" width="20" height="40" viewBox="0 0 20 40">
			<rect x="0" y="0" width="20" height="20" fill="#008000" />
			<rect x="0" y="20" width="20" height="20" fill="#0000FF" />
		</svg>
		""";

	private const string CurrentColorDocument =
		"""
		<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
			<rect x="0" y="0" width="24" height="24" fill="currentColor" />
		</svg>
		""";

	// Not a document at all: an element that is never closed. A parser that reads this and
	// reports success would be a parser that draws nothing and says nothing about it.
	private const string MalformedDocument = "<svg><rect";

	private static readonly Dictionary<string, string> Documents = new(StringComparer.OrdinalIgnoreCase)
	{
		[TwoHalves] = TwoHalvesDocument,
		[TallGreen] = TallGreenDocument,
		[CurrentColor] = CurrentColorDocument,
		[Malformed] = MalformedDocument,
	};

	/// <summary>The document names a feature file may ask for, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names
	{
		get
		{
			var names = new List<string>(Documents.Keys);
			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names;
		}
	}

	/// <summary>The text of one of the documents.</summary>
	/// <param name="name">The document's name, as a feature file writes it.</param>
	/// <returns>The SVG text.</returns>
	/// <exception cref="NotSupportedException">No document of that name is carried here.</exception>
	public static string Document(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return Documents.TryGetValue(name.Trim().Trim('"'), out var document)
			? document
			: throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture,
				$"There is no SVG document called \"{name}\". This project carries: {string.Join(", ", Names)}."));
	}

	/// <summary>The bytes of one of the documents, as a file of it would hold them.</summary>
	/// <param name="name">The document's name.</param>
	/// <returns>The UTF-8 bytes.</returns>
	public static byte[] Bytes(string name) => Encoding.UTF8.GetBytes(Document(name));

	/// <summary>
	/// One of the documents as the stream an application hands to
	/// <c>SvgImageSource.SetSourceAsync</c>. The stream is deliberately NOT disposed by the
	/// caller: the source keeps a clone of it, and the whole thing is managed memory that the
	/// collector reclaims once the source lets go.
	/// </summary>
	/// <param name="name">The document's name.</param>
	/// <returns>A stream positioned at the start of the document.</returns>
	public static IRandomAccessStream OpenStream(string name)
	{
		var bytes = Bytes(name);
		var stream = new InMemoryRandomAccessStream();

		// AsStream hands back the stream's own buffer rather than a copy, so this is a write
		// into the very memory the source will clone.
		var writer = stream.AsStream();
		writer.Write(bytes, 0, bytes.Length);
		writer.Flush();
		stream.Seek(0);
		return stream;
	}
}
