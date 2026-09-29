// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/VideoRectangles.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim.

#nullable enable

// Ported from CodeBrix.VideoPlayback.Skia (commit a3f3051, MIT, same author) on 2026-08-30;
// compiled against the Platform family's SkiaSharp.

using CodeBrix.VideoPlayback.Rendering;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal; //was previously: CodeBrix.VideoPlayback.Skia;

/// <summary>
/// Converts between the playback engine's drawing-free <see cref="VideoRectangle"/> and SkiaSharp's
/// <see cref="SKRect"/>.
/// </summary>
/// <remarks>
/// The playback engine says where a picture goes without naming a drawing library, so that the same
/// geometry serves every presenter in the family. This is the two-line bridge at the edge of this
/// one. It is internal because the add-in's own public surface never hands a
/// <see cref="VideoRectangle"/> to an application except inside a
/// <see cref="VideoCompositionContext"/>, and an application that wants to convert one can call
/// SkiaSharp's own constructor with the four edges.
/// </remarks>
internal static class VideoRectangles
{
	/// <summary>Turns a playback rectangle into a SkiaSharp one.</summary>
	/// <param name="rectangle">The rectangle to convert.</param>
	/// <returns>The same four edges, as an <see cref="SKRect"/>.</returns>
	internal static SKRect ToSKRect(this VideoRectangle rectangle) =>
		new SKRect(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

	/// <summary>Turns a SkiaSharp rectangle into a playback one.</summary>
	/// <param name="rectangle">The rectangle to convert.</param>
	/// <returns>The same four edges, as a <see cref="VideoRectangle"/>.</returns>
	internal static VideoRectangle FromSKRect(SKRect rectangle) =>
		new VideoRectangle(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

	/// <summary>Turns a SkiaSharp rectangle into a playback one.</summary>
	/// <param name="rectangle">The rectangle to convert.</param>
	/// <returns>The same four edges, as a <see cref="VideoRectangle"/>.</returns>
	/// <remarks>The extension-method spelling of <see cref="FromSKRect"/>, for fluent code.</remarks>
	internal static VideoRectangle ToVideoRectangle(this SKRect rectangle) => FromSKRect(rectangle);
}
