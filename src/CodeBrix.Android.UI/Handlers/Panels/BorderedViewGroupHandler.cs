using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Drawables;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of the Core elements that draw a background and border themselves and host
/// visual children: Border, every Panel, ContentPresenter (Core's
/// <c>IBorderInfoProvider</c> elements). The view is a <see cref="CodeBrixContentViewGroup"/>
/// whose background is a <see cref="BorderDrawable"/> (brush fill, per-side border, per-corner
/// radius, BackgroundSizing); brush contents are watched so re-pointed colours repaint.
/// </summary>
/// <typeparam name="TElement">Border, Panel or ContentPresenter.</typeparam>
internal abstract class BorderedViewGroupHandler<TElement> : ViewGroupHandler<TElement, CodeBrixContentViewGroup>
    where TElement : FrameworkElement
{
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private BorderDrawable _drawable;
    private double _drawnDensity;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper (its border keys map to <see cref="MapBorder"/>).</param>
    protected BorderedViewGroupHandler(IPropertyMapper mapper)
        : base(mapper)
    {
        _backgroundWatcher = new BrushWatcher(OnBrushContentChanged);
        _borderWatcher = new BrushWatcher(OnBrushContentChanged);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>
    /// True when the children are clipped to the inner edge of rounded corners (Border does;
    /// panels and presenters do not).
    /// </summary>
    protected virtual bool ClipsChildToCorners => false;

    /// <summary>Maps Background / BorderBrush / BorderThickness / CornerRadius / BackgroundSizing.</summary>
    public static void MapBorder(BorderedViewGroupHandler<TElement> handler, TElement element) => handler.UpdateBorder();

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        _backgroundWatcher.Clear();
        _borderWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        if (_drawnDensity != Density)
        {
            UpdateBorder();
        }
    }

    private void UpdateBorder()
    {
        if (NativeView is not CodeBrixContentViewGroup view || Element is not IBorderInfoProvider info)
        {
            return;
        }

        var density = Density;
        _drawnDensity = density;
        var background = info.Background;
        var borderBrush = info.BorderBrush;
        _backgroundWatcher.Watch(background);
        _borderWatcher.Watch(borderBrush);

        if (background == null && borderBrush == null)
        {
            if (_drawable != null)
            {
                view.Background = null;
                _drawable = null;
            }
        }
        else
        {
            if (_drawable == null)
            {
                _drawable = new BorderDrawable();
                view.Background = _drawable;
            }

            _drawable.Update(background, borderBrush, info.BorderThickness, info.CornerRadius, info.BackgroundSizing, density);
        }

        if (ClipsChildToCorners)
        {
            var radius = info.CornerRadius;
            var rounded = radius.TopLeft > 0 || radius.TopRight > 0 || radius.BottomRight > 0 || radius.BottomLeft > 0;
            if (rounded)
            {
                var geometry = global::CodeBrix.Android.UI.Portable.Drawing.BorderGeometry.Compute(10000, 10000, info.BorderThickness, radius, density);
                view.SetChildClip(geometry.Left, geometry.Top, geometry.Right, geometry.Bottom, geometry.InnerRadii);
            }
            else
            {
                view.SetChildClip(0, 0, 0, 0, null);
            }
        }
    }

    private void OnBrushContentChanged() => _drawable?.InvalidateBrushes();
}

/// <summary>The handler of Border.</summary>
internal sealed class BorderHandler : BorderedViewGroupHandler<Border>
{
    /// <summary>Border's mapper.</summary>
    public static readonly PropertyMapper<Border, BorderHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [FrameworkElement.BackgroundProperty] = MapBorder,
        [Border.BorderBrushProperty] = MapBorder,
        [Border.BorderThicknessProperty] = MapBorder,
        [Border.CornerRadiusProperty] = MapBorder,
        [Border.BackgroundSizingProperty] = MapBorder,
    };

    /// <summary>Creates the handler.</summary>
    public BorderHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    protected override bool ClipsChildToCorners => true;
}

/// <summary>The handler of every Panel (Grid, StackPanel, Canvas, RelativePanel, app panels, the root panels).</summary>
internal sealed class PanelHandler : BorderedViewGroupHandler<Panel>
{
    /// <summary>The panels' mapper (Background, and the border properties Grid and StackPanel declare).</summary>
    public static readonly PropertyMapper<Panel, PanelHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [FrameworkElement.BackgroundProperty] = MapBorder,
        [Grid.BorderBrushProperty] = MapBorder,
        [Grid.BorderThicknessProperty] = MapBorder,
        [Grid.CornerRadiusProperty] = MapBorder,
        [Grid.BackgroundSizingProperty] = MapBorder,
        [StackPanel.BorderBrushProperty] = MapBorder,
        [StackPanel.BorderThicknessProperty] = MapBorder,
        [StackPanel.CornerRadiusProperty] = MapBorder,
        [StackPanel.BackgroundSizingProperty] = MapBorder,
    };

    /// <summary>Creates the handler.</summary>
    public PanelHandler()
        : base(Mapper)
    {
    }
}

/// <summary>The handler of ContentPresenter (the presenter inside most control templates).</summary>
internal sealed class ContentPresenterHandler : BorderedViewGroupHandler<ContentPresenter>
{
    /// <summary>ContentPresenter's mapper.</summary>
    public static readonly PropertyMapper<ContentPresenter, ContentPresenterHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [FrameworkElement.BackgroundProperty] = MapBorder,
        [ContentPresenter.BorderBrushProperty] = MapBorder,
        [ContentPresenter.BorderThicknessProperty] = MapBorder,
        [ContentPresenter.CornerRadiusProperty] = MapBorder,
        [ContentPresenter.BackgroundSizingProperty] = MapBorder,
    };

    /// <summary>Creates the handler.</summary>
    public ContentPresenterHandler()
        : base(Mapper)
    {
    }
}
