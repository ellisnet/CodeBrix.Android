using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Platform.Drawables;
using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AColor = global::Android.Graphics.Color;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The base of handlers that replace a Control's template with a native widget (OwnsVisuals):
/// its <see cref="ControlMappers.ControlMapper"/> maps what every such control shows (plan 2.5): IsEnabled,
/// Foreground, Background / BorderBrush / BorderThickness / CornerRadius / BackgroundSizing
/// (one <see cref="BorderDrawable"/> as the view background), Padding and the font. Foreground
/// and the font apply to TextView-based widgets; other widgets override the Map*Core hooks.
/// A templated control without a native handler is NOT served by this base - the templated
/// fallback mirrors its Core template instead (whose own borders draw its background).
/// </summary>
/// <typeparam name="TControl">The control type.</typeparam>
/// <typeparam name="TView">The native widget type.</typeparam>
internal abstract class ControlViewHandler<TControl, TView> : ViewHandler<TControl, TView>, IControlViewHandler
    where TControl : Control
    where TView : AView
{
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private readonly BrushWatcher _foregroundWatcher;
    private BorderDrawable _drawable;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper (chain it to <see cref="ControlMappers.ControlMapper"/>).</param>
    /// <param name="commandMapper">The command mapper, or null.</param>
    protected ControlViewHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
        _backgroundWatcher = new BrushWatcher(() => _drawable?.InvalidateBrushes());
        _borderWatcher = new BrushWatcher(() => _drawable?.InvalidateBrushes());
        _foregroundWatcher = new BrushWatcher(() =>
        {
            if (Element is Control control)
            {
                MapForegroundCore(control);
            }
        });
    }

    /// <summary>Native widgets own their visuals: Core does not expand the template.</summary>
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <inheritdoc />
    public void MapIsEnabledCore(Control control)
    {
        if (NativeView is { } view)
        {
            view.Enabled = control.IsEnabled;
        }
    }

    /// <inheritdoc />
    public virtual void MapForegroundCore(Control control)
    {
        _foregroundWatcher.Watch(control.Foreground);
        if (NativeView is ATextView text && control.Foreground != null)
        {
            text.SetTextColor(new AColor(BrushPaint.SingleColor(control.Foreground, text.CurrentTextColor)));
        }
    }

    /// <inheritdoc />
    public virtual void MapBackgroundCore(Control control)
    {
        if (NativeView is not { } view)
        {
            return;
        }

        _backgroundWatcher.Watch(control.Background);
        _borderWatcher.Watch(control.BorderBrush);
        if (control.Background == null && control.BorderBrush == null)
        {
            // Keep the widget's own (Material) background: the theme default is not overridden.
            if (_drawable != null)
            {
                view.Background = null;
                _drawable = null;
            }

            return;
        }

        if (_drawable == null)
        {
            _drawable = new BorderDrawable();
            view.Background = _drawable;
        }

        _drawable.Update(control.Background, control.BorderBrush, control.BorderThickness, control.CornerRadius, control.BackgroundSizing, Density);
    }

    /// <inheritdoc />
    public virtual void MapPaddingCore(Control control)
    {
        if (NativeView is not { } view)
        {
            return;
        }

        var density = Density;
        var padding = control.Padding;
        view.SetPadding(
            LayoutReplayMath.ToPixels(padding.Left, density),
            LayoutReplayMath.ToPixels(padding.Top, density),
            LayoutReplayMath.ToPixels(padding.Right, density),
            LayoutReplayMath.ToPixels(padding.Bottom, density));
    }

    /// <inheritdoc />
    public virtual void MapFontCore(Control control)
    {
        if (NativeView is not ATextView text)
        {
            return;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(control.FontFamily, control.FontWeight, control.FontStyle, control.FontStretch);
        if (typeface != null)
        {
            text.Typeface = typeface;
        }

        text.SetTextSize(AComplexUnitType.Px, (float)(control.FontSize * Density));
        text.LetterSpacing = control.CharacterSpacing / 1000f;
    }

    /// <inheritdoc />
    public override Windows.Foundation.Size Measure(Windows.Foundation.Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <inheritdoc />
    protected override void DisconnectHandler(TView platformView)
    {
        _backgroundWatcher.Clear();
        _borderWatcher.Clear();
        _foregroundWatcher.Clear();
        base.DisconnectHandler(platformView);
    }
}

/// <summary>What the control mapper calls on a control handler.</summary>
internal interface IControlViewHandler : IViewHandler
{
    /// <summary>Maps IsEnabled.</summary>
    void MapIsEnabledCore(Control control);

    /// <summary>Maps Foreground.</summary>
    void MapForegroundCore(Control control);

    /// <summary>Maps Background and the border properties.</summary>
    void MapBackgroundCore(Control control);

    /// <summary>Maps Padding.</summary>
    void MapPaddingCore(Control control);

    /// <summary>Maps the font and CharacterSpacing.</summary>
    void MapFontCore(Control control);
}

/// <summary>The Control mapper (chained under the handlers of native controls).</summary>
internal static class ControlMappers
{
    /// <summary>The Control mapper: IsEnabled, Foreground, background/border, Padding, font.</summary>
    public static readonly PropertyMapper<Control, IControlViewHandler> ControlMapper = new(ViewMappers.ViewMapper)
    {
        [Control.IsEnabledProperty] = (h, c) => h.MapIsEnabledCore(c),
        [Control.ForegroundProperty] = (h, c) => h.MapForegroundCore(c),
        [FrameworkElement.BackgroundProperty] = (h, c) => h.MapBackgroundCore(c),
        [Control.BorderBrushProperty] = (h, c) => h.MapBackgroundCore(c),
        [Control.BorderThicknessProperty] = (h, c) => h.MapBackgroundCore(c),
        [Control.CornerRadiusProperty] = (h, c) => h.MapBackgroundCore(c),
        [Control.BackgroundSizingProperty] = (h, c) => h.MapBackgroundCore(c),
        [Control.PaddingProperty] = (h, c) => h.MapPaddingCore(c),
        [Control.FontFamilyProperty] = (h, c) => h.MapFontCore(c),
        [Control.FontSizeProperty] = (h, c) => h.MapFontCore(c),
        [Control.FontWeightProperty] = (h, c) => h.MapFontCore(c),
        [Control.FontStyleProperty] = (h, c) => h.MapFontCore(c),
        [Control.FontStretchProperty] = (h, c) => h.MapFontCore(c),
        [Control.CharacterSpacingProperty] = (h, c) => h.MapFontCore(c),
    };
}
