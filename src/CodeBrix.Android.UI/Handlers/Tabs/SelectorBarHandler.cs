using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation.Collections;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of SelectorBar (plan 3: a TabLayout row): see <see cref="TabStripOverlayHandler{TElement}"/>.
/// The template's items row is drawn by the native strip; SelectedItem and SelectionChanged stay Core's.
/// </summary>
internal sealed class SelectorBarHandler : TabStripOverlayHandler<SelectorBar>
{
    /// <summary>SelectorBar's mapper.</summary>
    public static readonly PropertyMapper<SelectorBar, SelectorBarHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [SelectorBar.SelectedItemProperty] = MapSelection,
    };

    private IObservableVector<SelectorBarItem> _watchedItems;

    /// <summary>Creates the handler.</summary>
    public SelectorBarHandler()
        : base(Mapper)
    {
    }

    /// <summary>True when the native row can show a SelectorBar (not re-templated by the application).</summary>
    internal static bool CanHandle(SelectorBar bar) =>
        bar != null && bar.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue;

    /// <inheritdoc />
    protected override FrameworkElement FindStripPart(SelectorBar element) =>
        FindDescendant<ItemsView>(element) ?? (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element) > 0
            ? Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, 0) as FrameworkElement
            : null);

    /// <inheritdoc />
    protected override IReadOnlyList<TabSpec> ReadTabs(SelectorBar element)
    {
        var tabs = new List<TabSpec>();
        foreach (var item in element.Items)
        {
            tabs.Add(new TabSpec(item?.Text ?? string.Empty, false));
        }

        return tabs;
    }

    /// <inheritdoc />
    protected override int ReadSelectedIndex(SelectorBar element) =>
        element.SelectedItem is { } selected ? element.Items.IndexOf(selected) : -1;

    /// <inheritdoc />
    protected override void SelectFromNative(SelectorBar element, int index)
    {
        if (index >= 0 && index < element.Items.Count)
        {
            element.SelectedItem = element.Items[index];
        }
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Element is SelectorBar bar && bar.Items is IObservableVector<SelectorBarItem> items)
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

    private void OnItemsChanged(IObservableVector<SelectorBarItem> sender, IVectorChangedEventArgs e) => PostRefresh();
}
