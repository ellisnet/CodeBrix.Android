// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/VideoComposingEventArgs.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim: the same public type in the same namespace.

#nullable enable

// Ported from CodeBrix.VideoPlayback.Skia (commit a3f3051, MIT, same author) on 2026-08-30;
// compiled against the Platform family's SkiaSharp.

using System;
using CodeBrix.VideoPlayback.Rendering;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia; //was previously: CodeBrix.VideoPlayback.Skia.Composition;

/// <summary>
/// Hands the composition surface to an application that would rather draw over the video with an
/// event handler than with an <see cref="IVideoLayer"/>.
/// </summary>
/// <remarks>
/// <see cref="VideoPlayer.Composing"/> is raised after the video base layer and every registered
/// layer have been drawn, and before the picture is presented. The canvas's state is saved and
/// restored around the call, so a handler may transform or clip it freely. Neither the canvas nor
/// these arguments outlive the call.
/// </remarks>
public sealed class VideoComposingEventArgs : EventArgs
{
	/// <summary>Creates the arguments.</summary>
	/// <param name="canvas">The composition surface's canvas.</param>
	/// <param name="context">Where the video is, which frame it is, and what composed it.</param>
	public VideoComposingEventArgs(SKCanvas canvas, VideoCompositionContext context)
	{
		Canvas = canvas;
		Context = context;
	}

	/// <summary>
	/// The composition surface's canvas, in video pixels. Valid only for the duration of the event.
	/// </summary>
	public SKCanvas Canvas { get; }

	/// <summary>Where the video is, which frame it is, and what composed it.</summary>
	public VideoCompositionContext Context { get; }
}
