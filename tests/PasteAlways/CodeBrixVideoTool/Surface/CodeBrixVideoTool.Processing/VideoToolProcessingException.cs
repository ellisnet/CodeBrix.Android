using System;

namespace CodeBrixVideoTool.Processing;

/// <summary>
/// Thrown when a file cannot be probed, a conversion cannot be planned, or a conversion fails in a
/// way this application can explain in a sentence.
/// </summary>
public class VideoToolProcessingException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in a sentence a person can act on.</param>
    public VideoToolProcessingException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and the failure underneath it.</summary>
    /// <param name="message">What went wrong, in a sentence a person can act on.</param>
    /// <param name="innerException">The failure this one is explaining.</param>
    public VideoToolProcessingException(string message, Exception innerException)
        : base(message, innerException) { }
}
