using System;
using System.Globalization;
using System.IO;
using CodeBrix.Audio.Midi;

namespace CodeBrix.Platform.UI.AddIn.AudioPlayer.UIReqs.Support;

/// <summary>
/// The silent files these scenarios play, and where they are on disk while a run is happening.
/// <para>
/// Every fixture is digital silence, so a run makes no sound even before the players are turned
/// down to nothing: the four audio files are all-zero samples, and the one-region SFZ instrument
/// plays one of them. They ship beside the executable as assets, because an <c>.sfz</c>
/// names its samples as separate files BESIDE it and a MIDI instrument therefore cannot be a
/// stream; the MIDI sequence itself is written here, on first use, into the test output folder.
/// Nothing is written anywhere else, and nothing is downloaded.
/// </para>
/// <para>
/// What ships beside the executable: <c>silence2s.wav</c> and <c>silence_long.wav</c> (2 s and 10 s
/// of 22.05 kHz mono zeros), <c>silence2s.ogg</c> (the one compressed format), and
/// <c>silent.sfz</c> with <c>Samples/silence.wav</c> beside it. That rate is not the smallest one an
/// audio player can read: the audio output is opened once for the whole PROCESS, at the rate of the
/// first thing played through it, and the MIDI synthesizer refuses an output below 16 kHz - so one
/// 8 kHz file would stop every instrument in the same run from loading.
/// </para>
/// </summary>
public static class AudioFixtures
{
	/// <summary>The two-second silent WAV, the plain "a file is loaded" fixture.</summary>
	public const string ShortWave = "silence2s.wav";

	/// <summary>The generated Standard MIDI File the MidiPlayer scenarios play.</summary>
	public const string MidiSequence = "tiny.mid";

	/// <summary>Ticks per quarter note in the generated sequence.</summary>
	public const int TicksPerQuarterNote = 480;

	/// <summary>Microseconds per quarter note in the generated sequence, which is 120 beats a minute.</summary>
	public const int MicrosecondsPerQuarterNote = 500_000;

	/// <summary>How many quarter notes the generated sequence holds, one to a beat.</summary>
	public const int NoteCount = 64;

	private static readonly object GenerationLock = new();

	private static string? _midiPath;

	/// <summary>Where the fixtures that ship beside the executable are.</summary>
	// ANDROID PORT (b): an APK has no folder beside its assemblies. The same Assets/ files are EmbeddedResources of the
	// scenario app (logical names "AudioPlayerAssets/<path>") and are written, tree and all, under the app's cache folder on
	// first use; an .sfz still finds its Samples/ folder beside it.
	public static string AssetsFolder => AndroidAssets.Value;

	/// <summary>Where a fixture this assembly writes for itself goes: the test output folder.</summary>
	// ANDROID PORT (b): the app's cache folder (the assemblies' folder is not the app's to write).
	public static string GeneratedFolder => Path.Combine(Path.GetTempPath(), "AudioPlayerUIReqs", "GeneratedFixtures");

	// ANDROID PORT (b): the embedded Assets/ tree, written once per process.
	private static readonly Lazy<string> AndroidAssets = new(() =>
	{
		var folder = Path.Combine(Path.GetTempPath(), "AudioPlayerUIReqs", "Assets");
		var assembly = typeof(AudioFixtures).Assembly;
		foreach (var name in assembly.GetManifestResourceNames())
		{
			if (!name.StartsWith("AudioPlayerAssets/", StringComparison.Ordinal))
			{
				continue;
			}

			var path = Path.Combine(folder, name["AudioPlayerAssets/".Length..]);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			using var resource = assembly.GetManifestResourceStream(name)!;
			using var file = File.Create(path);
			resource.CopyTo(file);
		}

		return folder;
	});

	/// <summary>
	/// Turns the name a feature file writes into the path the player is given. A generated fixture
	/// wins over an asset of the same name; anything else is read from the assets folder, whether
	/// or not it is there - a scenario about a file that is NOT there needs a path to a file that
	/// is not there.
	/// </summary>
	/// <param name="name">The fixture's file name, as a feature file writes it.</param>
	/// <returns>The absolute path to hand the player.</returns>
	public static string Resolve(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (string.Equals(name, MidiSequence, StringComparison.OrdinalIgnoreCase))
		{
			return MidiFilePath();
		}

		var generated = Path.Combine(GeneratedFolder, name);
		return File.Exists(generated) ? generated : Path.Combine(AssetsFolder, name);
	}

	/// <summary>
	/// Writes the MIDI sequence the MidiPlayer scenarios play, once per process, and answers where
	/// it is. It is <see cref="NoteCount"/> quarter notes at 120 beats a minute - long enough for a
	/// scenario to watch the musical clock advance while notes are still sounding - and it is
	/// generated rather than committed because a Standard MIDI File is a few lines of code and a
	/// committed binary nobody can read is not.
	/// </summary>
	/// <returns>The absolute path of the generated file.</returns>
	public static string MidiFilePath()
	{
		lock (GenerationLock)
		{
			if (_midiPath is not null)
			{
				return _midiPath;
			}

			Directory.CreateDirectory(GeneratedFolder);
			var path = Path.Combine(GeneratedFolder, MidiSequence);

			var events = new MidiEventCollection(midiFileType: 0, deltaTicksPerQuarterNote: TicksPerQuarterNote);
			var track = events.AddTrack();
			track.Add(new TempoEvent(MicrosecondsPerQuarterNote, absoluteTime: 0));

			for (var note = 0; note < NoteCount; note++)
			{
				// One note to a beat, walking up a scale so that no two neighbours are the same
				// note number - a synthesizer may fold a repeated note into the voice already
				// sounding, and these scenarios count voices.
				track.Add(new NoteOnEvent(
					absoluteTime: (long) note * TicksPerQuarterNote,
					channel: 1,
					noteNumber: 48 + (note % 24),
					velocity: 100,
					duration: TicksPerQuarterNote));
			}

			// Required before Export: it is what adds the note-offs and the end-of-track marker.
			events.PrepareForExport();
			MidiFile.Export(path, events);

			_midiPath = path;
			return path;
		}
	}

	/// <summary>A one-line description of where the fixtures are, for a failure message.</summary>
	/// <returns>The two folders the fixtures are read from.</returns>
	public static string Describe() => string.Create(CultureInfo.InvariantCulture,
		$"assets: {AssetsFolder}; generated: {GeneratedFolder}");
}
