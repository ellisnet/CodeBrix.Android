using CodeBrix.VideoPlayback;
using CodeBrix.VideoPlayback.Containers;
using CodeBrix.VideoPlayback.Containers.Cbv;
using CodeBrix.VideoPlayback.Containers.Ivf;
using CodeBrix.VideoPlayback.Containers.Ogg;
using CodeBrix.VideoPlayback.Decoding;
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using CodeBrixVideoTool.Processing.Probing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Containers;

/// <summary>
/// Turns a Mode 2 file into something FFmpeg can read.
/// </summary>
/// <remarks>
/// <para>
/// FFmpeg cannot open the bespoke CBVF container, so a Mode 2 file cannot be handed straight to a
/// conversion pass the way a Mode 1, WebM or Matroska file can. What it can do is read the streams
/// inside it, which are perfectly ordinary AV1 and Vorbis. So this demultiplexes the file with the
/// playback core's own reader, re-wraps the AV1 stream in IVF and the audio stream in Ogg with the
/// core's own <see cref="IvfWriter" /> and <see cref="OggAudioWriter" /> - the two containers the
/// authoring library itself writes when it builds a bespoke file, used here in the opposite
/// direction - and muxes those two into one Matroska file with no re-encoding at all.
/// </para>
/// <para>
/// From that point on a Mode 2 conversion is an ordinary conversion. Nothing is decoded, nothing is
/// re-encoded, and the chapters and caption cues come straight out of the header where the format
/// keeps them.
/// </para>
/// </remarks>
public sealed class Mode2Extractor
{
    /// <summary>Demultiplexes a Mode 2 file and remuxes it into an FFmpeg-readable intermediate.</summary>
    /// <param name="source">What probing found in the file.</param>
    /// <param name="workingFolder">A folder the intermediate files may be written into.</param>
    /// <param name="cancellationToken">Stops the extraction.</param>
    /// <returns>The intermediate file, the two elementary streams, and the chapters and captions.</returns>
    /// <exception cref="VideoToolProcessingException">
    /// The file is not a Mode 2 file, carries no AV1 video, or could not be re-wrapped.
    /// </exception>
    public async Task<Mode2Extraction> ExtractAsync(
        SourceMediaInfo source, string workingFolder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(workingFolder);

        var ivfPath = Path.Combine(workingFolder, "video.ivf");
        var oggPath = Path.Combine(workingFolder, "audio.ogg");
        var intermediatePath = Path.Combine(workingFolder, "intermediate.mkv");

        int videoFrames;
        var audioPackets = 0;
        var audioCodecId = string.Empty;
        var hasAudio = false;
        MediaSidecars sidecars;

        using (var reader = MediaContainers.Open(source.Path))
        {
            if (reader is not CbvReader)
            {
                throw new VideoToolProcessingException(
                    $"'{source.FileName}' is not a bespoke CodeBrix Mode 2 file, so it does not need demultiplexing.");
            }

            var video = reader.Tracks.FirstOrDefault(t => t.Kind == MediaTrackKind.Video)
                ?? throw new VideoToolProcessingException($"'{source.FileName}' carries no video track.");

            if (!string.Equals(video.CodecId, VideoCodecIds.Av1, StringComparison.OrdinalIgnoreCase))
            {
                throw new VideoToolProcessingException(
                    $"'{source.FileName}' carries '{video.CodecId}' video; only AV1 can be re-wrapped into IVF.");
            }

            var audio = reader.Tracks.FirstOrDefault(t => t.Kind == MediaTrackKind.Audio);

            using var ivf = IvfWriter.CreateAv1(ivfPath, video.Width, video.Height);
            var ogg = audio is null ? null : CreateAudioWriter(audio, oggPath, source.FileName);

            try
            {
                Demultiplex(reader, video.Id, audio?.Id ?? -1, ivf, ogg, cancellationToken,
                    out videoFrames, out audioPackets);

                ivf.Complete();
                ogg?.Complete();
            }
            finally
            {
                ogg?.Dispose();
            }

            if (videoFrames == 0)
            {
                throw new VideoToolProcessingException(
                    $"'{source.FileName}' produced no video frames, so there is nothing to convert.");
            }

            if (audio is not null)
            {
                hasAudio = audioPackets > 0;
                audioCodecId = audio.CodecId ?? string.Empty;
            }

            sidecars = SidecarExtractor.ExtractFromReader(reader, workingFolder);
        }

        await RemuxAsync(ivfPath, hasAudio ? oggPath : null, intermediatePath, cancellationToken)
            .ConfigureAwait(false);

        return new Mode2Extraction(
            intermediatePath, ivfPath, hasAudio ? oggPath : null, audioCodecId,
            videoFrames, audioPackets, sidecars);
    }

