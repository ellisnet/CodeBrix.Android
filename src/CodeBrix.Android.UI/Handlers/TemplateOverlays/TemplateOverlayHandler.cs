using System.Collections.Generic;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the view of a control whose visible form is a native widget drawn OVER its Core template: it mirrors the
/// template (like the templated fallback) and shows one native <see cref="NativeOverlay"/> on top, laid out at
/// <see cref="OverlayRect"/> (the AP3b tab-row technique, <see cref="TabStripHostView"/>, generalised).
/// </summary>
internal sealed class TemplateOverlayHostView : CodeBrixContentViewGroup
{
    private Rect _overlayDips = Rect.Empty;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    /// <param name="overlay">The native widget shown over the template.</param>
    internal TemplateOverlayHostView(AContext context, AView overlay)
        : base(context)
    {
        NativeOverlay = overlay;
        overlay.Visibility = AViewStates.Gone;
        AddView(overlay);
    }

    /// <summary>The native widget shown over the template.</summary>
    internal AView NativeOverlay { get; }

    /// <summary>Where the overlay goes, relative to this view, in DIPs (empty = hidden).</summary>
    internal Rect OverlayRect
    {
        get => _overlayDips;
        set
        {
            var shown = !value.IsEmpty && value.Width > 0 && value.Height > 0;
            var state = shown ? AViewStates.Visible : AViewStates.Gone;
            if (_overlayDips == value && NativeOverlay.Visibility == state)
            {
                return;
            }

            _overlayDips = value;
            NativeOverlay.Visibility = state;
            RequestLayout();
        }
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        var (left, top, right, bottom) = OverlayPixels();
        NativeOverlay.Measure(MeasureSpecExtensions.Exactly(right - left), MeasureSpecExtensions.Exactly(bottom - top));
    }

    /// <inheritdoc />
    protected override void OnLaidOut(int width, int height)
    {
        base.OnLaidOut(width, height);
        var (left, top, right, bottom) = OverlayPixels();
        if (NativeOverlay.MeasuredWidth != right - left || NativeOverlay.MeasuredHeight != bottom - top)
        {
            NativeOverlay.Measure(MeasureSpecExtensions.Exactly(right - left), MeasureSpecExtensions.Exactly(bottom - top));
        }

        // The overlay was added first and element views are inserted before it, so it stays the last child: drawn on
        // top of the template it covers.
        NativeOverlay.Layout(left, top, right, bottom);
    }

    private (int Left, int Top, int Right, int Bottom) OverlayPixels()
    {
        if (_overlayDips.IsEmpty)
        {
            return (0, 0, 0, 0);
        }

        var density = HandlerContext.Density(ElementHandler?.Element);
        var pixels = LayoutReplayMath.ChildPixels(AbsoluteDipOrigin, _overlayDips, density);
        return (pixels.Left, pixels.Top, pixels.Right, pixels.Bottom);
    }
}

/// <summary>AP10-B: what tests and diagnostics read of a <see cref="TemplateOverlayHandler{TElement, TOverlay}"/>.</summary>
internal interface ITemplateOverlayHandler
{
    /// <summary>True while the native widget is shown (and the covered parts are not drawn).</summary>
    bool IsOverlayVisible { get; }

    /// <summary>The native widget.</summary>
    AView OverlayView { get; }
}

