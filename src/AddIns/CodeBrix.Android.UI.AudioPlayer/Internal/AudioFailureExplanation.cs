// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.AudioPlayer.Skia/Skia/AudioFailureExplanation.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim; namespace kept (the Core's internals are granted to this assembly).

#nullable enable

using System;
using System.IO;
using CodeBrix.Audio.Codecs;

namespace CodeBrix.Platform.UI.AudioPlayer.Skia.Internal;

/// <summary>
/// Turns a load failure into something the person reading it can act on.
/// </summary>
/// <remarks>
/// One failure needs this badly. Ogg is a container rather than a codec, and the audio engine
/// reports the format identifier "ogg" for everything inside one - so an .opus file played by an
/// application that has not registered the Opus codec fails with "No registered and working codec
/// factory found for decoding format 'ogg'", which names neither the format the user chose nor the
/// thing they have to do about it.
/// </remarks>
internal static class AudioFailureExplanation
{
	private const string OpusAdvice =
		" This is an Ogg Opus file. Opus is BSD-3-Clause rather than MIT, so it is not part of " +
		"CodeBrix.Audio and this AddIn neither references nor needs it: reference the " +
		"CodeBrix.Audio.Opus package from the application and call CodeBrixAudioOpus.Register() " +
		"once at start-up, and .opus files play here like any other format.";

	/// <summary>
	/// Returns <paramref name="message"/>, with an explanation appended when the source turns out
	/// to be a format that needs something of the application.
	/// </summary>
	/// <param name="message">The failure message as it stands.</param>
	/// <param name="source">The source that failed to load, in any form the resolver accepts.</param>
	/// <remarks>
	/// Only ever called on the failure path, so re-opening the source to look at it costs nothing
	/// that matters. Any failure while looking leaves the message exactly as it was - an
	/// explanation is a courtesy, and must never replace the real error or throw over it.
	/// </remarks>
	public static string Amend(string message, string source)
	{
		try
		{
			return IdentifyOggCodec(source) == OggCodec.Opus ? message + OpusAdvice : message;
		}
		catch (Exception)
		{
			return message;
		}
	}

	private static OggCodec IdentifyOggCodec(string source)
	{
		var (filePath, stream) = AudioSourceResolver.Resolve(source);

		if (filePath is not null)
		{
			if (!File.Exists(filePath))
			{
				// A missing file is its own, already clear, failure.
				return OggCodec.NotOgg;
			}

			using var file = File.OpenRead(filePath);
			return OggCodecSniffer.Identify(file);
		}

		using (stream)
		{
			if (stream!.CanSeek)
			{
				return OggCodecSniffer.Identify(stream);
			}

			// The sniffer needs to seek; an embedded resource stream that cannot is cheap to copy,
			// because only its first page is read.
			using var buffer = new MemoryStream();
			stream.CopyTo(buffer);
			buffer.Position = 0;
			return OggCodecSniffer.Identify(buffer);
		}
	}
}
