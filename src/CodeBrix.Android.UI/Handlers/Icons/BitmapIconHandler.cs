using CodeBrix.Android.UI.Platform;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using AImageView = global::Android.Widget.ImageView;
using APorterDuffMode = global::Android.Graphics.PorterDuff.Mode;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the handler of BitmapIcon (tsv row BitmapIcon: "ImageView / Drawable (tinted)"). Core composes the icon
/// (a Grid holding an Image of UriSource), whose native form is an ImageView (<see cref="ImageHandler"/>); this
/// handler mirrors that composition (as the templated fallback does) and tints the ImageView with the icon's
/// Foreground when ShowAsMonochrome is on (WinUI draws the bitmap's alpha in the Foreground colour; SrcIn does the
/// same), and clears the tint when it is off.
/// </summary>
internal sealed class BitmapIconHandler : ViewGroupHandler<BitmapIcon, CodeBrixContentViewGroup>
{
    /// <summary>BitmapIcon's mapper.</summary>
    public static readonly PropertyMapper<BitmapIcon, BitmapIconHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [BitmapIcon.ShowAsMonochromeProperty] = MapTint,
        [BitmapIcon.UriSourceProperty] = MapTint,
        [IconElement.ForegroundProperty] = MapTint,
    };

    /// <summary>Creates the handler.</summary>
    public BitmapIconHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The tint applied now (ARGB), or null.</summary>
    internal int? AppliedTint { get; private set; }

    /// <summary>Maps ShowAsMonochrome / Foreground / UriSource: the tint of the icon's ImageView.</summary>
    public static void MapTint(BitmapIconHandler handler, BitmapIcon element) => handler.ApplyTint();

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        ApplyTint();
    }

    /// <inheritdoc />
    public override void OnChildAdded(UIElement child, int index)
    {
        base.OnChildAdded(child, index);
        ApplyTint();
    }

    private void ApplyTint()
    {
        if (Element is not BitmapIcon element || FindImage(element) is not { } image || (image.Handler as IViewHandler)?.NativeView is not AImageView view)
        {
            return;
        }

        var tint = element.ShowAsMonochrome ? ThemeResources.ColorOf(element.Foreground) : null;
        if (tint is int color)
        {
            view.SetColorFilter(new AColor(color), APorterDuffMode.SrcIn!);
        }
        else
        {
            view.ClearColorFilter();
        }

        AppliedTint = tint;
    }

    private static Image FindImage(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Image image)
            {
                return image;
            }

            if (FindImage(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
