using System;
using CodeBrix.Android.UI.Platform.Recycler.ItemsSources;
using CodeBrix.Android.UI.Platform.Recycler.Portable;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using ARecyclerView = global::AndroidX.RecyclerView.Widget.RecyclerView;
using AGridLayoutManager = global::AndroidX.RecyclerView.Widget.GridLayoutManager;
using ALinearLayoutManager = global::AndroidX.RecyclerView.Widget.LinearLayoutManager;
using AViewGroupLayoutParams = global::Android.Views.ViewGroup.LayoutParams;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// What an items control handler supplies to a <see cref="RecyclerItemsEngine"/>: which Core element shows
/// an item and how it is prepared (Core's container generation for ListView/GridView/FlipView, the item
/// template for ItemsRepeater), plus an optional header and footer element.
/// </summary>
internal interface IRecyclerItemsProvider
{
    /// <summary>The number of items.</summary>
    int ItemCount { get; }

    /// <summary>The item at an index.</summary>
    object ItemAt(int index);

    /// <summary>The collection whose changes the list follows (INotifyCollectionChanged or IObservableVector&lt;object&gt;).</summary>
    object ChangeSource { get; }

    /// <summary>The view type of an item (0 and up; one per template, or one per item that is its own container).</summary>
    int GetItemViewType(int index);

    /// <summary>Creates the element of a view type.</summary>
    UIElement CreateItemElement(int viewType);

    /// <summary>Prepares an element for an item.</summary>
    void BindItemElement(UIElement element, int viewType, int index);

    /// <summary>Clears an element whose holder was recycled.</summary>
    void UnbindItemElement(UIElement element, int viewType);

    /// <summary>An element the pool dropped.</summary>
    void DiscardItemElement(UIElement element, int viewType);

    /// <summary>An element entered the list's panel (its holder was attached).</summary>
    void ItemElementAttached(UIElement element, int index);

    /// <summary>The header element, or null.</summary>
    UIElement HeaderElement { get; }

    /// <summary>The footer element, or null.</summary>
    UIElement FooterElement { get; }
}

/// <summary>
/// The shared native machinery of the RecyclerView-backed items controls (plan 5.1 AP3b): the
/// <see cref="CodeBrixRecyclerView"/>, its adapter over the Core collection, the layout manager chosen from
/// the items panel (<see cref="ItemsLayoutSpec"/>), uniform-grid sizing, the Core items panel that holds the
/// realised elements, the bridge for Core-only pointer input, and the scroll position reported back to Core.
/// </summary>
internal sealed class RecyclerItemsEngine : IRecyclerItemsOwner, IDisposable
{
    private readonly UIElement _owner;
    private readonly IRecyclerItemsProvider _provider;
    private ObservableItemsSource _source;
    private CorePointerScrollBridge _bridge;
    private SpacingItemDecoration _decoration;
    private ItemsLayoutSpec _spec;
    private Size? _uniformSize;
    private Size _lastViewport;
    private Thickness _padding;
    private bool _disposed;

