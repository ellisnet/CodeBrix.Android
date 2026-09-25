using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Drawables;
using CodeBrix.Android.UI.Platform.Recycler;
using CodeBrix.Android.UI.Platform.Recycler.Portable;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The base of the handlers that show a Core ItemsControl through a RecyclerView (ListView, GridView,
/// TreeView's list, FlipView): the control's template is not expanded (OwnsVisuals); its Core children are a
/// <see cref="ListScrollViewer"/> (the native list) holding a <see cref="RecyclerItemsPanel"/> (the control's
/// ItemsPanelRoot, holding the realised containers). Containers come from Core's own generation
/// (GetContainerForTemplate, then the items-host entry points PrepareContainerForItemsHost /
/// ReleaseContainerFromItemsHost), so ItemContainerStyle setters, the item template, selection state and the
/// item-its-own-container rule are Core's; the RecyclerView decides which items are realised and recycles the
/// containers (<see cref="RecyclerItemsEngine"/>).
/// </summary>
/// <remarks>
/// The ITEMS HOST (pin 1.0.268.12, WPE1-5): the handler has <see cref="ElementHandlerCapabilities.OwnsItemsHost"/>
/// and implements <see cref="IItemsHostHandler"/>, so Core generates no containers into an items panel, reads the
/// realised containers from <see cref="GetMaterializedContainers"/> (ContainerFromIndex / ContainerFromItem /
/// IndexFromContainer / selection), takes the visible range from the RecyclerView's layout manager, never calls the
/// items panel's layouter, and sends ListViewBase.ScrollIntoView to <see cref="Invoke"/> as a
/// <see cref="ScrollIntoViewRequest"/> (answered with <see cref="RecyclerItemsEngine.ScrollToPosition"/>; before the
/// items host Core's own ScrollIntoView went to an inert layouter and did not scroll the native list).
/// </remarks>
/// <typeparam name="TElement">The items control type.</typeparam>
internal abstract class RecyclerItemsControlHandler<TElement> : ViewGroupHandler<TElement, CodeBrixContentViewGroup>, IRecyclerItemsProvider, IListScrollOwner, IItemsHostHandler
    where TElement : ItemsControl
{
    /// <summary>The first view type of the items that are their own containers (one type per item).</summary>
    internal const int OwnContainerViewTypeBase = 1_000_000;

    private static readonly ConditionalWeakTable<ItemsPanelTemplate, ItemsLayoutSpecBox> SpecCache = new();

    private readonly Dictionary<DataTemplate, int> _templateTypes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, DataTemplate> _typeTemplates = new();
    private readonly Dictionary<object, int> _ownTypes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, object> _typeOwners = new();
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private BorderDrawable _drawable;
    private RecyclerItemsEngine _engine;
    private ListScrollViewer _scrollViewer;
    private DataTemplate _nullTemplateKey;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper.</param>
    /// <param name="commandMapper">The command mapper, or null.</param>
    protected RecyclerItemsControlHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
        _backgroundWatcher = new BrushWatcher(() => _drawable?.InvalidateBrushes());
        _borderWatcher = new BrushWatcher(() => _drawable?.InvalidateBrushes());
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsChildren
        | ElementHandlerCapabilities.OwnsItemsHost;

    /// <inheritdoc />
    public int FirstVisibleIndex => _engine?.VisibleItemRange().First ?? -1;

    /// <inheritdoc />
    public int LastVisibleIndex => _engine?.VisibleItemRange().Last ?? -1;

    /// <inheritdoc />
    public IEnumerable<DependencyObject> GetMaterializedContainers()
    {
        if (_engine?.Panel is not { } panel)
        {
            yield break;
        }

        var header = HeaderElement;
        var footer = FooterElement;
        foreach (var child in panel.Children)
        {
            if (!ReferenceEquals(child, header) && !ReferenceEquals(child, footer))
            {
                yield return child;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Called after Core's own item bookkeeping. The RecyclerView adapter follows the items collection itself (the
    /// MAUI ObservableItemsSource port: notifyItemRange* / notifyDataSetChanged), and a whole-source change is mapped
    /// (ItemsSource, ItemTemplate, ...), so nothing more is needed here; a container of a removed item is released
    /// when its ViewHolder is recycled (<see cref="UnbindItemElement"/>).
    /// </remarks>
    public void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
        if (RecyclerTrace.IsEnabled)
        {
            RecyclerTrace.Write($"items host: OnItemsChanged {(args == null ? "refresh" : args.Action.ToString())} items={ItemCount}");
        }
    }

    /// <inheritdoc />
    public override bool Invoke(string command, object args)
    {
        if (command == ElementHandlerCommands.ScrollIntoView && args is ScrollIntoViewRequest request)
        {
            var position = _engine?.PositionOfItem(request.Index) ?? -1;
            if (position < 0)
            {
                return false;
            }

            _engine.ScrollToPosition(position, request.Alignment == ScrollIntoViewAlignment.Leading);
            return true;
        }

        return base.Invoke(command, args);
    }

    /// <summary>The native machinery (null while disconnected).</summary>
    internal RecyclerItemsEngine Engine => _engine;

    /// <summary>The Core scroll viewer of the list (null until the first measure).</summary>
    internal ListScrollViewer ScrollViewer => _scrollViewer;

    /// <inheritdoc />
    public CodeBrixRecyclerView Recycler => _engine?.Recycler;

    /// <inheritdoc />
    public RecyclerItemsPanel ItemsPanel => _engine?.Panel;

    /// <summary>The default layout of the control type (when its ItemsPanel is not set).</summary>
    protected abstract ItemsLayoutSpec DefaultSpec { get; }

    /// <inheritdoc />
    public virtual int ItemCount => Element is ItemsControl control ? Math.Max(0, control.NumberOfItems) : 0;

    /// <inheritdoc />
    public virtual object ChangeSource => (Element as ItemsControl)?.Items;

    /// <inheritdoc />
    public virtual UIElement HeaderElement => null;

    /// <inheritdoc />
    public virtual UIElement FooterElement => null;

    /// <summary>Maps a property that changes which elements show the items.</summary>
    public static void MapItems(RecyclerItemsControlHandler<TElement> handler, TElement element) => handler.ResetItems();

    /// <summary>Maps ItemsPanel.</summary>
    public static void MapItemsPanel(RecyclerItemsControlHandler<TElement> handler, TElement element) =>
        handler._engine?.SetSpec(handler.ResolveSpec());

    /// <summary>Maps Padding.</summary>
    public static void MapPadding(RecyclerItemsControlHandler<TElement> handler, TElement element) =>
        handler._engine?.SetPadding(element.Padding);

    /// <summary>Maps IsEnabled: a disabled list does not scroll under a finger either.</summary>
    public static void MapIsEnabled(RecyclerItemsControlHandler<TElement> handler, TElement element)
    {
        if (handler._engine?.Recycler is { } recycler)
        {
            recycler.Enabled = element.IsEnabled;
        }
    }

    /// <summary>Maps the background and border properties.</summary>
    public static void MapBackground(RecyclerItemsControlHandler<TElement> handler, TElement element) => handler.UpdateBackground();

    /// <summary>Maps BorderThickness (background + layout).</summary>
    public static void MapBorderThickness(RecyclerItemsControlHandler<TElement> handler, TElement element)
    {
        handler.UpdateBackground();
        element.InvalidateMeasure();
    }

    /// <summary>
    /// The layout of an items panel template: a known Core panel maps to a RecyclerView layout, anything
    /// else returns null (the control then keeps Core's own template: see <see cref="CodeBrixHandlers"/>).
    /// </summary>
    /// <param name="template">The ItemsPanel (null = the control's default).</param>
    /// <param name="defaultSpec">The control's default layout.</param>
    /// <returns>The layout, or null when the panel has no RecyclerView equivalent.</returns>
    internal static ItemsLayoutSpec SpecOf(ItemsPanelTemplate template, ItemsLayoutSpec defaultSpec)
    {
        if (template == null)
        {
            return defaultSpec;
        }

        if (SpecCache.TryGetValue(template, out var cached))
        {
            return cached.Spec;
        }

        ItemsLayoutSpec spec = null;
        try
        {
            var root = template.LoadContentCachedCore(null);
            spec = root switch
            {
                ItemsStackPanel stack => new ItemsLayoutSpec(ItemsLayoutKind.Linear, stack.Orientation == Orientation.Horizontal),
                VirtualizingStackPanel stack => new ItemsLayoutSpec(ItemsLayoutKind.Linear, stack.Orientation == Orientation.Horizontal),
                StackPanel stack => new ItemsLayoutSpec(ItemsLayoutKind.Linear, stack.Orientation == Orientation.Horizontal, MainSpacing: stack.Spacing),
                ItemsWrapGrid wrap => new ItemsLayoutSpec(ItemsLayoutKind.UniformGrid, wrap.Orientation == Orientation.Vertical, wrap.ItemWidth, wrap.ItemHeight, wrap.MaximumRowsOrColumns),
                WrapGrid wrap => new ItemsLayoutSpec(ItemsLayoutKind.UniformGrid, wrap.Orientation == Orientation.Vertical, wrap.ItemWidth, wrap.ItemHeight, wrap.MaximumRowsOrColumns),
                _ => null,
            };
            if (root != null)
            {
                template.ReleaseTemplateRoot(root);
            }
        }
        catch (Exception)
        {
            spec = null;
        }

        SpecCache.AddOrUpdate(template, new ItemsLayoutSpecBox(spec));
        return spec;
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        EnsureScrollViewer();
        if (_scrollViewer == null || Element is not TElement element)
        {
            return new Size(0, 0);
        }

        var border = element.BorderThickness;
        _scrollViewer.Measure(ContentHostMath.Deflate(availableSize, border));
        if (RecyclerTrace.IsEnabled)
        {
            RecyclerTrace.Write($"measure {element.GetType().Name} available={availableSize} sv.desired={_scrollViewer.DesiredSize} svParent={Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(_scrollViewer)?.GetType().Name} panelParent={Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(_engine.Panel)?.GetType().Name} svChildren={Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(_scrollViewer)} items={ItemCount} rv={Recycler?.Width}x{Recycler?.Height} rvChildren={Recycler?.ChildCount}");
        }

        return ContentHostMath.Inflate(_scrollViewer.DesiredSize, border);
    }

    /// <inheritdoc />
    public Size MeasureList(Size availableSize) => _engine?.Measure(availableSize) ?? new Size(0, 0);

    /// <inheritdoc />
    public void ListArranged(Size size) => _engine?.Arranged(size);

    /// <inheritdoc />
    public bool ChangeView(ChangeViewRequest request) => _engine?.ChangeView(request) == true;

    /// <inheritdoc />
    public virtual object ItemAt(int index) => (Element as ItemsControl)?.ItemFromIndex(index);

    /// <inheritdoc />
    public virtual int GetItemViewType(int index)
    {
        if (Element is not ItemsControl control)
        {
            return 0;
        }

        var item = control.ItemFromIndex(index);
        if (item != null && control.IsItemItsOwnContainer(item))
        {
            if (!_ownTypes.TryGetValue(item, out var own))
            {
                own = OwnContainerViewTypeBase + _ownTypes.Count;
                _ownTypes[item] = own;
                _typeOwners[own] = item;
            }

            return own;
        }

        var template = control.ResolveItemTemplate(item) ?? (_nullTemplateKey ??= new DataTemplate());
        if (!_templateTypes.TryGetValue(template, out var type))
        {
            type = _templateTypes.Count;
            _templateTypes[template] = type;
            _typeTemplates[type] = template;
        }

        return type;
    }

    /// <inheritdoc />
    public virtual UIElement CreateItemElement(int viewType)
    {
        if (Element is not ItemsControl control)
        {
            return new Grid();
        }

        if (viewType >= OwnContainerViewTypeBase && _typeOwners.TryGetValue(viewType, out var own))
        {
            return own as UIElement ?? new Grid();
        }

        _typeTemplates.TryGetValue(viewType, out var template);
        if (ReferenceEquals(template, _nullTemplateKey))
        {
            template = null;
        }

        return control.GetContainerForTemplate(template) as UIElement ?? new ContentPresenter();
    }

    /// <inheritdoc />
    public virtual void BindItemElement(UIElement element, int viewType, int index)
    {
        if (Element is ItemsControl control && index >= 0 && index < control.NumberOfItems)
        {
            control.PrepareContainerForItemsHost(element, index);
        }
    }

    /// <inheritdoc />
    public virtual void UnbindItemElement(UIElement element, int viewType)
    {
        if (viewType < OwnContainerViewTypeBase && Element is ItemsControl control)
        {
            control.ReleaseContainerFromItemsHost(element);
        }
    }

    /// <inheritdoc />
    public virtual void DiscardItemElement(UIElement element, int viewType)
    {
        if (viewType >= OwnContainerViewTypeBase)
        {
            if (_typeOwners.Remove(viewType, out var own))
            {
                _ownTypes.Remove(own);
            }

            return;
        }

        if (Element is ItemsControl control)
        {
            control.ReleaseContainerFromItemsHost(element);
        }
    }

    /// <inheritdoc />
    public virtual void ItemElementAttached(UIElement element, int index)
    {
        // ContainerFromIndex also records the container's owner (SelectorItem.Selector reads it).
        (Element as ItemsControl)?.ContainerFromIndex(index);
    }

    /// <summary>The layout the control asks for now (its ItemsPanel, else its default).</summary>
    protected virtual ItemsLayoutSpec ResolveSpec() =>
        SpecOf((Element as ItemsControl)?.ItemsPanel, DefaultSpec) ?? DefaultSpec;

    /// <summary>The whole item set changed (source, template, container style): rebind.</summary>
    protected void ResetItems()
    {
        _templateTypes.Clear();
        _typeTemplates.Clear();
        _ownTypes.Clear();
        _typeOwners.Clear();
        _engine?.Reset();
    }

    /// <summary>Called when the native list scrolled (offsets in DIPs).</summary>
    protected virtual void OnScrolled(double horizontalOffset, double verticalOffset, bool isIntermediate) =>
        _scrollViewer?.ReportScrolled(horizontalOffset, verticalOffset, isIntermediate);

    /// <summary>Called when a drag of Core-only input ended (travel and velocity along the axis, DIPs).</summary>
    protected virtual void OnBridgedDragEnded(double travel, double velocity)
    {
    }

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(CodeBrixContentViewGroup platformView)
    {
        base.ConnectHandler(platformView);
        _engine = new RecyclerItemsEngine(VirtualElement, this, Context, ResolveSpec())
        {
            Origin = ListOrigin,
        };
        _engine.Scrolled += OnScrolled;
        _engine.Bridge.DragEnded += OnBridgedDragEnded;
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Element is TElement element)
        {
            element.ItemsPanelRoot = _engine.Panel;
            _engine.SetPadding(element.Padding);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        _backgroundWatcher.Clear();
        _borderWatcher.Clear();
        var element = Element as TElement;
        if (_engine != null)
        {
            _engine.Scrolled -= OnScrolled;
            _engine.Bridge.DragEnded -= OnBridgedDragEnded;
            _engine.Dispose();
        }

        if (element != null)
        {
            if (_scrollViewer != null)
            {
                element.RemoveChild(_scrollViewer);
                if (_engine != null)
                {
                    _scrollViewer.RemoveChild(_engine.Panel);
                }
            }

            if (_engine != null && ReferenceEquals(element.ItemsPanelRoot, _engine.Panel))
            {
                element.ItemsPanelRoot = null;
            }
        }

        _scrollViewer = null;
        _engine = null;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (_scrollViewer != null && Element is TElement element)
        {
            var border = element.BorderThickness;
            _scrollViewer.Arrange(new Rect(
                border.Left,
                border.Top,
                Math.Max(0, finalRect.Width - border.Left - border.Right),
                Math.Max(0, finalRect.Height - border.Top - border.Bottom)));
        }

        base.OnArranged(finalRect, changed);
    }

    private void EnsureScrollViewer()
    {
        if (_scrollViewer != null || _engine == null || Element is not TElement element)
        {
            return;
        }

        // The scroll viewer goes live (and connects: it owns its visuals, so Core releases whatever template it
        // had) BEFORE the panel is added under it; a child added earlier would be dropped at that release.
        _scrollViewer = new ListScrollViewer(this);
        element.AddChild(_scrollViewer, null);
        _scrollViewer.AddChild(_engine.Panel, null);
    }

    private Point ListOrigin()
    {
        var origin = ViewGroup?.AbsoluteDipOrigin ?? default;
        if (Element is TElement element)
        {
            var border = element.BorderThickness;
            origin = new Point(origin.X + border.Left, origin.Y + border.Top);
        }

        return origin;
    }

    private void UpdateBackground()
    {
        if (NativeView is not CodeBrixContentViewGroup view || Element is not TElement element)
        {
            return;
        }

        _backgroundWatcher.Watch(element.Background);
        _borderWatcher.Watch(element.BorderBrush);
        if (element.Background == null && element.BorderBrush == null)
        {
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

        _drawable.Update(element.Background, element.BorderBrush, element.BorderThickness, element.CornerRadius, element.BackgroundSizing, Density);
    }

    private sealed class ItemsLayoutSpecBox
    {
        internal ItemsLayoutSpecBox(ItemsLayoutSpec spec) => Spec = spec;

        internal ItemsLayoutSpec Spec { get; }
    }
}

/// <summary>The mapper keys shared by the RecyclerView-backed items controls.</summary>
internal static class RecyclerItemsControlMappers
{
    /// <summary>Adds the items, panel, padding and background keys to an items control mapper.</summary>
    internal static PropertyMapper<TElement, THandler> WithRecyclerItemsKeys<TElement, THandler>(this PropertyMapper<TElement, THandler> mapper)
        where TElement : ItemsControl
        where THandler : RecyclerItemsControlHandler<TElement>
    {
        mapper[ItemsControl.ItemsSourceProperty] = RecyclerItemsControlHandler<TElement>.MapItems;
        mapper[ItemsControl.ItemTemplateProperty] = RecyclerItemsControlHandler<TElement>.MapItems;
        mapper[ItemsControl.ItemTemplateSelectorProperty] = RecyclerItemsControlHandler<TElement>.MapItems;
        mapper[ItemsControl.ItemContainerStyleProperty] = RecyclerItemsControlHandler<TElement>.MapItems;
        mapper[ItemsControl.ItemContainerStyleSelectorProperty] = RecyclerItemsControlHandler<TElement>.MapItems;
        mapper[ItemsControl.DisplayMemberPathProperty] = RecyclerItemsControlHandler<TElement>.MapItems;
        mapper[ItemsControl.ItemsPanelProperty] = RecyclerItemsControlHandler<TElement>.MapItemsPanel;
        mapper[Control.PaddingProperty] = RecyclerItemsControlHandler<TElement>.MapPadding;
        mapper[Control.IsEnabledProperty] = RecyclerItemsControlHandler<TElement>.MapIsEnabled;
        mapper[Control.BackgroundProperty] = RecyclerItemsControlHandler<TElement>.MapBackground;
        mapper[Control.BorderBrushProperty] = RecyclerItemsControlHandler<TElement>.MapBackground;
        mapper[Control.BorderThicknessProperty] = RecyclerItemsControlHandler<TElement>.MapBorderThickness;
        mapper[Control.CornerRadiusProperty] = RecyclerItemsControlHandler<TElement>.MapBackground;
        mapper[Control.BackgroundSizingProperty] = RecyclerItemsControlHandler<TElement>.MapBackground;
        return mapper;
    }
}
