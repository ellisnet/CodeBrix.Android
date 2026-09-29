// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/VideoFailureExplanation.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim.

#nullable enable

using System;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;

/// <summary>
/// Turns a load failure into something the person reading it can act on.
/// </summary>
/// <remarks>
/// <para>
/// Two failures need this, and one of them is the most likely failure this AddIn will ever
/// produce. Every video this family authors carries AV1 video (the one exception a player can meet
/// is an uncompressed test track, which the playback core decodes itself), and AV1 decoding is BSD-2-Clause
/// rather than Apache-2.0, so it ships as a separate package that the APPLICATION references and
/// registers: until it does, every single file fails. Ogg Opus audio is the same story a size
/// smaller (BSD-3-Clause, a separate package, a separate registration call).
/// </para>
/// <para>
/// The playback engine's own message already names the piece and the call - "video codec 'av01'
/// has no registered decoder - reference CodeBrix.VideoPlayback.Dav1d and call
/// CodeBrixVideoPlaybackDav1d.Register()". What it does not name is the NuGet package id, which
/// is what somebody has to type. This adds exactly that, and leaves every other failure alone.
/// </para>
/// </remarks>
internal static class VideoFailureExplanation
{
	/// <summary>The text the engine uses to name the AV1 decoder package in its own message.</summary>
	private const string Dav1dMarker = "CodeBrixVideoPlaybackDav1d.Register()";

	/// <summary>The text the engine uses to name the Opus decoder package in its own message.</summary>
	private const string OpusMarker = "CodeBrixAudioOpus.Register()";

	private const string Dav1dAdvice =
		" The AV1 decoder is BSD-2-Clause rather than Apache-2.0, so it is not part of this AddIn " +
		"and this AddIn neither references nor needs it: reference the " +
		"CodeBrix.VideoPlayback.Dav1d.BsdLicenseForever package from the application and call " +
		"CodeBrixVideoPlaybackDav1d.Register() once at start-up, and AV1 files play here like any " +
		"other format.";

	private const string OpusAdvice =
		" Opus is BSD-3-Clause rather than Apache-2.0, so it is not part of this AddIn and this " +
		"AddIn neither references nor needs it: reference the CodeBrix.Audio.Opus.BsdLicenseForever " +
		"package from the application and call CodeBrixAudioOpus.Register() once at start-up, and " +
		"the soundtrack plays with the picture.";

	/// <summary>
	/// Returns <paramref name="message"/>, with an explanation appended when
	/// <paramref name="engineMessage"/> turns out to name a decoder the application has to supply.
	/// </summary>
	/// <param name="message">The failure message as it stands.</param>
	/// <param name="engineMessage">
	/// The playback engine's own message for the failure (the exception's <c>Message</c>), which is
	/// what names the missing piece. Null or empty leaves the message untouched.
	/// </param>
	/// <remarks>
	/// Only ever called on the failure path. An explanation is a courtesy and must never replace
	/// the real error, so anything unrecognized comes back exactly as it went in.
	/// </remarks>
	public static string Amend(string message, string? engineMessage)
	{
		if (string.IsNullOrEmpty(engineMessage))
		{
			return message;
		}

		if (engineMessage.Contains(Dav1dMarker, StringComparison.Ordinal))
		{
			return message + Dav1dAdvice;
		}

		if (engineMessage.Contains(OpusMarker, StringComparison.Ordinal))
		{
			return message + OpusAdvice;
		}

		return message;
	}
}
