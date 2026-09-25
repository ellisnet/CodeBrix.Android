using System;

namespace JustBetweenUs.Controls;

/// <summary>
/// Describes an image that an <see cref="EmbeddedImage"/> could not load. A page (or a control that
/// composes an <see cref="EmbeddedImage"/>) handles <see cref="EmbeddedImage.LoadFailed"/> to show
/// the failure instead of leaving an empty image behind.
/// </summary>
public sealed class EmbeddedImageFailedEventArgs : EventArgs
{
    /// <summary>Creates the arguments for a failed image load.</summary>
    /// <param name="uriSource">The URI the control was asked to load.</param>
    /// <param name="error">The exception that stopped the load.</param>
    public EmbeddedImageFailedEventArgs(string uriSource, Exception error)
    {
        UriSource = uriSource;
        Error = error;
    }

    /// <summary>The URI the control was asked to load.</summary>
    public string UriSource { get; }

    /// <summary>The exception that stopped the load.</summary>
    public Exception Error { get; }
}
