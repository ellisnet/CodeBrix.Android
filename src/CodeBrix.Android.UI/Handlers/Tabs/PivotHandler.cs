using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation.Collections;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of Pivot (plan 3: a TabLayout header row over the pivot's content): see
/// <see cref="TabStripOverlayHandler{TElement}"/>. The template's header panel is drawn by the native strip;
/// the sections, SelectedIndex and SelectionChanged stay Core's.
/// </summary>
internal sealed class PivotHandler : TabStripOverlayHandler<Pivot>
{
    /// <summary>Pivot's mapper.</summary>
    public static readonly PropertyMapper<Pivot, PivotHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Pivot.SelectedIndexProperty] = MapSelection,
        [Pivot.SelectedItemProperty] = MapSelection,
        [ItemsControl.ItemsSourceProperty] = MapTabs,
    };

    private IObservableVector<object> _watchedItems;

    /// <summary>Creates the handler.</summary>
    public PivotHandler()
        : base(Mapper)
    {
    }

    /// <summary>True when the native row can show a Pivot (not re-templated by the application).</summary>
    internal static bool CanHandle(Pivot pivot) =>
        pivot != null && pivot.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue;

    /// <inheritdoc />
    protected override FrameworkElement FindStripPart(Pivot element) => FindDescendant<PivotHeaderPanel>(element);

    /// <inheritdoc />
    protected override IReadOnlyList<TabSpec> ReadTabs(Pivot element)
    {
        var tabs = new List<TabSpec>();
        var items = element.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var header = items[i] is PivotItem pivotItem ? pivotItem.Header : items[i];
            tabs.Add(new TabSpec(header?.ToString() ?? string.Empty, false));
        }

        return tabs;
    }

    /// <inheritdoc />
    protected override int ReadSelectedIndex(Pivot element) => element.SelectedIndex;

    /// <inheritdoc />
    protected override void SelectFromNative(Pivot element, int index) => element.SelectedIndex = index;

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Element is Pivot pivot)
        {
            _watchedItems = pivot.Items;
            _watchedItems.VectorChanged += OnItemsChanged;
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
}
