using System;
using CodeBrix.Android.UI.Platform.Recycler;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>What a <see cref="ListScrollViewer"/> asks the list that created it.</summary>
internal interface IListScrollOwner
{
    /// <summary>The native list (the scroll viewer's view).</summary>
    CodeBrixRecyclerView Recycler { get; }

    /// <summary>The items panel (the scroll viewer's only child).</summary>
    RecyclerItemsPanel ItemsPanel { get; }

    /// <summary>Measures the native list for Core (DIPs in, DIPs out).</summary>
    Size MeasureList(Size availableSize);

    /// <summary>The scroll viewer was arranged at <paramref name="size"/> (DIPs).</summary>
    void ListArranged(Size size);

    /// <summary>The seam's ChangeView on the scroll viewer: scroll the native list.</summary>
    bool ChangeView(ChangeViewRequest request);
}

/// <summary>
/// The Core ScrollViewer of a RecyclerView-backed list: an element of the list's Core tree (where a
/// template's ScrollViewer would be) whose view IS the native list. It exists so that what an application
/// (or a test) finds in a list's visual tree still answers like WinUI: a ScrollViewer whose
/// HorizontalOffset / VerticalOffset / ViewChanged follow the native scrolling, whose ChangeView scrolls it,
/// and whose only child is the list's items panel (<see cref="RecyclerItemsPanel"/>, added as a visual child
/// directly - not as Content - so it is live as soon as the scroll viewer is).
/// </summary>
internal sealed class ListScrollViewer : ScrollViewer
{
    /// <summary>Creates the scroll viewer of a list.</summary>
    /// <param name="owner">The list handler.</param>
    internal ListScrollViewer(IListScrollOwner owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        IsTabStop = false;
    }

    /// <summary>The list that created it.</summary>
    internal IListScrollOwner Owner { get; }

    /// <summary>Reports the native scroll position to Core (offsets, ViewChanged).</summary>
    internal void ReportScrolled(double horizontalOffset, double verticalOffset, bool isIntermediate) =>
        OnPresenterScrolled(horizontalOffset, verticalOffset, isIntermediate);
}

/// <summary>
/// The handler of <see cref="ListScrollViewer"/>: its view is the list's <see cref="CodeBrixRecyclerView"/>
/// (the list's own view group places it at the scroll viewer's Core rectangle). Capabilities: the scroll
/// viewer has no template (OwnsVisuals), the native list measures it (MeasuresNatively) and scrolls it
/// (OwnsScrolling: ChangeView comes here); its child, the items panel, fills it.
/// </summary>
internal sealed class ListScrollViewerHandler : ViewHandler<ListScrollViewer, CodeBrixRecyclerView>
{
    /// <summary>The mapper (UIElement keys only: the scroll viewer's own properties are fixed).</summary>
    public static readonly PropertyMapper<ListScrollViewer, ListScrollViewerHandler> Mapper = new(ViewMappers.ViewMapper);

    /// <summary>The commands: ChangeView.</summary>
    public static readonly CommandMapper<ListScrollViewer, ListScrollViewerHandler> Commands = new(ElementHandler.ElementCommandMapper)
    {
        [ElementHandlerCommands.ChangeView] = (h, e, args) => e.Owner.ChangeView(args as ChangeViewRequest),
    };

    /// <summary>Creates the handler.</summary>
    public ListScrollViewerHandler()
        : base(Mapper, Commands)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively
        | ElementHandlerCapabilities.OwnsScrolling | ElementHandlerCapabilities.OwnsChildren;

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Element is not ListScrollViewer viewer)
        {
            return new Size(0, 0);
        }

        var size = viewer.Owner.MeasureList(availableSize);
        if (viewer.Owner.ItemsPanel is { } panel)
        {
            panel.ViewportSize = size;
            panel.Measure(size);
        }

        return size;
    }

    /// <inheritdoc />
    protected override CodeBrixRecyclerView CreatePlatformView() =>
        (Element as ListScrollViewer)?.Owner.Recycler ?? throw new InvalidOperationException("A ListScrollViewer is created by its list.");

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (Element is ListScrollViewer viewer)
        {
            var size = new Size(finalRect.Width, finalRect.Height);
            if (viewer.Owner.ItemsPanel is { } panel)
            {
                panel.ViewportSize = size;
                panel.Arrange(new Rect(0, 0, size.Width, size.Height));
            }

            viewer.Owner.ListArranged(size);
        }

        base.OnArranged(finalRect, changed);
    }
}
