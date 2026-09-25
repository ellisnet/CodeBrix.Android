using CodeBrix.VideoPlayback.Captions;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodeBrixVideoTool.Processing.Containers;

/// <summary>
/// Writes a <see cref="CaptionTrack" /> out as a WebVTT file.
/// </summary>
/// <remarks>
/// The playback core reads WebVTT and formats its timestamps, but publishes no writer, so this
/// application brings one. Cue identifiers and cue settings are written back out untouched, because
/// both of the destinations that can carry them - the bespoke container and a Matroska or WebM
/// subtitle track copied rather than re-encoded - keep them.
/// </remarks>
public static class WebVttFile
{
    /// <summary>Writes a caption track to a WebVTT file.</summary>
    /// <param name="track">The track to write.</param>
    /// <param name="path">The file to create, overwriting anything already there.</param>
    /// <exception cref="ArgumentNullException">The track is null.</exception>
    public static void Write(CaptionTrack track, string path)
    {
        ArgumentNullException.ThrowIfNull(track);

        var text = new StringBuilder();
        text.Append("WEBVTT\n\n");

        foreach (var cue in track.Cues)
        {
            if (!string.IsNullOrEmpty(cue.Identifier))
            {
                text.Append(cue.Identifier).Append('\n');
            }

            text.Append(CaptionFiles.FormatWebVttTime(cue.Start))
                .Append(" --> ")
                .Append(CaptionFiles.FormatWebVttTime(cue.End));

            if (!string.IsNullOrEmpty(cue.Settings))
            {
                text.Append(' ').Append(cue.Settings);
            }

            text.Append('\n').Append(cue.Text.Replace("\r\n", "\n")).Append("\n\n");
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    /// <summary>
    /// A file name for one caption track that says which track it is, and cannot collide with
    /// another track of the same language.
    /// </summary>
    /// <param name="index">The track's position in the source, from zero.</param>
    /// <param name="language">The track's BCP 47 language tag.</param>
    /// <returns>A bare file name ending in <c>.vtt</c>.</returns>
    public static string FileNameFor(int index, string language)
    {
        var tag = string.IsNullOrWhiteSpace(language) ? "und" : language;
        var safe = new StringBuilder(tag.Length);
        foreach (var character in tag)
        {
            safe.Append(char.IsLetterOrDigit(character) || character == '-' ? character : '_');
        }

        return string.Create(CultureInfo.InvariantCulture, $"captions.{index}.{safe}.vtt");
    }
}
