// Derived from .NET MAUI, src/Controls/src/Core/Handlers/Items/Android/Adapters/ItemsViewAdapter.cs and TemplatedItemViewHolder.cs (the adapter and view-holder technique) @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Platform.Recycler.ItemsSources;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using AViewGroup = global::Android.Views.ViewGroup;
using ARecyclerView = global::AndroidX.RecyclerView.Widget.RecyclerView;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>What a <see cref="CoreItemsAdapter"/> asks the handler that owns the list.</summary>
internal interface IRecyclerItemsOwner
{
    /// <summary>The Core panel whose children are the attached item elements.</summary>
    RecyclerItemsPanel Panel { get; }

    /// <summary>The adapter positions (items, header, footer).</summary>
    IItemsViewSource Source { get; }

    /// <summary>The view type of an adapter position (item template, own container, header, footer).</summary>
    int GetViewType(int position);

    /// <summary>Creates the Core element for a view type (an item container, the header...).</summary>
    UIElement CreateElement(int viewType);

    /// <summary>Prepares an element for an adapter position (Core's container preparation: content, index, selection).</summary>
    void BindElement(UIElement element, int viewType, int position);

    /// <summary>Clears an element whose view holder went to the recycled pool.</summary>
    void UnbindElement(UIElement element, int viewType);

    /// <summary>An element the pool dropped (it will not be reused).</summary>
    void DiscardElement(UIElement element, int viewType);

    /// <summary>Called when an element entered the panel (its view holder was attached).</summary>
    void OnElementAttached(UIElement element, int position);

    /// <summary>The layout parameters of an item host of a view type.</summary>
    ARecyclerView.LayoutParams CreateLayoutParams(int viewType);

    /// <summary>A fixed item size in DIPs (uniform grids), or null.</summary>
    Size? FixedItemSize(int viewType);

    /// <summary>The RecyclerView's origin in window DIPs.</summary>
    Point RecyclerOrigin();
}

/// <summary>
/// The RecyclerView adapter of the Core items controls. A view holder is an item host
/// (<see cref="RecyclerItemHost"/>) plus the Core element it shows (an item container with its applied
/// template - reused across items, as MAUI reuses a TemplatedItemViewHolder's content). The element is a
/// child of the list's Core panel exactly while its view holder is attached (so Core's tree holds the
/// realised items, Loaded/Unloaded follow attach/detach, and Core finds the container of a visible item);
/// binding runs Core's own container preparation. Changes of the Core collection arrive through
/// <see cref="ICollectionChangedNotifier"/>.
/// </summary>
internal sealed class CoreItemsAdapter : ARecyclerView.Adapter, ICollectionChangedNotifier
{
    /// <summary>The view type of the header position.</summary>
    internal const int HeaderViewType = -2;

    /// <summary>The view type of the footer position.</summary>
    internal const int FooterViewType = -3;

    private readonly IRecyclerItemsOwner _owner;

    /// <summary>Creates the adapter.</summary>
    /// <param name="owner">The list handler.</param>
    internal CoreItemsAdapter(IRecyclerItemsOwner owner) => _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>How many view holders (item hosts + Core elements) the adapter created (diagnostics: recycling).</summary>
    internal int CreatedHolders { get; private set; }

    /// <summary>Raised after a collection change was applied to the adapter.</summary>
    internal event Action DataChanged;

    /// <inheritdoc />
    public override int ItemCount => Math.Max(0, _owner.Source?.Count ?? 0);

    /// <inheritdoc />
    public override int GetItemViewType(int position) => _owner.GetViewType(position);

    /// <inheritdoc />
    public override ARecyclerView.ViewHolder OnCreateViewHolder(AViewGroup parent, int viewType)
    {
        var host = new RecyclerItemHost(parent.Context)
        {
            Panel = _owner.Panel,
            RecyclerOrigin = _owner.RecyclerOrigin,
            LayoutParameters = _owner.CreateLayoutParams(viewType),
        };
        var element = _owner.CreateElement(viewType);
        CreatedHolders++;
        return new CoreItemViewHolder(host, element, viewType);
    }

    /// <inheritdoc />
    public override void OnBindViewHolder(ARecyclerView.ViewHolder holder, int position)
    {
        if (holder is not CoreItemViewHolder item)
        {
            return;
        }

        _owner.BindElement(item.Element, item.ViewType, position);
        item.Host.FixedSize = _owner.FixedItemSize(item.ViewType);
        item.Host.Element = item.Element;
        if (_owner.Panel is { } panel)
        {
            panel.SetHost(item.Element, item.Host);

            // Rebound in place while attached (a reset rebinds the attached holders without detaching them):
            // Core may have cleared the panel meanwhile, so the element goes back in.
            if (item.Host.IsAttachedToWindow && !panel.Children.Contains(item.Element))
            {
                panel.Children.Add(item.Element);
                item.Host.AdoptElementView();
                _owner.OnElementAttached(item.Element, position);
            }
        }

        item.Host.RequestLayout();
    }

