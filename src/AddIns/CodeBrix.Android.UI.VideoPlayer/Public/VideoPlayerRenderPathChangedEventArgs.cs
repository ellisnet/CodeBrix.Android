// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/VideoPlayerRenderPathChangedEventArgs.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim: the same public type in the same namespace.

#nullable enable

using System;
using CodeBrix.VideoPlayback.Rendering;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia;

/// <summary>
/// Event args for <see cref="VideoPlayer.RenderPathChanged"/>: which render path settled, and
/// whether the configured effect chain is being applied on it.
/// </summary>
public sealed class VideoPlayerRenderPathChangedEventArgs : EventArgs
{
	internal VideoPlayerRenderPathChangedEventArgs(VideoRenderBackend activeRenderPath, bool effectsActive)
	{
		ActiveRenderPath = activeRenderPath;
		EffectsActive = effectsActive;
	}

	/// <summary>Which render path is now running.</summary>
	public VideoRenderBackend ActiveRenderPath { get; }

	/// <summary>Whether the configured effects are actually being applied on it.</summary>
	public bool EffectsActive { get; }
}
