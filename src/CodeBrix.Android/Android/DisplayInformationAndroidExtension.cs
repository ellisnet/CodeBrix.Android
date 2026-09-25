using System;
using Windows.Graphics.Display;
using ADisplayMetrics = global::Android.Util.DisplayMetrics;
using AOrientation = global::Android.Content.Res.Orientation;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of the DisplayInformation registry extension, from the
/// application context's display metrics and configuration. One view pixel (DIP) is
/// DisplayMetrics.Density physical pixels.
/// </summary>
internal sealed class DisplayInformationAndroidExtension : IDisplayInformationExtension
{
    private const float BaseDpi = 96f;

    /// <inheritdoc />
    public DisplayOrientations CurrentOrientation =>
        AndroidContext.Current.Resources.Configuration.Orientation == AOrientation.Portrait
            ? DisplayOrientations.Portrait
            : DisplayOrientations.Landscape;

    /// <inheritdoc />
    public uint ScreenHeightInRawPixels => (uint)Math.Max(0, Metrics.HeightPixels);

    /// <inheritdoc />
    public uint ScreenWidthInRawPixels => (uint)Math.Max(0, Metrics.WidthPixels);

    /// <inheritdoc />
    public float LogicalDpi => BaseDpi * Metrics.Density;

    /// <inheritdoc />
    public double RawPixelsPerViewPixel => Metrics.Density;

    /// <inheritdoc />
    public ResolutionScale ResolutionScale => NearestResolutionScale(Metrics.Density * 100);

    /// <inheritdoc />
    public double? DiagonalSizeInInches
    {
        get
        {
            var metrics = Metrics;
            if (metrics.Xdpi <= 0 || metrics.Ydpi <= 0)
            {
                return null;
            }

            var width = metrics.WidthPixels / metrics.Xdpi;
            var height = metrics.HeightPixels / metrics.Ydpi;
            return Math.Sqrt((width * width) + (height * height));
        }
    }

    private static ADisplayMetrics Metrics => AndroidContext.Current.Resources.DisplayMetrics;

    /// <summary>Returns the defined ResolutionScale closest to a scale percentage.</summary>
    internal static ResolutionScale NearestResolutionScale(double percent)
    {
        var best = ResolutionScale.Scale100Percent;
        var bestDistance = double.MaxValue;
        foreach (var value in Enum.GetValues<ResolutionScale>())
        {
            if (value == ResolutionScale.Invalid)
            {
                continue;
            }

            var distance = Math.Abs((int)value - percent);
            if (distance < bestDistance)
            {
                best = value;
                bestDistance = distance;
            }
        }

        return best;
    }
}
