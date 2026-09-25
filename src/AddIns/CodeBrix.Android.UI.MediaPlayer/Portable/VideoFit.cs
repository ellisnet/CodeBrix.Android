using System;

namespace CodeBrix.Android.UI.MediaPlayer.Portable;

/// <summary>How a stretch mode fits a picture into a box (as the CodeBrix.Platform presenters fit it).</summary>
internal enum VideoStretch
{
    /// <summary>The picture's own size, centred.</summary>
    None = 0,

    /// <summary>The box, whatever the picture's proportions.</summary>
    Fill = 1,

    /// <summary>The largest picture of its own proportions inside the box, centred (bars on two sides).</summary>
    Uniform = 2,

    /// <summary>The smallest picture of its own proportions covering the box, centred (edges cut).</summary>
    UniformToFill = 3,
}

/// <summary>
/// The rectangle a video picture of a given size is drawn in inside a box, per stretch mode (pure arithmetic, host-free
/// tested; the Android presenter lays its TextureView out at it and clips it to the box).
/// </summary>
internal static class VideoFit
{
    /// <summary>The destination rectangle (left, top, width, height) of the picture inside the box.</summary>
    /// <param name="videoWidth">The picture's width in pixels (0 = unknown: the box).</param>
    /// <param name="videoHeight">The picture's height in pixels (0 = unknown: the box).</param>
    /// <param name="boxWidth">The box's width.</param>
    /// <param name="boxHeight">The box's height.</param>
    /// <param name="stretch">The stretch mode.</param>
    /// <returns>The rectangle, in the box's units.</returns>
    internal static (double Left, double Top, double Width, double Height) Destination(double videoWidth, double videoHeight, double boxWidth, double boxHeight, VideoStretch stretch)
    {
        if (videoWidth <= 0 || videoHeight <= 0 || boxWidth <= 0 || boxHeight <= 0 || stretch == VideoStretch.Fill)
        {
            return (0, 0, Math.Max(0, boxWidth), Math.Max(0, boxHeight));
        }

        double width;
        double height;
        switch (stretch)
        {
            case VideoStretch.None:
                width = videoWidth;
                height = videoHeight;
                break;
            case VideoStretch.UniformToFill:
            {
                var scale = Math.Max(boxWidth / videoWidth, boxHeight / videoHeight);
                width = videoWidth * scale;
                height = videoHeight * scale;
                break;
            }

            default:
            {
                var scale = Math.Min(boxWidth / videoWidth, boxHeight / videoHeight);
                width = videoWidth * scale;
                height = videoHeight * scale;
                break;
            }
        }

        return ((boxWidth - width) / 2, (boxHeight - height) / 2, width, height);
    }
}