    /// <summary>Creates the engine of an items control.</summary>
    /// <param name="owner">The Core element whose pointer events the bridge follows (the items control).</param>
    /// <param name="provider">The handler that supplies the item elements.</param>
    /// <param name="context">The context of the native views.</param>
    /// <param name="spec">The layout.</param>
    internal RecyclerItemsEngine(UIElement owner, IRecyclerItemsProvider provider, AContext context, ItemsLayoutSpec spec)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _spec = spec ?? ItemsLayoutSpec.VerticalList;
        Panel = new RecyclerItemsPanel();
        Recycler = new CodeBrixRecyclerView(context);
        Adapter = new CoreItemsAdapter(this);
        Recycler.SetItemViewCacheSize(0);
        Recycler.SetItemAnimator(null);
        Recycler.SetRecycledViewPool(new CoreItemsViewPool(Adapter, 12));
        Recycler.Scrolled += OnRecyclerScrolled;
        Adapter.DataChanged += OnDataChanged;
        ApplyLayoutManager();
        RebuildSource();
        Recycler.SetAdapter(Adapter);
        _bridge = new CorePointerScrollBridge(owner, Recycler) { IsHorizontal = _spec.ScrollsHorizontally };
    }

    /// <summary>The native list.</summary>
    internal CodeBrixRecyclerView Recycler { get; }

    /// <summary>The adapter.</summary>
    internal CoreItemsAdapter Adapter { get; }

    /// <summary>The bridge for Core-only pointer input.</summary>
    internal CorePointerScrollBridge Bridge => _bridge;

    /// <summary>The current layout.</summary>
    internal ItemsLayoutSpec Spec => _spec;

    /// <inheritdoc />
    public RecyclerItemsPanel Panel { get; }

    /// <inheritdoc />
    public IItemsViewSource Source => _source;

    /// <summary>The origin of the list in window DIPs (set by the owner; used for pixel rounding).</summary>
    internal Func<Point> Origin { get; set; }

    /// <summary>Raised when the scroll position changed: offsets in DIPs and whether more changes follow.</summary>
    internal event Action<double, double, bool> Scrolled;

    /// <summary>The horizontal scroll offset in DIPs.</summary>
    internal double HorizontalOffset => LayoutReplayMath.FromPixels(Recycler.OffsetX, Density);

    /// <summary>The vertical scroll offset in DIPs.</summary>
    internal double VerticalOffset => LayoutReplayMath.FromPixels(Recycler.OffsetY, Density);

    private double Density => HandlerContext.Density(_owner);

    /// <summary>Changes the layout (the items panel or the repeater layout changed).</summary>
    internal void SetSpec(ItemsLayoutSpec spec)
    {
        spec ??= ItemsLayoutSpec.VerticalList;
        if (spec == _spec)
        {
            return;
        }

        _spec = spec;
        _uniformSize = null;
        if (_bridge != null)
        {
            _bridge.IsHorizontal = spec.ScrollsHorizontally;
        }

        ApplyLayoutManager();
        Adapter.NotifyDataSetChanged();
    }

    /// <summary>The items (source, template, own containers) changed as a whole: rebind everything.</summary>
    internal void Reset()
    {
        RebuildSource();
        _uniformSize = null;
        Adapter.NotifyDataSetChanged();
        Recycler.ScrollToPosition(0);
        Recycler.ResetOffsets();
    }

    /// <summary>The header or footer appeared or disappeared.</summary>
    internal void RefreshHeaderFooter()
    {
        if (_source == null)
        {
            return;
        }

        var hasHeader = _provider.HeaderElement != null;
        var hasFooter = _provider.FooterElement != null;
        if (hasHeader != _source.HasHeader || hasFooter != _source.HasFooter)
        {
            _source.HasHeader = hasHeader;
            _source.HasFooter = hasFooter;
            Adapter.NotifyDataSetChanged();
        }
    }

    /// <summary>The list's padding (DIPs): applied to the native list (content scrolls under it).</summary>
    internal void SetPadding(Thickness padding)
    {
        _padding = padding;
        ApplyPadding();
    }

    /// <summary>Measures the native list for Core (DIPs), the way a ScrollViewer around the items panel would size.</summary>
    internal Size Measure(Size available)
    {
        var density = Density;
        _lastViewport = available;
        UpdateUniformGrid(available);
        var widthSpec = CreateSpec(available.Width, density);
        var heightSpec = CreateSpec(available.Height, density);
        if (_spec.Kind == ItemsLayoutKind.Pager)
        {
            // A pager fills what it is given (a FlipView has no natural size of its own).
            return new Size(Finite(available.Width), Finite(available.Height));
        }

        Recycler.NestedScrollingEnabled = !(double.IsInfinity(available.Height) || double.IsInfinity(available.Width));
        Recycler.Measure(widthSpec, heightSpec);
        return new Size(
            LayoutReplayMath.FromPixels(Recycler.MeasuredWidth, density),
            LayoutReplayMath.FromPixels(Recycler.MeasuredHeight, density));
    }

    /// <summary>The list was arranged at a size (DIPs).</summary>
    internal void Arranged(Size size)
    {
        Panel.ViewportSize = size;
        if (_spec.Kind == ItemsLayoutKind.UniformGrid && size != _lastViewport)
        {
            _lastViewport = size;
            UpdateUniformGrid(size);
        }
    }

    /// <summary>The seam's ChangeView: scrolls to the requested offsets (DIPs), animated unless asked not to.</summary>
    internal bool ChangeView(ChangeViewRequest request)
    {
        if (request == null)
        {
            return false;
        }

        var density = Density;
        var dx = request.HorizontalOffset is { } h ? (int)Math.Round(h * density) - Recycler.OffsetX : 0;
        var dy = request.VerticalOffset is { } v ? (int)Math.Round(v * density) - Recycler.OffsetY : 0;
        if (request.DisableAnimation)
        {
            Recycler.ScrollByPixels(dx, dy);
        }
        else
        {
            Recycler.SmoothScrollBy(dx, dy);
        }

        return true;
    }

    /// <summary>The adapter position of an item index (the header, when there is one, is position 0).</summary>
    /// <param name="itemIndex">The item's index in the control's items.</param>
    /// <returns>The adapter position, or -1 for an index outside the items.</returns>
    internal int PositionOfItem(int itemIndex)
    {
        if (_source == null || itemIndex < 0 || itemIndex >= _provider.ItemCount)
        {
            return -1;
        }

        return itemIndex + (_source.HasHeader ? 1 : 0);
    }

    /// <summary>
    /// The item indexes the RecyclerView shows now, at least partly (the header and footer excluded); (-1, -1) when
    /// no item is laid out (the items host's FirstVisibleIndex / LastVisibleIndex, WPE1-5).
    /// </summary>
    internal (int First, int Last) VisibleItemRange()
    {
        if (_source == null || Recycler.GetLayoutManager() is not ALinearLayoutManager linear)
        {
            return (-1, -1);
        }

        var first = linear.FindFirstVisibleItemPosition();
        var last = linear.FindLastVisibleItemPosition();
        if (first < 0 || last < 0)
        {
            return (-1, -1);
        }

        var count = _provider.ItemCount;
        var firstItem = Math.Max(0, _source.ToItemIndex(first));
        var lastItem = Math.Min(count - 1, _source.ToItemIndex(last));
        return firstItem <= lastItem ? (firstItem, lastItem) : (-1, -1);
    }

    /// <summary>Brings an adapter position into view (ScrollIntoView), aligned to the start when asked.</summary>
    internal void ScrollToPosition(int position, bool alignStart)
    {
        if (position < 0 || position >= Adapter.ItemCount)
        {
            return;
        }

        if (alignStart && Recycler.GetLayoutManager() is ALinearLayoutManager linear)
        {
            linear.ScrollToPositionWithOffset(position, 0);
        }
        else
        {
            Recycler.ScrollToPosition(position);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bridge?.Dispose();
        _bridge = null;
        Recycler.Scrolled -= OnRecyclerScrolled;
        Adapter.DataChanged -= OnDataChanged;
        _source?.Dispose();
        _source = null;
        Recycler.SetAdapter(null);
    }

    /// <inheritdoc />
    public int GetViewType(int position)
    {
        if (_source == null)
        {
            return 0;
        }

        if (_source.IsHeader(position))
        {
            return CoreItemsAdapter.HeaderViewType;
        }

        if (_source.IsFooter(position))
        {
            return CoreItemsAdapter.FooterViewType;
        }

        return _provider.GetItemViewType(_source.ToItemIndex(position));
    }

    /// <inheritdoc />
    public UIElement CreateElement(int viewType) => viewType switch
    {
        CoreItemsAdapter.HeaderViewType => _provider.HeaderElement,
        CoreItemsAdapter.FooterViewType => _provider.FooterElement,
        _ => _provider.CreateItemElement(viewType),
    };

    /// <inheritdoc />
    public void BindElement(UIElement element, int viewType, int position)
    {
        if (viewType < 0 || _source == null || element == null)
        {
            return;
        }

        _provider.BindItemElement(element, viewType, _source.ToItemIndex(position));
    }

    /// <inheritdoc />
    public void UnbindElement(UIElement element, int viewType)
    {
        if (viewType >= 0 && element != null)
        {
            _provider.UnbindItemElement(element, viewType);
        }
    }

    /// <inheritdoc />
    public void DiscardElement(UIElement element, int viewType)
    {
        if (viewType >= 0 && element != null)
        {
            _provider.DiscardItemElement(element, viewType);
        }
    }

    /// <inheritdoc />
    public void OnElementAttached(UIElement element, int position)
    {
        if (_source == null || position < 0 || _source.IsHeader(position) || _source.IsFooter(position))
        {
            return;
        }

        _provider.ItemElementAttached(element, _source.ToItemIndex(position));
        if (_spec.Kind == ItemsLayoutKind.UniformGrid && _uniformSize == null)
        {
            // The first realised item sets the cell size (ItemsWrapGrid / UniformGridLayout).
            Recycler.Post(() => UpdateUniformGrid(_lastViewport));
        }
    }

    /// <inheritdoc />
    public ARecyclerView.LayoutParams CreateLayoutParams(int viewType)
    {
        const int Match = AViewGroupLayoutParams.MatchParent;
        const int Wrap = AViewGroupLayoutParams.WrapContent;
        if (viewType < 0)
        {
            // Header and footer span the list across.
            return _spec.ScrollsHorizontally ? new ARecyclerView.LayoutParams(Wrap, Match) : new ARecyclerView.LayoutParams(Match, Wrap);
        }

        return _spec.Kind switch
        {
            ItemsLayoutKind.Pager => new ARecyclerView.LayoutParams(Match, Match),
            ItemsLayoutKind.UniformGrid => new ARecyclerView.LayoutParams(Wrap, Wrap),
            _ => _spec.ScrollsHorizontally ? new ARecyclerView.LayoutParams(Wrap, Match) : new ARecyclerView.LayoutParams(Match, Wrap),
        };
    }

    /// <inheritdoc />
    public Size? FixedItemSize(int viewType)
    {
        if (viewType < 0)
        {
            return null;
        }

        if (_spec.Kind == ItemsLayoutKind.UniformGrid)
        {
            return _uniformSize;
        }

        return null;
    }

    /// <inheritdoc />
    public Point RecyclerOrigin() => Origin?.Invoke() ?? default;

    private void RebuildSource()
    {
        var hasHeader = _provider.HeaderElement != null;
        var hasFooter = _provider.FooterElement != null;
        _source?.Dispose();
        _source = new ObservableItemsSource(() => _provider.ItemCount, _provider.ItemAt, _provider.ChangeSource, Adapter)
        {
            HasHeader = hasHeader,
            HasFooter = hasFooter,
        };
    }

    private void OnDataChanged()
    {
        Panel.DrainLayouter();
        if (_spec.Kind == ItemsLayoutKind.UniformGrid && _provider.ItemCount == 0)
        {
            _uniformSize = null;
        }
    }

    private void OnRecyclerScrolled(int x, int y, bool isIntermediate)
    {
        var density = Density;
        Scrolled?.Invoke(LayoutReplayMath.FromPixels(x, density), LayoutReplayMath.FromPixels(y, density), isIntermediate);
    }

    private void ApplyLayoutManager()
    {
        var context = Recycler.Context;
        var orientation = _spec.ScrollsHorizontally ? ALinearLayoutManager.Horizontal : ALinearLayoutManager.Vertical;
        ARecyclerView.LayoutManager manager = _spec.Kind switch
        {
            ItemsLayoutKind.UniformGrid => new CachingGridLayoutManager(context, 1, orientation),
            ItemsLayoutKind.Pager => new ALinearLayoutManager(context, orientation, false),
            _ => new CachingLinearLayoutManager(context, orientation),
        };
        if (manager is ALinearLayoutManager linear)
        {
            // Prefetched holders would be bound (and their Core elements prepared) without being shown.
            linear.ItemPrefetchEnabled = false;
        }

        Recycler.SetLayoutManager(manager);
        if (_decoration != null)
        {
            Recycler.RemoveItemDecoration(_decoration);
            _decoration = null;
        }

        var spacingPx = LayoutReplayMath.ToPixels(_spec.MainSpacing, Density);
        if (spacingPx > 0)
        {
            _decoration = new SpacingItemDecoration(spacingPx, _spec.ScrollsHorizontally);
            Recycler.AddItemDecoration(_decoration);
        }

        if (_spec.Kind == ItemsLayoutKind.Pager)
        {
            Recycler.SetOnFlingListener(null);
            new global::AndroidX.RecyclerView.Widget.PagerSnapHelper().AttachToRecyclerView(Recycler);
        }

        ApplyPadding();
    }

    private void UpdateUniformGrid(Size available)
    {
        if (_spec.Kind != ItemsLayoutKind.UniformGrid || Recycler.GetLayoutManager() is not AGridLayoutManager grid)
        {
            return;
        }

        var size = UniformItemSize();
        if (size == null)
        {
            ApplyPadding();
            return;
        }

        var horizontal = _spec.ScrollsHorizontally;
        var across = horizontal ? available.Height - _padding.Top - _padding.Bottom : available.Width - _padding.Left - _padding.Right;
        var itemAcross = horizontal ? size.Value.Height : size.Value.Width;
        var span = UniformGridMath.SpanCount(across, itemAcross, _spec.CrossSpacing, _spec.MaximumLines);
        if (grid.SpanCount != span)
        {
            grid.SpanCount = span;
        }

        var extra = Math.Max(0, UniformGridMath.ExtraEndPadding(across, itemAcross, _spec.CrossSpacing, span) - Math.Max(0, _spec.CrossSpacing));
        ApplyPadding(extra);
    }

    private Size? UniformItemSize()
    {
        if (_uniformSize is { } known)
        {
            return known;
        }

        var width = _spec.ItemWidth;
        var height = _spec.ItemHeight;
        if (double.IsFinite(width) && width > 0 && double.IsFinite(height) && height > 0)
        {
            _uniformSize = new Size(width, height);
            return _uniformSize;
        }

        // The first realised item's desired size (measured unconstrained), as ItemsWrapGrid does.
        UIElement first = null;
        foreach (var child in Panel.Children)
        {
            first = child;
            break;
        }

        if (first == null)
        {
            return null;
        }

        first.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = first.DesiredSize;
        if (desired.Width <= 0 || desired.Height <= 0)
        {
            return null;
        }

        _uniformSize = new Size(
            double.IsFinite(width) && width > 0 ? width : desired.Width,
            double.IsFinite(height) && height > 0 ? height : desired.Height);
        Recycler.Post(() =>
        {
            for (var i = 0; i < Recycler.ChildCount; i++)
            {
                if (Recycler.GetChildAt(i) is RecyclerItemHost host && host.FixedSize == null)
                {
                    host.FixedSize = _uniformSize;
                    host.RequestLayout();
                }
            }

            Recycler.RequestLayout();
        });
        return _uniformSize;
    }

    private void ApplyPadding(double extraEnd = 0)
    {
        var density = Density;
        var left = LayoutReplayMath.ToPixels(_padding.Left, density);
        var top = LayoutReplayMath.ToPixels(_padding.Top, density);
        var right = LayoutReplayMath.ToPixels(_padding.Right, density);
        var bottom = LayoutReplayMath.ToPixels(_padding.Bottom, density);
        var extra = (int)Math.Floor(Math.Max(0, extraEnd) * density);
        if (_spec.ScrollsHorizontally)
        {
            bottom += extra;
        }
        else
        {
            right += extra;
        }

        if (Recycler.PaddingLeft != left || Recycler.PaddingTop != top || Recycler.PaddingRight != right || Recycler.PaddingBottom != bottom)
        {
            Recycler.SetPadding(left, top, right, bottom);
        }
    }

    private static int CreateSpec(double dips, double density)
    {
        if (double.IsNaN(dips) || double.IsInfinity(dips))
        {
            return AMeasureSpecMode.Unspecified.MakeMeasureSpec(0);
        }

        return AMeasureSpecMode.AtMost.MakeMeasureSpec((int)Math.Floor(Math.Round(Math.Max(0, dips) * density, 3)));
    }

    private static double Finite(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
}

/// <summary>
/// A LinearLayoutManager that keeps one viewport of items realised before and after the visible ones (WinUI's
/// ItemsStackPanel keeps a cache buffer too): an item that just scrolled out still has its container.
/// </summary>
internal sealed class CachingLinearLayoutManager : ALinearLayoutManager
{
    /// <summary>Creates the layout manager.</summary>
    internal CachingLinearLayoutManager(AContext context, int orientation)
        : base(context, orientation, false)
    {
    }

    /// <inheritdoc />
    protected override void CalculateExtraLayoutSpace(ARecyclerView.State state, int[] extraLayoutSpace) =>
        CachingLayout.Fill(this, extraLayoutSpace);
}

/// <summary>The grid counterpart of <see cref="CachingLinearLayoutManager"/> (ItemsWrapGrid's cache buffer).</summary>
internal sealed class CachingGridLayoutManager : AGridLayoutManager
{
    /// <summary>Creates the layout manager.</summary>
    internal CachingGridLayoutManager(AContext context, int spanCount, int orientation)
        : base(context, spanCount, orientation, false)
    {
    }

    /// <inheritdoc />
    protected override void CalculateExtraLayoutSpace(ARecyclerView.State state, int[] extraLayoutSpace) =>
        CachingLayout.Fill(this, extraLayoutSpace);
}

/// <summary>The cache extent of the caching layout managers.</summary>
internal static class CachingLayout
{
    /// <summary>One viewport before and after.</summary>
    internal static void Fill(ALinearLayoutManager manager, int[] extra)
    {
        if (extra == null || extra.Length < 2)
        {
            return;
        }

        var extent = manager.Orientation == ALinearLayoutManager.Horizontal
            ? manager.Width - manager.PaddingLeft - manager.PaddingRight
            : manager.Height - manager.PaddingTop - manager.PaddingBottom;
        extent = Math.Max(0, extent);
        extra[0] = extent;
        extra[1] = extent;
    }
}
