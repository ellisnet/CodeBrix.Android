using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the handler of a stand-alone ColorSpectrum (tsv row ColorSpectrum: "spectrum custom View (android.graphics)").
/// Its native form is the spectrum of the native ColorPicker (AP6's <see cref="ColorSpectrumView"/>: the two HSV channels
/// of Components, the selection ring, a finger picking). Core keeps the colour: a pick sets HsvColor (Core sets
/// Color from it and raises ColorChanged), and Color / HsvColor / Components set in Core move the ring. As in the AP6 ColorPicker, Shape Ring is
/// drawn as the box and the Min/Max Hue / Saturation / Value clamps are not applied.
/// </summary>
internal sealed class ColorSpectrumHandler : ViewHandler<ColorSpectrum, ColorSpectrumView>
{
    /// <summary>ColorSpectrum's mapper.</summary>
    public static readonly PropertyMapper<ColorSpectrum, ColorSpectrumHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ColorSpectrum.ColorProperty] = MapColor,
        [ColorSpectrum.HsvColorProperty] = MapColor,
        [ColorSpectrum.ComponentsProperty] = MapColor,
        [Control.IsEnabledProperty] = (h, e) => h.PlatformView.Enabled = e.IsEnabled,
    };

    /// <summary>Creates the handler.</summary>
    public ColorSpectrumHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The handler, or the templated fallback for a re-templated spectrum.</summary>
    /// <param name="element">The ColorSpectrum.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is ColorSpectrum spectrum && NativeControlPolicy.IsNative(spectrum, typeof(ColorSpectrum), new[] { "DefaultColorSpectrumStyle" }, out _)
            ? new ColorSpectrumHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps the colour and the components: the ring and the spectrum.</summary>
    public static void MapColor(ColorSpectrumHandler handler, ColorSpectrum element) =>
        handler.PlatformView.SetColor(Hsv(element), element.Components);

    /// <inheritdoc />
    public override Size Measure(Size availableSize) => ViewHandlerExtensions.GetDesiredSizeFromView(PlatformView, availableSize, Density);

    /// <inheritdoc />
    protected override ColorSpectrumView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(ColorSpectrumView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Picked += OnPicked;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ColorSpectrumView platformView)
    {
        platformView.Picked -= OnPicked;
        base.DisconnectHandler(platformView);
    }

    private void OnPicked(object sender, (double X, double Y) position)
    {
        if (Element is not ColorSpectrum element)
        {
            return;
        }

        var hsv = ColorPickerMath.AtSpectrumPosition(position.X, position.Y, element.Components, Hsv(element));

        // HsvColor is ColorSpectrum's own input path: Core sets Color from it and raises ColorChanged.
        element.HsvColor = new System.Numerics.Vector4((float)hsv.H, (float)hsv.S, (float)hsv.V, element.HsvColor.W);
    }

    // HsvColor keeps the hue of greys and black, which Color loses.
    private static HsvColor Hsv(ColorSpectrum element) => new(element.HsvColor.X, element.HsvColor.Y, element.HsvColor.Z);
}
