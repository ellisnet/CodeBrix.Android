using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Recycler;
using CodeBrix.Android.UI.Platform.Recycler.Portable;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of ItemsRepeater (plan 3: RecyclerView; StackLayout -> LinearLayoutManager, UniformGridLayout
/// -> GridLayoutManager): the repeater's view is a <see cref="CodeBrixRecyclerView"/> whose items are the
/// elements the repeater's ItemTemplate makes (a DataTemplate or a DataTemplateSelector; an item that is a
/// UIElement is its own element; any other item without a template shows its text), with the item as
/// DataContext. Core does not run the repeater's own virtualization (MeasuresNatively): the realised
/// elements are children of a <see cref="RecyclerItemsPanel"/> under the repeater. Inside a ScrollViewer the
/// repeater is measured unconstrained and shows every item (the outer ScrollViewer scrolls - the corpus'
/// lazy-fill pattern); with a finite size the native list virtualizes and scrolls itself.
/// </summary>
internal sealed class ItemsRepeaterHandler : ViewHandler<ItemsRepeater, CodeBrixRecyclerView>, IRecyclerItemsProvider
{
    /// <summary>The view type of text items shown without a template.</summary>
    internal const int TextViewType = 999_999;

    /// <summary>The first view type of the items that are their own elements.</summary>
    internal const int OwnElementViewTypeBase = 1_000_000;

    /// <summary>ItemsRepeater's mapper.</summary>
    public static readonly PropertyMapper<ItemsRepeater, ItemsRepeaterHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ItemsRepeater.ItemsSourceProperty] = (h, e) => h.ResetItems(),
        [ItemsRepeater.ItemTemplateProperty] = (h, e) => h.ResetItems(),
        [ItemsRepeater.LayoutProperty] = (h, e) => h._engine?.SetSpec(SpecOf(e.Layout)),
    };

    private readonly Dictionary<DataTemplate, int> _templateTypes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, DataTemplate> _typeTemplates = new();
    private readonly Dictionary<object, int> _ownTypes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, object> _typeOwners = new();
    private RecyclerItemsEngine _engine;
    private bool _panelAdded;

    /// <summary>Creates the handler.</summary>
    public ItemsRepeaterHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsChildren;

    /// <inheritdoc />
    public int ItemCount => Element is ItemsRepeater repeater ? repeater.ItemsSourceView?.Count ?? 0 : 0;

    /// <inheritdoc />
    public object ChangeSource => (Element as ItemsRepeater)?.ItemsSourceView;

    /// <inheritdoc />
    public UIElement HeaderElement => null;

    /// <inheritdoc />
    public UIElement FooterElement => null;

    /// <summary>
    /// The native layout of a repeater layout: StackLayout and UniformGridLayout (null = StackLayout), or
    /// null for any other layout (the repeater then keeps Core's own layout: see <see cref="CodeBrixHandlers"/>).
    /// </summary>
    internal static ItemsLayoutSpec SpecOf(Layout layout) => layout switch
    {
        null => ItemsLayoutSpec.VerticalList,
        StackLayout stack => new ItemsLayoutSpec(ItemsLayoutKind.Linear, stack.Orientation == Orientation.Horizontal, MainSpacing: stack.Spacing),
        UniformGridLayout grid => new ItemsLayoutSpec(
            ItemsLayoutKind.UniformGrid,
            grid.Orientation == Orientation.Vertical,
            grid.MinItemWidth > 0 ? grid.MinItemWidth : double.NaN,
            grid.MinItemHeight > 0 ? grid.MinItemHeight : double.NaN,
            grid.MaximumRowsOrColumns > 0 ? grid.MaximumRowsOrColumns : 0,
            grid.Orientation == Orientation.Vertical ? grid.MinColumnSpacing : grid.MinRowSpacing,
            grid.Orientation == Orientation.Vertical ? grid.MinRowSpacing : grid.MinColumnSpacing),
        _ => null,
    };

    /// <summary>True when the native list can show a repeater (a StackLayout or UniformGridLayout, a template the list can instantiate).</summary>
    /// <remarks>
    /// A repeater that is a part of another control's template (ItemsView, SelectorBar, NavigationView...) keeps
    /// Core's own realisation: those controls drive their items through the repeater's Core API
    /// (ElementPrepared, TryGetElement, the selection model).
    /// </remarks>
    internal static bool CanHandle(ItemsRepeater repeater) =>
        repeater != null
        && repeater.TemplatedParent == null
        && !IsInsideCoreControl(repeater)
        && SpecOf(repeater.Layout) != null
        && repeater.ItemTemplate is null or DataTemplate or DataTemplateSelector;

    /// <summary>
    /// True when the repeater sits inside the template of a Core control that drives its items through the
    /// repeater (before the nearest page or user control).
    /// </summary>
    private static bool IsInsideCoreControl(UIElement element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is Page or UserControl)
            {
                return false;
            }

            if (parent is Control control && RepeaterHosts.Contains(control.GetType().Name) && control.GetType().Assembly == typeof(Control).Assembly)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The Core controls whose templates realise their items through an ItemsRepeater.</summary>
    private static readonly System.Collections.Generic.HashSet<string> RepeaterHosts = new(StringComparer.Ordinal)
    {
        "ItemsView", "SelectorBar", "NavigationView", "NavigationViewItem", "BreadcrumbBar", "PipsPager", "TabView",
        "RadioButtons", "RatingControl", "ColorPicker", "AnnotatedScrollBar", "SwipeControl", "TeachingTip", "InfoBar",
        "MenuBar", "CommandBarFlyout", "TreeView", "ItemsRepeaterScrollHost",
    };

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (_engine == null || Element is not ItemsRepeater repeater)
        {
            return new Size(0, 0);
        }

        if (!_panelAdded)
        {
            _panelAdded = true;
            repeater.AddChild(_engine.Panel, null);
        }

        var size = _engine.Measure(availableSize);
        if (repeater.Layout is UniformGridLayout { ItemsStretch: UniformGridLayoutItemsStretch.None } grid
            && _engine.UniformCrossExtent(availableSize) is { } across)
        {
            // [AP8-S batch 2] Core's UniformGridLayout extent across the lines (span x (item + spacing) - spacing),
            // not the native list's wrap size: the wrap size depends on the cell width of the LAST layout, so a
            // centred repeater re-arranged at it lost a column (3 -> 2 in a 732-DIP catalog after a scroll).
            size = grid.Orientation == Orientation.Vertical ? new Size(size.Width, across) : new Size(across, size.Height);
        }

        _engine.Panel.ViewportSize = size;
        _engine.Panel.Measure(size);
        return size;
    }

    /// <inheritdoc />
    public object ItemAt(int index)
    {
        var view = (Element as ItemsRepeater)?.ItemsSourceView;
        return view != null && index >= 0 && index < view.Count ? view.GetAt(index) : null;
    }

    /// <inheritdoc />
    public int GetItemViewType(int index)
    {
        var item = ItemAt(index);
        var template = TemplateFor(item);
        if (template == null)
        {
            if (item is UIElement)
            {
                if (!_ownTypes.TryGetValue(item, out var own))
                {
                    own = OwnElementViewTypeBase + _ownTypes.Count;
                    _ownTypes[item] = own;
                    _typeOwners[own] = item;
                }

                return own;
            }

            return TextViewType;
        }

        if (!_templateTypes.TryGetValue(template, out var type))
        {
            type = _templateTypes.Count;
            _templateTypes[template] = type;
            _typeTemplates[type] = template;
        }

        return type;
    }

    /// <inheritdoc />
    public UIElement CreateItemElement(int viewType)
    {
        if (viewType >= OwnElementViewTypeBase)
        {
            return _typeOwners.TryGetValue(viewType, out var own) && own is UIElement element ? element : new Grid();
        }

        if (viewType == TextViewType)
        {
            return new TextBlock();
        }

        return _typeTemplates.TryGetValue(viewType, out var template) && template.LoadContent() is UIElement root
            ? root
            : new Grid();
    }

    /// <inheritdoc />
    public void BindItemElement(UIElement element, int viewType, int index)
    {
        var item = ItemAt(index);
        if (viewType >= OwnElementViewTypeBase)
        {
            return;
        }

        if (element is FrameworkElement framework)
        {
            framework.DataContext = item;
        }

        if (viewType == TextViewType && element is TextBlock text)
        {
            text.Text = item?.ToString() ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public void UnbindItemElement(UIElement element, int viewType)
    {
    }

    /// <inheritdoc />
    public void DiscardItemElement(UIElement element, int viewType)
    {
        if (viewType >= OwnElementViewTypeBase && _typeOwners.Remove(viewType, out var own))
        {
            _ownTypes.Remove(own);
        }
    }

    /// <inheritdoc />
    public void ItemElementAttached(UIElement element, int index)
    {
    }

    /// <inheritdoc />
    protected override CodeBrixRecyclerView CreatePlatformView()
    {
        _engine = new RecyclerItemsEngine(VirtualElement, this, Context, SpecOf(VirtualElement.Layout) ?? ItemsLayoutSpec.VerticalList)
        {
            Origin = Origin,
        };
        return _engine.Recycler;
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (_engine != null)
        {
            var size = new Size(finalRect.Width, finalRect.Height);
            _engine.Panel.ViewportSize = size;
            _engine.Panel.Arrange(new Rect(0, 0, size.Width, size.Height));
            _engine.Arranged(size);
        }

        base.OnArranged(finalRect, changed);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixRecyclerView platformView)
    {
        if (Element is ItemsRepeater repeater && _engine != null && _panelAdded)
        {
            repeater.RemoveChild(_engine.Panel);
        }

        _panelAdded = false;
        _engine?.Dispose();
        _engine = null;
        base.DisconnectHandler(platformView);
    }

    private DataTemplate TemplateFor(object item) => (Element as ItemsRepeater)?.ItemTemplate switch
    {
        DataTemplate template => template,
        DataTemplateSelector selector => selector.SelectTemplate(item),
        _ => null,
    };

    private void ResetItems()
    {
        _templateTypes.Clear();
        _typeTemplates.Clear();
        _ownTypes.Clear();
        _typeOwners.Clear();
        _engine?.Reset();
    }

    private Point Origin()
    {
        var parent = NativeView?.Parent as CodeBrixViewGroup;
        var origin = parent?.AbsoluteDipOrigin ?? default;
        return HasArranged ? new Point(origin.X + ArrangedRect.X, origin.Y + ArrangedRect.Y) : origin;
    }
}
