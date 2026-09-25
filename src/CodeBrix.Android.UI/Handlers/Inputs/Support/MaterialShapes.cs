using Microsoft.UI.Xaml;
using AShapeAppearanceModel = Google.Android.Material.Shape.ShapeAppearanceModel;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>WinUI corner radii and border thicknesses as Material shape values.</summary>
internal static class MaterialShapes
{
    /// <summary>A shape model with the four WinUI corner radii (DIPs) in pixels.</summary>
    /// <param name="radius">The corner radii.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The model.</returns>
    internal static AShapeAppearanceModel Model(CornerRadius radius, double density) =>
        AShapeAppearanceModel.InvokeBuilder()
            .SetTopLeftCornerSize((float)(radius.TopLeft * density))
            .SetTopRightCornerSize((float)(radius.TopRight * density))
            .SetBottomRightCornerSize((float)(radius.BottomRight * density))
            .SetBottomLeftCornerSize((float)(radius.BottomLeft * density))
            .Build();

    /// <summary>
    /// The one stroke width a Material widget draws for a WinUI BorderThickness (Material strokes are
    /// uniform): the largest side, in pixels.
    /// </summary>
    /// <param name="thickness">The border thickness.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The stroke width in pixels (0 for none).</returns>
    internal static int StrokeWidth(Thickness thickness, double density)
    {
        var dips = System.Math.Max(System.Math.Max(thickness.Left, thickness.Right), System.Math.Max(thickness.Top, thickness.Bottom));
        return dips <= 0 ? 0 : System.Math.Max(1, MaterialWidgets.Px(dips, density));
    }
}
