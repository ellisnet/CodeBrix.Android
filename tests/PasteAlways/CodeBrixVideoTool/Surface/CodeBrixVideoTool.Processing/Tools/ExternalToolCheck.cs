using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Tools;

/// <summary>
/// Checks that FFmpeg, FFprobe and FFmpeg's SVT-AV1 encoder are there, and says which is missing.
/// </summary>
/// <remarks>
/// <para>
/// All three questions go through CodeBrix.VideoProcessing, exactly as the real work does: it starts
/// <c>ffmpeg -version</c> and <c>ffprobe -version</c> from <c>GlobalFFOptions.BinaryFolder</c> or the PATH
/// (adding <c>.exe</c> on Windows only), and reads FFmpeg's own encoder list. Nothing here is specific to one
/// operating system, and the answer is the one the conversions themselves would get.
/// </para>
/// <para>
/// FFprobe is checked because this application uses it: <see cref="Probing.MediaProbe" /> probes every
/// non-<c>.cbv</c> file with it, and the sidecar extractor reads a source's chapters and captions with it.
/// SVT-AV1 is checked because every MKV, WebM and <c>.cbv</c> destination is authored with it.
/// </para>
/// </remarks>
public sealed class ExternalToolCheck : IExternalToolCheck
{
    internal const string SvtAv1EncoderName = "libsvtav1";

    internal const string FFmpegMissingMessage =
        "FFmpeg is required for this application, but cannot be found on this machine. Converting video, and "
        + "opening any file that is not a .cbv, run through the 'ffmpeg' and 'ffprobe' commands. Install FFmpeg "
        + "so that 'ffmpeg' runs from a terminal - this application looks for it on its PATH - and then restart "
        + "the application.";

    internal const string FFprobeMissingMessage =
        "FFprobe is required for this application, but cannot be found on this machine. FFmpeg was found, but "
        + "'ffprobe' - which this application uses to look inside every file it opens that is not a .cbv - was "
        + "not. FFprobe normally comes with FFmpeg: install a complete FFmpeg so that 'ffprobe' also runs from a "
        + "terminal, and then restart the application.";

    internal const string SvtAv1MissingMessage =
        "The SVT-AV1 encoder is required for this application's conversions, but the FFmpeg on this machine does "
        + "not include it (its encoder list has no 'libsvtav1'). Files can still be opened and played, and "
        + "exporting to .mp4 still works, but importing, transcoding or exporting to MKV, WebM or CodeBrix .cbv "
        + "will fail. Install an FFmpeg build that includes SVT-AV1, and then restart the application.";

    private readonly Func<bool> ffmpegRuns;
    private readonly Func<bool> ffprobeRuns;
    private readonly Func<string, bool?> hasEncoder;

    /// <summary>Creates a check against the FFmpeg installed on this machine.</summary>
    public ExternalToolCheck()
        : this(FFmpegRuns, FFprobeRuns, HasEncoder)
    {
    }

    /// <summary>Creates a check that asks its three questions of the functions given, for tests.</summary>
    /// <param name="ffmpegRuns">Whether ffmpeg runs.</param>
    /// <param name="ffprobeRuns">Whether ffprobe runs.</param>
    /// <param name="hasEncoder">Whether ffmpeg has the named encoder, or null when that could not be found out.</param>
    internal ExternalToolCheck(Func<bool> ffmpegRuns, Func<bool> ffprobeRuns, Func<string, bool?> hasEncoder)
    {
        this.ffmpegRuns = ffmpegRuns;
        this.ffprobeRuns = ffprobeRuns;
        this.hasEncoder = hasEncoder;
    }

    /// <inheritdoc />
    public Task<string> FindProblemAsync(CancellationToken cancellationToken)
    {
        //Each step starts a child process and waits for it, so none of it belongs on the UI thread.
        return Task.Run(() => FindProblem(cancellationToken), cancellationToken);
    }

    internal string FindProblem(CancellationToken cancellationToken)
    {
        if (!ffmpegRuns())
        {
            return FFmpegMissingMessage;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!ffprobeRuns())
        {
            return FFprobeMissingMessage;
        }

        cancellationToken.ThrowIfCancellationRequested();

        //Only a definite "no" is reported. An encoder list that could not be read says nothing either way, and a
        //warning that might be wrong is worse than none: the conversion itself will say what went wrong.
        return hasEncoder(SvtAv1EncoderName) == false ? SvtAv1MissingMessage : null;
    }

    private static bool FFmpegRuns()
    {
        try
        {
            FFMpegHelper.VerifyFFMpegExists(GlobalFFOptions.Current);
            return true;
        }
        catch (Exception)
        {
            //Not found, not runnable, or not allowed to start: all of them mean the application cannot use it.
            return false;
        }
    }

    private static bool FFprobeRuns()
    {
        try
        {
            FFProbeHelper.VerifyFFProbeExists(GlobalFFOptions.Current);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool? HasEncoder(string encoderName)
    {
        try
        {
            return FFMpeg.TryGetCodec(encoderName, out Codec codec) && codec is not null && codec.EncodingSupported;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