/// <summary>
/// AP10-B: the base of the handlers that show a control as a native Material widget OVER its Core template
/// (InfoBar, RatingControl, SplitButton / ToggleSplitButton). The control keeps its template, so everything WinUI does
/// stays Core's: the template parts exist, are laid out, answer hit tests and Core-injected input, run the control's
/// visual states and raise its events. The parts the native widget replaces (<see cref="CoveredParts"/>, or the whole
/// template) are laid out by Core but not drawn; the widget is drawn in their place and takes the real fingers, which
/// it hands to Core through the parts' own paths (a part Button's <c>RaiseClickFromPlatform</c>, a public property).
/// When the native form cannot show the control's current state (<see cref="IsOverlayShown"/> false: an element in a
/// place the widget has no room for), the template is simply shown.
/// </summary>
/// <typeparam name="TElement">The control type.</typeparam>
/// <typeparam name="TOverlay">The native widget type.</typeparam>
internal abstract class TemplateOverlayHandler<TElement, TOverlay> : ViewGroupHandler<TElement, TemplateOverlayHostView>, ITemplateOverlayHandler
    where TElement : FrameworkElement
    where TOverlay : AView
{
    private readonly List<AView> _hidden = new();
    private bool _refreshPosted;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper.</param>
    protected TemplateOverlayHandler(IPropertyMapper mapper)
        : base(mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The native widget (null while disconnected).</summary>
    internal TOverlay Overlay => (NativeView as TemplateOverlayHostView)?.NativeOverlay as TOverlay;

    /// <inheritdoc />
    public AView OverlayView => Overlay;

    /// <inheritdoc />
    public bool IsOverlayVisible => NativeView is TemplateOverlayHostView { NativeOverlay.Visibility: AViewStates.Visible };

    /// <summary>Creates the native widget.</summary>
    /// <param name="context">A Material 3 context.</param>
    /// <returns>The widget.</returns>
    protected abstract TOverlay CreateOverlay(AContext context);

    /// <summary>True when the native widget can show the control as it is now.</summary>
    /// <param name="element">The control.</param>
    /// <returns>True to show the widget.</returns>
    protected abstract bool IsOverlayShown(TElement element);

    /// <summary>Brings the widget up to date with the control.</summary>
    /// <param name="element">The control.</param>
    /// <param name="overlay">The widget.</param>
    protected abstract void UpdateOverlay(TElement element, TOverlay overlay);

    /// <summary>
    /// The template parts the widget replaces (laid out by Core, not drawn), or null for the whole template. The
    /// widget covers the first part's rectangle (or the whole control).
    /// </summary>
    /// <param name="element">The control.</param>
    /// <returns>The parts, or null.</returns>
    protected virtual IReadOnlyList<FrameworkElement> CoveredParts(TElement element) => null;

    /// <summary>Maps a property the widget shows: refreshed after the current layout pass.</summary>
    public static void MapOverlay(TemplateOverlayHandler<TElement, TOverlay> handler, TElement element) => handler.PostRefresh();

    /// <summary>Re-reads the control after the current layout pass.</summary>
    protected void PostRefresh()
    {
        if (_refreshPosted || NativeView is not { } view)
        {
            return;
        }

        _refreshPosted = true;
        view.Post(() =>
        {
            _refreshPosted = false;
            Refresh();
        });
    }

    /// <inheritdoc />
    protected override TemplateOverlayHostView CreatePlatformView() => new(Context, CreateOverlay(MaterialWidgets.Material3(Context)));

    /// <inheritdoc />
    protected override void DisconnectHandler(TemplateOverlayHostView platformView)
    {
        ShowCovered();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        Refresh();

        // The element's own ActualWidth/ActualHeight and its template parts' positions are final only once the layout
        // pass is over: read them again then.
        PostRefresh();
    }

    /// <summary>Re-reads the control now: the widget's rectangle, the covered parts, the widget's content.</summary>
    protected void Refresh()
    {
        if (Element is not TElement element || NativeView is not TemplateOverlayHostView view || view.NativeOverlay is not TOverlay overlay)
        {
            return;
        }

        ShowCovered();
        var size = HasArranged ? new Size(ArrangedRect.Width, ArrangedRect.Height) : new Size(element.ActualWidth, element.ActualHeight);
        if (!IsOverlayShown(element) || size.Width <= 0 || size.Height <= 0)
        {
            view.OverlayRect = Rect.Empty;
            return;
        }

        var parts = CoveredParts(element);
        Rect rect;
        if (parts == null)
        {
            rect = new Rect(0, 0, size.Width, size.Height);
            foreach (var child in view.ElementChildren)
            {
                Hide(view.ViewOf(child));
            }
        }
        else
        {
            if (parts.Count == 0 || parts[0] is not { ActualWidth: > 0, ActualHeight: > 0 } first)
            {
                view.OverlayRect = Rect.Empty;
                return;
            }

            var origin = first.TransformToVisual(element).TransformPoint(default);
            rect = new Rect(origin.X, origin.Y, first.ActualWidth, first.ActualHeight);
            foreach (var part in parts)
            {
                Hide((part?.Handler as IViewHandler)?.NativeView);
            }
        }

        UpdateOverlay(element, overlay);
        view.OverlayRect = rect;
    }

    /// <summary>The first descendant of <paramref name="root"/> named <paramref name="name"/> (template order).</summary>
    /// <param name="root">Where to search.</param>
    /// <param name="name">The x:Name.</param>
    /// <returns>The element, or null.</returns>
    protected static FrameworkElement FindNamed(DependencyObject root, string name)
    {
        if (root == null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: var childName } match && childName == name)
            {
                return match;
            }

            if (FindNamed(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private void Hide(AView view)
    {
        if (view == null || view.Visibility != AViewStates.Visible)
        {
            return;
        }

        // Core still lays the part out (it answers hit tests, automation, focus); the widget draws it and takes the
        // fingers.
        view.Visibility = AViewStates.Invisible;
        _hidden.Add(view);
    }

    private void ShowCovered()
    {
        foreach (var view in _hidden)
        {
            if (view.Handle != System.IntPtr.Zero && view.Visibility == AViewStates.Invisible)
            {
                view.Visibility = AViewStates.Visible;
            }
        }

        _hidden.Clear();
    }
}
