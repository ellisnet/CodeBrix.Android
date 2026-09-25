using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation.Collections;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of TabView (plan 3: TabLayout, scrollable, a close button on closable tabs, the add button
/// trailing): see <see cref="TabStripOverlayHandler{TElement}"/>. The tab row of the template (its
/// TabViewListView and add button) is drawn by the native strip; the selected tab's content, selection,
/// SelectionChanged, TabCloseRequested (Core's RequestCloseTab) and AddTabButtonClick (the seam's raise entry
/// point) stay Core's. Drag-reordering of tabs is not native in v1 (Core's template keeps it for Core input).
/// </summary>
internal sealed class TabViewHandler : TabStripOverlayHandler<TabView>
{
    /// <summary>TabView's mapper.</summary>
    public static readonly PropertyMapper<TabView, TabViewHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [TabView.SelectedIndexProperty] = MapSelection,
        [TabView.SelectedItemProperty] = MapSelection,
        [TabView.TabItemsSourceProperty] = MapTabs,
        [TabView.IsAddTabButtonVisibleProperty] = MapTabs,
    };

    private IObservableVector<object> _watchedItems;

    /// <summary>Creates the handler.</summary>
    public TabViewHandler()
        : base(Mapper)
    {
    }

    /// <summary>True when the native row can show a TabView (not re-templated by the application).</summary>
    internal static bool CanHandle(TabView tabs) =>
        tabs != null && tabs.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue;

    /// <inheritdoc />
    protected override FrameworkElement FindStripPart(TabView element)
    {
        // The row: the parent of the tab list (the template's tab container grid holds the list, the add
        // button and the header/footer slots).
        var list = FindDescendant<TabViewListView>(element);
        return (list == null ? null : Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(list) as FrameworkElement) ?? list;
    }

    /// <inheritdoc />
    protected override IReadOnlyList<TabSpec> ReadTabs(TabView element)
    {
        var tabs = new List<TabSpec>();
        var items = element.TabItems;
        var count = element.TabItemsSource is System.Collections.IEnumerable source ? Count(source) : items?.Count ?? 0;
        for (var i = 0; i < count; i++)
        {
            var container = element.ContainerFromIndex(i) as TabViewItem ?? (i < (items?.Count ?? 0) ? items[i] as TabViewItem : null);
            var header = container?.Header ?? (i < (items?.Count ?? 0) ? items[i] : null);
            tabs.Add(new TabSpec(header?.ToString() ?? string.Empty, container?.IsClosable ?? false));
        }

        return tabs;
    }

    /// <inheritdoc />
    protected override int ReadSelectedIndex(TabView element) => element.SelectedIndex;

    /// <inheritdoc />
    /// <remarks>
    /// Through the seam's TabView.RaiseSelectionChangedFromPlatform (pin 1.0.268.12): with the template's tab list it
    /// selects through the list (the normal relay), and a re-selection of the same tab raises nothing.
    /// </remarks>
    protected override void SelectFromNative(TabView element, int index) => element.RaiseSelectionChangedFromPlatform(index);

    /// <inheritdoc />
    protected override void CloseFromNative(TabView element, int index)
    {
        if (element.ContainerFromIndex(index) is TabViewItem container)
        {
            element.RequestCloseTab(container, true);
        }
    }

    /// <inheritdoc />
    protected override void AddFromNative(TabView element) => element.RaiseAddTabButtonClickFromPlatform();

    /// <inheritdoc />
    protected override bool ShowsAddButton(TabView element) => element.IsAddTabButtonVisible;

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Element is TabView tabs && tabs.TabItems is IObservableVector<object> items)
        {
            _watchedItems = items;
            items.VectorChanged += OnItemsChanged;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TabStripHostView platformView)
    {
        if (_watchedItems != null)
        {
            _watchedItems.VectorChanged -= OnItemsChanged;
            _watchedItems = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnItemsChanged(IObservableVector<object> sender, IVectorChangedEventArgs e) => PostRefresh();

    private static int Count(System.Collections.IEnumerable source)
    {
        if (source is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        var count = 0;
        foreach (var unused in source)
        {
            count++;
        }

        return count;
    }
}