    /// <inheritdoc />
    public override void OnViewAttachedToWindow(Java.Lang.Object holder)
    {
        base.OnViewAttachedToWindow(holder);
        if (holder is not CoreItemViewHolder item || _owner.Panel is not { } panel)
        {
            return;
        }

        panel.SetHost(item.Element, item.Host);
        if (!panel.Children.Contains(item.Element))
        {
            panel.Children.Add(item.Element);
        }

        item.Host.Element = item.Element;
        item.Host.AdoptElementView();
        if (RecyclerTrace.IsEnabled)
        {
            RecyclerTrace.Write($"attach pos={item.BindingAdapterPosition} element={item.Element.GetType().Name} live={item.Element.XamlRoot != null} panelLive={panel.XamlRoot != null} panelChildren={panel.Children.Count} handler={(item.Element as Microsoft.UI.Xaml.UIElement)?.GetType().Name}");
        }

        _owner.OnElementAttached(item.Element, item.BindingAdapterPosition);
    }

    /// <inheritdoc />
    public override void OnViewDetachedFromWindow(Java.Lang.Object holder)
    {
        base.OnViewDetachedFromWindow(holder);
        if (holder is not CoreItemViewHolder item || _owner.Panel is not { } panel)
        {
            return;
        }

        panel.Children.Remove(item.Element);
        panel.Forget(item.Element);
        item.Host.ReleaseElementView();
    }

    /// <inheritdoc />
    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        base.OnViewRecycled(holder);
        if (holder is CoreItemViewHolder item)
        {
            _owner.UnbindElement(item.Element, item.ViewType);
        }
    }

    /// <inheritdoc />
    public override bool OnFailedToRecycleView(Java.Lang.Object holder) => true;

    /// <summary>Drops a view holder the pool will not keep.</summary>
    internal void Discard(ARecyclerView.ViewHolder holder)
    {
        if (holder is CoreItemViewHolder item)
        {
            _owner.Panel?.SetHost(item.Element, null);
            _owner.DiscardElement(item.Element, item.ViewType);
        }
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyDataSetChanged()
    {
        NotifyDataSetChanged();
        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemChanged(IItemsViewSource source, int startIndex)
    {
        NotifyItemChanged(startIndex);
        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemInserted(IItemsViewSource source, int startIndex)
    {
        NotifyItemInserted(startIndex);
        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemMoved(IItemsViewSource source, int fromPosition, int toPosition)
    {
        NotifyItemMoved(fromPosition, toPosition);
        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemRangeChanged(IItemsViewSource source, int start, int end)
    {
        var count = Math.Min(end, ItemCount) - start;
        if (count > 0)
        {
            NotifyItemRangeChanged(start, count);
        }

        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemRangeInserted(IItemsViewSource source, int startIndex, int count)
    {
        NotifyItemRangeInserted(startIndex, count);
        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemRangeRemoved(IItemsViewSource source, int startIndex, int count)
    {
        NotifyItemRangeRemoved(startIndex, count);
        DataChanged?.Invoke();
    }

    /// <inheritdoc />
    void ICollectionChangedNotifier.NotifyItemRemoved(IItemsViewSource source, int startIndex)
    {
        NotifyItemRemoved(startIndex);
        DataChanged?.Invoke();
    }
}

/// <summary>A view holder: an item host and the Core element it shows.</summary>
internal sealed class CoreItemViewHolder : ARecyclerView.ViewHolder
{
    /// <summary>Creates the holder.</summary>
    internal CoreItemViewHolder(RecyclerItemHost host, UIElement element, int viewType)
        : base(host)
    {
        Host = host;
        Element = element;
        ViewType = viewType;
    }

    /// <summary>The item view.</summary>
    internal RecyclerItemHost Host { get; }

    /// <summary>The Core element (reused while the holder is recycled and rebound).</summary>
    internal UIElement Element { get; }

    /// <summary>The view type the holder was created for.</summary>
    internal int ViewType { get; }
}

/// <summary>
/// A recycled-view pool that keeps a bounded number of view holders per view type and tells the adapter
/// about the ones it drops (their Core elements are released).
/// </summary>
internal sealed class CoreItemsViewPool : ARecyclerView.RecycledViewPool
{
    private readonly CoreItemsAdapter _adapter;
    private readonly int _maxPerType;
    private readonly HashSet<int> _limited = new();

    /// <summary>Creates the pool.</summary>
    internal CoreItemsViewPool(CoreItemsAdapter adapter, int maxPerType)
    {
        _adapter = adapter;
        _maxPerType = Math.Max(1, maxPerType);
    }

    /// <inheritdoc />
    public override void PutRecycledView(ARecyclerView.ViewHolder scrap)
    {
        if (scrap == null)
        {
            return;
        }

        var type = scrap.ItemViewType;
        if (_limited.Add(type))
        {
            SetMaxRecycledViews(type, _maxPerType);
        }

        if (GetRecycledViewCount(type) >= _maxPerType)
        {
            _adapter.Discard(scrap);
            return;
        }

        base.PutRecycledView(scrap);
    }
}
