using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Tools;

/// <summary>Finds out whether the external tools this application needs are installed and usable.</summary>
public interface IExternalToolCheck
{
    /// <summary>
    /// Checks, in this order, that <c>ffmpeg</c> runs, that <c>ffprobe</c> runs, and that the FFmpeg build
    /// includes the SVT-AV1 encoder. Each check is made only when the one before it passed.
    /// </summary>
    /// <param name="cancellationToken">Stops the check between its steps.</param>
    /// <returns>
    /// A sentence a person can read, describing the FIRST thing found missing, or null when nothing is.
    /// </returns>
    Task<string> FindProblemAsync(CancellationToken cancellationToken);
}