    private static OggAudioWriter CreateAudioWriter(MediaTrackInfo audio, string path, string fileName)
    {
        try
        {
            if (string.Equals(audio.CodecId, VideoCodecIds.Vorbis, StringComparison.OrdinalIgnoreCase))
            {
                return OggAudioWriter.CreateVorbis(path, audio.CodecPrivate.Span, audio.SampleRate);
            }

            if (string.Equals(audio.CodecId, VideoCodecIds.Opus, StringComparison.OrdinalIgnoreCase))
            {
                return OggAudioWriter.CreateOpus(path, audio.CodecPrivate.Span, audio.PreSkipSamples);
            }
        }
        catch (VideoPlaybackException exception)
        {
            //The core refuses codec-private data that is not the three Xiph-laced Vorbis headers or an
            //OpusHead; that is this application's "could not be re-wrapped" case, in its own words.
            throw new VideoToolProcessingException(
                $"'{fileName}' carries '{audio.CodecId}' audio whose setup data could not be re-wrapped into an Ogg stream: {exception.Message}",
                exception);
        }

        throw new VideoToolProcessingException(
            $"'{fileName}' carries '{audio.CodecId}' audio, which cannot be re-wrapped into an Ogg stream.");
    }

    private static void Demultiplex(
        IMediaContainerReader reader,
        int videoTrackId,
        int audioTrackId,
        IvfWriter ivf,
        OggAudioWriter ogg,
        CancellationToken cancellationToken,
        out int videoFrames,
        out int audioPackets)
    {
        videoFrames = 0;
        audioPackets = 0;

        //One packet of lookahead on the audio side: an Ogg granule position says where a packet
        //ENDS, and the next packet's timestamp is the most reliable statement of that.
        byte[] pendingAudio = null;
        var pendingTimestamp = TimeSpan.Zero;
        var pendingDuration = TimeSpan.Zero;

        while (reader.TryReadPacket(out var packet))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (packet.TrackId == videoTrackId)
            {
                ivf.WriteFrame(packet.Data.Span, packet.Timestamp);
                videoFrames++;
                continue;
            }

            if (ogg is null || packet.TrackId != audioTrackId)
            {
                continue;
            }

            if (pendingAudio is not null)
            {
                ogg.WritePacket(pendingAudio, EndOf(pendingTimestamp, pendingDuration, packet.Timestamp));
                audioPackets++;
            }

            //MediaPacket.Data is borrowed from the reader and is gone on the next read.
            pendingAudio = packet.Data.ToArray();
            pendingTimestamp = packet.Timestamp;
            pendingDuration = packet.Duration;
        }

        if (pendingAudio is not null && ogg is not null)
        {
            var end = pendingDuration > TimeSpan.Zero
                ? pendingTimestamp + pendingDuration
                : MaxOf(pendingTimestamp, reader.Duration);
            ogg.WritePacket(pendingAudio, end);
            audioPackets++;
        }
    }

    private static TimeSpan EndOf(TimeSpan timestamp, TimeSpan duration, TimeSpan nextTimestamp) =>
        duration > TimeSpan.Zero ? timestamp + duration : MaxOf(timestamp, nextTimestamp);

    private static TimeSpan MaxOf(TimeSpan first, TimeSpan second) => second > first ? second : first;

    private static async Task RemuxAsync(
        string ivfPath, string oggPath, string outputPath, CancellationToken cancellationToken)
    {
        var arguments = FFMpegArguments.FromFileInput(ivfPath);
        if (oggPath is not null)
        {
            arguments = arguments.AddFileInput(oggPath);
        }

        var errors = new List<string>();
        var succeeded = await arguments
            .OutputToFile(outputPath, true, options =>
            {
                options.SelectStream(0, 0, Channel.Video);
                if (oggPath is not null)
                {
                    options.SelectStream(0, 1, Channel.Audio);
                }

                options.WithCopyCodec().ForceFormat("matroska");
            })
            .NotifyOnError(errors.Add)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(false)
            .ConfigureAwait(false);

        if (!succeeded || !File.Exists(outputPath))
        {
            throw new VideoToolProcessingException(
                "The demultiplexed Mode 2 streams could not be muxed into an intermediate file. " +
                string.Join(" ", errors.TakeLast(5)));
        }
    }
}
