using System;

namespace PdfSideBySide.PdfRender.Viewing;

/// <summary>
/// Where one pane's page image sits inside its viewer: the size to give the image (the shared
/// zoom multiplied by the scale that fits the whole page in the viewer) and the offset to scroll
/// the viewer to so that the pane's pan position is what shows. The view works it out; the page
/// applies it to its controls, because the viewport size is the one number only the page knows.
/// </summary>
/// <param name="ImageWidth">Width in pixels to give the page image, or <see cref="double.NaN"/> when there is nothing to show.</param>
/// <param name="ImageHeight">Height in pixels to give the page image, or <see cref="double.NaN"/> when there is nothing to show.</param>
/// <param name="ScrollOffsetX">How far to scroll the viewer from its left edge.</param>
/// <param name="ScrollOffsetY">How far to scroll the viewer from its top edge.</param>
public readonly record struct PaneLayout(
    double ImageWidth,
    double ImageHeight,
    double ScrollOffsetX,
    double ScrollOffsetY)
{
    /// <summary>
    /// Nothing to lay out: no page rendered yet, or a viewer that has not been measured. The two
    /// sizes are <see cref="double.NaN"/>, which is what an image control wants when it is to size
    /// itself to its content.
    /// </summary>
    public static PaneLayout None { get; } = new(double.NaN, double.NaN, 0, 0);

    /// <summary>Whether there is a page to size and position, i.e. this is not <see cref="None"/>.</summary>
    public bool HasImage => !double.IsNaN(ImageWidth) && !double.IsNaN(ImageHeight);

    /// <summary>
    /// Works out the layout of a page pageWidth by pageHeight pixels shown in a viewer
    /// viewportWidth by viewportHeight pixels at zoomFactor, with the viewport
    /// sitting at pan. At a factor of 1 the whole page fits the viewer; every factor above
    /// that overflows it, and the pan fractions say which part of the overflow shows. Returns
    /// <see cref="None"/> when any of the sizes is missing.
    /// </summary>
    /// <param name="pageWidth">Width of the rendered page in pixels.</param>
    /// <param name="pageHeight">Height of the rendered page in pixels.</param>
    /// <param name="viewportWidth">Width of the viewer the page is shown in, in pixels.</param>
    /// <param name="viewportHeight">Height of the viewer the page is shown in, in pixels.</param>
    /// <param name="zoomFactor">The zoom level as a multiplier of the fit-the-viewer size.</param>
    /// <param name="pan">Where the viewport sits over the page, as fractions of the scrollable range.</param>
    /// <returns>The size and scroll offset to apply, or <see cref="None"/>.</returns>
    public static PaneLayout Create(double pageWidth, double pageHeight, double viewportWidth,
        double viewportHeight, double zoomFactor, PanPosition pan)
    {
        if (pan == null || zoomFactor <= 0 || pageWidth <= 0 || pageHeight <= 0
            || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return None;
        }

        var fit = Math.Min(viewportWidth / pageWidth, viewportHeight / pageHeight);
        var imageWidth = Math.Floor(pageWidth * fit * zoomFactor);
        var imageHeight = Math.Floor(pageHeight * fit * zoomFactor);

        //Whatever the image overflows its viewer by is the scrollable range the fractions apply to
        return new PaneLayout(
            imageWidth,
            imageHeight,
            pan.Horizontal * Math.Max(0, imageWidth - viewportWidth),
            pan.Vertical * Math.Max(0, imageHeight - viewportHeight));
    }
}
