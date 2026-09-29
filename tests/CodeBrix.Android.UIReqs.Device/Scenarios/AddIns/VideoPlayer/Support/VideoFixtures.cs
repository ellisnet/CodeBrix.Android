using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace CodeBrix.Platform.UI.AddIn.VideoPlayer.UIReqs.Support;

/// <summary>
/// The clips the scenarios play, and where they are on disk. A video fixture cannot be a C#
/// literal the way a piece of markup can: the player opens a CONTAINER by name and reads it with
/// its own demuxer, so the three clips are real files that travel beside the executable
/// (Assets/, copied by the project, linked into the twin by the shared targets).
/// <para>
/// Every clip is two seconds of one flat colour changing to another - red for the first second,
/// blue for the second - in an uncompressed Matroska track, which the playback engine's built-in
/// decoder reads with no codec package and no native library. That is what makes "the picture is
/// red, and then it is blue" a requirement a person can confirm by looking at the frame.
/// </para>
/// </summary>
public static class VideoFixtures
{
	/// <summary>The two-second clip with no audio track at all: the default for every scenario.</summary>
	/// <remarks>
	/// 128 x 96 at 10 frames a second. No audio track means no output device is opened, which is
	/// one moving part fewer per scenario - the reason the video-only clip is the default rather
	/// than any rule about sound.
	/// </remarks>
	public const string TwoColour = "twocolour_raw_videoonly.mkv";

	/// <summary>The same clip with a silent Vorbis soundtrack, for the scenarios about sound.</summary>
	public const string TwoColourWithSound = "twocolour_raw.mkv";

	/// <summary>The same two seconds authored 96 x 128, so a landscape cell has to letterbox it.</summary>
	public const string Portrait = "portrait_raw.mkv";

	/// <summary>The folder the clips are copied to, beside the test executable.</summary>
	// ANDROID PORT (b): an APK has no folder beside its assemblies. The same Assets/ clips are EmbeddedResources of the
	// scenario app (logical names "VideoPlayerAssets/<file>") and are written under the app's cache folder on first use.
	public static string Folder { get; } = AndroidClips();

	/// <summary>
	/// The absolute path a scenario's clip name stands for. The name is NOT checked here: a
	/// scenario about a source that names no file has to be able to write one.
	/// </summary>
	/// <param name="clip">The file name of the clip, as a feature file writes it.</param>
	/// <returns>The absolute path.</returns>
	public static string PathOf(string clip)
	{
		ArgumentException.ThrowIfNullOrEmpty(clip);

		return Path.Combine(Folder, clip.Trim().Trim('"'));
	}

	/// <summary>
	/// The absolute path of a clip that must be there, with a message naming the folder when it
	/// is not - which is what a build that stopped copying the assets looks like.
	/// </summary>
	/// <param name="clip">The file name of the clip.</param>
	/// <returns>The absolute path.</returns>
	/// <exception cref="FileNotFoundException">The clip is not beside the executable.</exception>
	public static string Require(string clip)
	{
		var path = PathOf(clip);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException(string.Create(CultureInfo.InvariantCulture,
				$"The clip \"{clip}\" is not beside the scenarios. It should have been copied to {Folder}."),
				path);
		}

		return path;
	}

	// ANDROID PORT (b): writes the embedded clips once per process and answers their folder.
	private static string AndroidClips()
	{
		var folder = Path.Combine(Path.GetTempPath(), "VideoPlayerUIReqs", "Assets");
		Directory.CreateDirectory(folder);
		var assembly = typeof(VideoFixtures).Assembly;
		foreach (var name in assembly.GetManifestResourceNames())
		{
			if (!name.StartsWith("VideoPlayerAssets/", StringComparison.Ordinal))
			{
				continue;
			}

			using var resource = assembly.GetManifestResourceStream(name)!;
			using var file = File.Create(Path.Combine(folder, name["VideoPlayerAssets/".Length..]));
			resource.CopyTo(file);
		}

		return folder;
	}

	// ANDROID PORT (b): AssemblyFolder() (the folder of the assembly) removed - an APK has none; see AndroidClips().
}
