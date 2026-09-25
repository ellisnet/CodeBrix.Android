using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// The Core side of a RecyclerView-backed list: the panel whose children are the item containers the
/// RecyclerView shows RIGHT NOW (one per attached ViewHolder). It is the list's ItemsPanelRoot, so Core's
/// own container lookups (ContainerFromIndex / ContainerFromItem / IndexFromContainer), selection
/// updates (SelectorItem.IsSelected) and index repair after collection changes find the containers the
/// RecyclerView realised. Layout is the RecyclerView's: each child is measured with the constraint its
/// item host last used and arranged where the host view sits in the RecyclerView (DIPs, relative to
/// the panel = the RecyclerView's top-left), so Core's hit testing and TransformToVisual agree with what
/// is on screen.
/// </summary>
/// <remarks>
/// The panel is a Core VirtualizingPanel (IVirtualizingPanel; FlipView sizes its containers from a
/// VirtualizingPanel's available size) so that Core's Selector does not treat it as a
/// non-virtualizing panel to fill with a container for EVERY item (Selector.ShouldItemsControlManageChildren).
/// Core then talks to the panel's layouter for collection changes (AddItems/RemoveItems/Refresh), paging and
/// the slow ScrollIntoView path: GetLayouter() returns Core's ItemsStackPanelLayout initialized on
/// a detached panel that never loads, so those calls stay inert (the RecyclerView adapter follows the
/// collection itself); <see cref="DrainLayouter"/> clears the change queue such calls leave behind. A Core
/// seam for this (an items-host capability) is requested in the AP3b report.
/// </remarks>
internal sealed class RecyclerItemsPanel : VirtualizingPanel
{
    private readonly VirtualizingPanelLayout _inertLayouter;
    private readonly Dictionary<UIElement, Size> _constraints = new();
    private readonly Dictionary<UIElement, Rect> _rects = new();
    private readonly Dictionary<UIElement, RecyclerItemHost> _hosts = new();

    /// <summary>Creates the panel.</summary>
    internal RecyclerItemsPanel()
    {
        var layouter = new ItemsStackPanelLayout();
        layouter.Initialize(new StackPanel());
        _inertLayouter = layouter;
    }

    /// <summary>The size the panel reports and fills (the RecyclerView's viewport, in DIPs).</summary>
    internal Size ViewportSize { get; set; }

    /// <inheritdoc />
    private protected override VirtualizingPanelLayout GetLayouterCore() => _inertLayouter;

    /// <summary>Clears the collection changes Core queued on the inert layouter.</summary>
    internal void DrainLayouter()
    {
        try
        {
            _inertLayouter.Refresh();
        }
        catch (Exception)
        {
            // The inert layouter owns nothing; a failure here must never break the list.
        }
    }

    /// <summary>Records the constraint a child was last measured with by its item host.</summary>
    internal void SetConstraint(UIElement child, Size constraint) => _constraints[child] = constraint;

    /// <summary>Records where a child's host view sits (DIPs, panel coordinates).</summary>
    internal void SetRect(UIElement child, Rect rect) => _rects[child] = rect;

    /// <summary>Forgets a child's constraint and rectangle (it left the panel).</summary>
    internal void Forget(UIElement child)
    {
        _constraints.Remove(child);
        _rects.Remove(child);
    }

    /// <summary>Records which item host shows an element (the host adopts the element's native view).</summary>
    internal void SetHost(UIElement element, RecyclerItemHost host)
    {
        if (host == null)
        {
            _hosts.Remove(element);
        }
        else
        {
            _hosts[element] = host;
        }
    }

    /// <summary>The item host that shows an element, or null.</summary>
    internal RecyclerItemHost HostOf(UIElement element) =>
        element != null && _hosts.TryGetValue(element, out var host) ? host : null;

    /// <summary>The last rectangle recorded for a child, if any.</summary>
    internal bool TryGetRect(UIElement child, out Rect rect) => _rects.TryGetValue(child, out rect);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            if (_constraints.TryGetValue(child, out var constraint))
            {
                child.Measure(constraint);
            }
        }

        return Finite(ViewportSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (_rects.TryGetValue(child, out var rect))
            {
                child.Arrange(rect);
            }
        }

        return finalSize;
    }

    private static Size Finite(Size size) => new(
        double.IsFinite(size.Width) ? Math.Max(0, size.Width) : 0,
        double.IsFinite(size.Height) ? Math.Max(0, size.Height) : 0);
}
