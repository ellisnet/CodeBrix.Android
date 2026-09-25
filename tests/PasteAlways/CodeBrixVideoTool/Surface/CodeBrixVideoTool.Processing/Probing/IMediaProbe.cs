using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Probing;

/// <summary>Looks inside a media file and reports what is in it.</summary>
public interface IMediaProbe
{
    /// <summary>
    /// Probes one file. A <c>.cbv</c> file is read by the playback core's own container readers; every
    /// other file is probed with ffprobe through CodeBrix.VideoProcessing.
    /// </summary>
    /// <param name="path">The file to look at.</param>
    /// <param name="cancellationToken">Stops the probe.</param>
    /// <returns>What the file turned out to be.</returns>
    /// <exception cref="VideoToolProcessingException">
    /// The file is missing, is not a media file this application can work with, or carries no video.
    /// </exception>
    Task<SourceMediaInfo> ProbeAsync(string path, CancellationToken cancellationToken);
}
