using System;
using CodeBrix.Android.UI.Platform.Recycler.Portable;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of ListView, GridView and TreeView's list (plan 3: RecyclerView + LinearLayoutManager /
/// GridLayoutManager): see <see cref="RecyclerItemsControlHandler{TElement}"/>. Selection, ItemClick,
/// SelectionMode (single / multiple / extended), IsItemClickEnabled and the selection look of the
/// containers stay Core's (the containers keep their Fluent template, drawn natively); what is native is
/// the list: virtualization, recycling, scrolling, fling and overscroll. Header and Footer are the first
/// and last positions of the list (ContentControls with HeaderTemplate / FooterTemplate).
/// </summary>
internal sealed class ListViewBaseHandler : RecyclerItemsControlHandler<ListViewBase>
{
    /// <summary>ListViewBase's mapper.</summary>
    public static readonly PropertyMapper<ListViewBase, ListViewBaseHandler> Mapper = new PropertyMapper<ListViewBase, ListViewBaseHandler>(ViewMappers.ViewMapper)
    {
        [ListViewBase.HeaderProperty] = (h, e) => h.UpdateHeader(),
        [ListViewBase.HeaderTemplateProperty] = (h, e) => h.UpdateHeader(),
        [ListViewBase.FooterProperty] = (h, e) => h.UpdateFooter(),
        [ListViewBase.FooterTemplateProperty] = (h, e) => h.UpdateFooter(),
    }.WithRecyclerItemsKeys();

    private ContentControl _header;
    private ContentControl _footer;

    /// <summary>Creates the handler.</summary>
    public ListViewBaseHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override UIElement HeaderElement => _header;

    /// <inheritdoc />
    public override UIElement FooterElement => _footer;

    /// <inheritdoc />
    protected override ItemsLayoutSpec DefaultSpec => Element is GridView ? ItemsLayoutSpec.VerticalGrid : ItemsLayoutSpec.VerticalList;

    /// <summary>
    /// True when a list can be shown by this handler: ListView, GridView, TreeView's TreeViewList and
    /// application subclasses of ListView / GridView (not the lists other Core controls keep inside their
    /// templates), not re-templated by the application, not grouped, and with an items panel the
    /// RecyclerView reproduces. Other lists keep Core's template (the templated fallback).
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns>True when the native list serves it.</returns>
    internal static bool CanHandle(ListViewBase list)
    {
        if (list == null)
        {
            return false;
        }

        var type = list.GetType();
        var coreAssembly = typeof(ListView).Assembly;
        var known = type == typeof(ListView) || type == typeof(GridView) || type == typeof(TreeViewList);
        if (!known && type.Assembly == coreAssembly)
        {
            return false;
        }

        // A list inside another control's template is that control's business (TreeView's list excepted).
        if (list.TemplatedParent != null && list is not TreeViewList)
        {
            return false;
        }

        if (list.ReadLocalValue(Control.TemplateProperty) != DependencyProperty.UnsetValue || list.IsGrouping)
        {
            return false;
        }

        var defaultSpec = list is GridView ? ItemsLayoutSpec.VerticalGrid : ItemsLayoutSpec.VerticalList;
        return SpecOf(list.ItemsPanel, defaultSpec) != null;
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        // TreeViewList attaches its flattened node list in OnApplyTemplate, which a list that owns its visuals
        // never runs: do what it does (Core's TreeView then expands and collapses through that list).
        if (Element is TreeViewList tree && tree.ItemsSource == null && tree.ListViewModel is { } model)
        {
            tree.ItemsSource = model;
            tree.IsItemClickEnabled = true;
        }

        base.OnConnected();
    }

    private void UpdateHeader()
    {
        if (Element is not ListViewBase list)
        {
            return;
        }

        _header = UpdateSlot(_header, list.Header, list.HeaderTemplate);
        Engine?.RefreshHeaderFooter();
    }

    private void UpdateFooter()
    {
        if (Element is not ListViewBase list)
        {
            return;
        }

        _footer = UpdateSlot(_footer, list.Footer, list.FooterTemplate);
        Engine?.RefreshHeaderFooter();
    }

    private static ContentControl UpdateSlot(ContentControl slot, object content, DataTemplate template)
    {
        if (content == null && template == null)
        {
            if (slot != null)
            {
                slot.Content = null;
            }

            return null;
        }

        slot ??= new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            IsTabStop = false,
        };
        slot.Content = content;
        slot.ContentTemplate = template;
        return slot;
    }
}
