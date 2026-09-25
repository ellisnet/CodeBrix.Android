// Technique from .NET MAUI, src/Core/src/Handlers/Image/ImageHandler.Android.cs and ImageHandler2.Android.cs
// @ 828569a864 (an ImageView whose scale type follows the aspect; the bitmap set when the source finished
// loading). Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See
// THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Composition.Android;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using ABitmap = global::Android.Graphics.Bitmap;
using AImageView = global::Android.Widget.ImageView;
using AMatrix = global::Android.Graphics.Matrix;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The Image handler (plan 3 rows Image, BitmapImage; 2.13): an ImageView showing the bitmap Core decoded
/// for the element's source (Core loads BitmapImage / WriteableBitmap / RenderTargetBitmap sources through
/// IImagingPlatform into composition surfaces whose platform object is an Android bitmap -
/// <see cref="BitmapCompositionSurfacePlatform"/>; the composition itself draws nothing, D-P2). Core keeps
/// measuring and arranging the Image (the natural size, Stretch, alignment); the view maps Stretch to its
/// scale type: Fill = FitXY, Uniform = FitCenter, UniformToFill = CenterCrop, None = the bitmap at its size
/// in DIPs, centred.
/// </summary>
internal sealed class ImageHandler : ViewHandler<Image, AImageView>
{
    /// <summary>Image's mapper.</summary>
    public static readonly PropertyMapper<Image, ImageHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Image.SourceProperty] = MapSource,
        [Image.StretchProperty] = MapStretch,
    };

    private ABitmap _shown;
    private ABitmap _childContent;

    /// <summary>Creates the handler.</summary>
    public ImageHandler()
        : base(Mapper)
    {
    }

    /// <summary>Maps Source (the bitmap once Core has loaded it).</summary>
    public static void MapSource(ImageHandler handler, Image element) => handler.UpdateBitmap();

    /// <summary>Maps Stretch to the scale type.</summary>
    public static void MapStretch(ImageHandler handler, Image element) => handler.ApplyScale();

    /// <summary>The Android bitmap Core decoded for an Image (null while loading or for a non-bitmap source).</summary>
    /// <param name="image">The Image.</param>
    /// <returns>The bitmap, or null.</returns>
    internal static ABitmap BitmapOf(UIElement image)
    {
        if (image?.Visual is not ContainerVisual visual)
        {
            return null;
        }

        foreach (var child in visual.Children)
        {
            if (child is SpriteVisual { Brush: CompositionSurfaceBrush { Surface: PlatformCompositionSurface surface } }
                && surface.Platform is BitmapCompositionSurfacePlatform { Bitmap: { } bitmap } && bitmap.Handle != IntPtr.Zero)
            {
                return bitmap;
            }
        }

        return null;
    }

    /// <summary>
    /// Shows a bitmap an add-in rendered from the Image's own visual child - the Skia canvas Core adds to an Image
    /// whose Source is an SvgImageSource (the Svg add-in's SvgCanvas). The Image's native view is a leaf ImageView,
    /// so that child's view cannot be attached; its handler paints into a bitmap of exactly the Image's arranged
    /// pixel size instead. Shown only while Core decoded no bitmap of its own; null stops showing it.
    /// </summary>
    /// <param name="bitmap">The rendered content (the Image's arranged pixel size), or null.</param>
    internal void ShowChildContent(ABitmap bitmap)
    {
        var same = bitmap != null && ReferenceEquals(bitmap, _childContent);
        _childContent = bitmap;
        UpdateBitmap();
        if (same)
        {
            // Re-rendered into the same bitmap: the ImageView has to draw it again.
            NativeView?.Invalidate();
        }
    }

    /// <inheritdoc />
    protected override AImageView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(AImageView platformView)
    {
        base.ConnectHandler(platformView);
        if (Element is Image image)
        {
            image.ImageOpened += OnImageOpened;
            image.ImageFailed += OnImageFailed;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(AImageView platformView)
    {
        if (Element is Image image)
        {
            image.ImageOpened -= OnImageOpened;
            image.ImageFailed -= OnImageFailed;
        }

        platformView.SetImageBitmap(null);
        _shown = null;
        _childContent = null;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);

        // The source may have finished loading during this layout pass (Core loads on measure).
        UpdateBitmap();

        // Clipping to the layout slot (UniformToFill, None, a slot smaller than the picture) is Core's layout
        // clip, replayed by the parent view group for every element (Platform/ClipReplay).
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        ApplyScale();
    }

    private void OnImageOpened(object sender, RoutedEventArgs e) => UpdateBitmap();

    private void OnImageFailed(object sender, ExceptionRoutedEventArgs e) => UpdateBitmap();

    private void UpdateBitmap()
    {
        if (PlatformView is not { } view || Element is not Image element)
        {
            return;
        }

        var bitmap = element.Source == null ? null : BitmapOf(element) ?? _childContent;
        if (ReferenceEquals(bitmap, _shown))
        {
            return;
        }

        _shown = bitmap;
        view.SetImageBitmap(bitmap);
        ApplyScale();
    }

    private void ApplyScale()
    {
        if (PlatformView is not { } view || Element is not Image element)
        {
            return;
        }

        if (_shown != null && ReferenceEquals(_shown, _childContent))
        {
            // Rendered at the Image's arranged pixel size: shown one to one (the child applied the Stretch).
            view.SetScaleType(AImageView.ScaleType.FitXy);
            return;
        }

        switch (element.Stretch)
        {
            case Stretch.Fill:
                view.SetScaleType(AImageView.ScaleType.FitXy);
                break;
            case Stretch.Uniform:
                view.SetScaleType(AImageView.ScaleType.FitCenter);
                break;
            case Stretch.UniformToFill:
                view.SetScaleType(AImageView.ScaleType.CenterCrop);
                break;
            default:
            {
                // None: the bitmap's pixels are DIPs (as Core measures it), centred in the arranged size.
                view.SetScaleType(AImageView.ScaleType.Matrix);
                var density = (float)Density;
                var matrix = new AMatrix();
                if (_shown is { } bitmap && bitmap.Handle != IntPtr.Zero && HasArranged)
                {
                    var viewWidth = (float)(ArrangedRect.Width * density);
                    var viewHeight = (float)(ArrangedRect.Height * density);
                    matrix.SetScale(density, density);
                    matrix.PostTranslate((viewWidth - (bitmap.Width * density)) / 2f, (viewHeight - (bitmap.Height * density)) / 2f);
                }

                view.ImageMatrix = matrix;
                break;
            }
        }
    }
}
