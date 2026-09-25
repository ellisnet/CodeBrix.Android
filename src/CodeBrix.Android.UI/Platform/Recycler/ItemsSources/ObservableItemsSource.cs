// Derived from .NET MAUI, src/Controls/src/Core/Handlers/Items/Android/ItemsSources/ObservableItemsSource.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Specialized;
using Windows.Foundation.Collections;

namespace CodeBrix.Android.UI.Platform.Recycler.ItemsSources;

/// <summary>
/// An adapter's view of a Core items collection that tells the adapter what changed: an
/// <see cref="INotifyCollectionChanged"/> source (ranges, the MAUI technique) or an
/// <see cref="IObservableVector{T}"/> (an ItemsControl's ItemCollection, which forwards every change
/// of its ItemsSource or of its Items, one index at a time). The items are read through the
/// delegates the owner gives (for an ItemsControl: Core's own item list, so a non-observable
/// ItemsSource that Core snapshotted reads the snapshot). Header and footer positions are counted in.
/// </summary>
/// <remarks>
/// Changes arrive on the UI thread (Core raises them there); a change raised on another thread is
/// posted to the main looper, as MAUI's DispatchIfRequired does.
/// </remarks>
internal sealed class ObservableItemsSource : IItemsViewSource
{
    private readonly Func<int> _count;
    private readonly Func<int, object> _itemAt;
    private readonly ICollectionChangedNotifier _notifier;
    private INotifyCollectionChanged _observable;
    private IObservableVector<object> _vector;
    private bool _disposed;

    /// <summary>Creates the source.</summary>
    /// <param name="count">The number of items.</param>
    /// <param name="itemAt">The item at an item index.</param>
    /// <param name="changes">The collection whose change events are followed: an INotifyCollectionChanged or an IObservableVector&lt;object&gt; (null = none).</param>
    /// <param name="notifier">Who is told about changes (the adapter).</param>
    internal ObservableItemsSource(Func<int> count, Func<int, object> itemAt, object changes, ICollectionChangedNotifier notifier)
    {
        _count = count ?? throw new ArgumentNullException(nameof(count));
        _itemAt = itemAt ?? throw new ArgumentNullException(nameof(itemAt));
        _notifier = notifier;
        switch (changes)
        {
            case INotifyCollectionChanged observable:
                _observable = observable;
                _observable.CollectionChanged += OnCollectionChanged;
                break;
            case IObservableVector<object> vector:
                _vector = vector;
                _vector.VectorChanged += OnVectorChanged;
                break;
        }
    }

    /// <summary>Raised after a change was forwarded to the notifier.</summary>
    internal event EventHandler Changed;

    /// <inheritdoc />
    public int Count => ItemsCount() + (HasHeader ? 1 : 0) + (HasFooter ? 1 : 0);

    /// <inheritdoc />
    public bool HasHeader { get; set; }

    /// <inheritdoc />
    public bool HasFooter { get; set; }

    /// <summary>False to stop forwarding changes (the owner rebuilds instead).</summary>
    internal bool ObserveChanges { get; set; } = true;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_observable != null)
        {
            _observable.CollectionChanged -= OnCollectionChanged;
            _observable = null;
        }

        if (_vector != null)
        {
            _vector.VectorChanged -= OnVectorChanged;
            _vector = null;
        }
    }

    /// <inheritdoc />
    public bool IsFooter(int index) => HasFooter && index == Count - 1;

    /// <inheritdoc />
    public bool IsHeader(int index) => HasHeader && index == 0;

    /// <inheritdoc />
    public int GetPosition(object item)
    {
        var count = ItemsCount();
        for (var n = 0; n < count; n++)
        {
            var elementByIndex = _itemAt(n);
            var isEqual = elementByIndex == item || (elementByIndex != null && item != null && elementByIndex.Equals(item));
            if (isEqual)
            {
                return AdjustPositionForHeader(n);
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public object GetItem(int position) => _itemAt(ToItemIndex(position));

    /// <inheritdoc />
    public int ToItemIndex(int position) => position - (HasHeader ? 1 : 0);

    /// <summary>The number of items (header and footer not counted).</summary>
    internal int ItemsCount() => Math.Max(0, _count());

    private int AdjustPositionForHeader(int position) => position + (HasHeader ? 1 : 0);

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        if (!ObserveChanges || _disposed)
        {
            return;
        }

        RunOnMainThread(() => CollectionChanged(args));
    }

    private void OnVectorChanged(IObservableVector<object> sender, IVectorChangedEventArgs args)
    {
        if (!ObserveChanges || _disposed)
        {
            return;
        }

        var change = args.CollectionChange;
        var index = (int)args.Index;
        RunOnMainThread(() =>
        {
            if (_notifier != null)
            {
                switch (change)
                {
                    case CollectionChange.ItemInserted:
                        _notifier.NotifyItemInserted(this, AdjustPositionForHeader(index));
                        break;
                    case CollectionChange.ItemRemoved:
                        _notifier.NotifyItemRemoved(this, AdjustPositionForHeader(index));
                        break;
                    case CollectionChange.ItemChanged:
                        _notifier.NotifyItemChanged(this, AdjustPositionForHeader(index));
                        break;
                    default:
                        _notifier.NotifyDataSetChanged();
                        break;
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private void CollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        if (_notifier != null)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Add(args);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    Remove(args);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    Replace(args);
                    break;
                case NotifyCollectionChangedAction.Move:
                    Move(args);
                    break;
                default:
                    _notifier.NotifyDataSetChanged();
                    break;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Move(NotifyCollectionChangedEventArgs args)
    {
        var count = args.NewItems?.Count ?? 1;
        if (count == 1)
        {
            // For a single item, NotifyItemMoved keeps the animation.
            _notifier.NotifyItemMoved(this, AdjustPositionForHeader(args.OldStartingIndex), AdjustPositionForHeader(args.NewStartingIndex));
            return;
        }

        var start = AdjustPositionForHeader(Math.Min(args.OldStartingIndex, args.NewStartingIndex));
        var end = AdjustPositionForHeader(Math.Max(args.OldStartingIndex, args.NewStartingIndex) + count);
        _notifier.NotifyItemRangeChanged(this, start, end);
    }

    private void Add(NotifyCollectionChangedEventArgs args)
    {
        var count = args.NewItems?.Count ?? 0;
        if (args.NewStartingIndex < 0 || count == 0)
        {
            // Not enough information about where the items went: rebind everything.
            _notifier.NotifyDataSetChanged();
            return;
        }

        var startIndex = AdjustPositionForHeader(args.NewStartingIndex);
        if (count == 1)
        {
            _notifier.NotifyItemInserted(this, startIndex);
            return;
        }

        _notifier.NotifyItemRangeInserted(this, startIndex, count);
    }

    private void Remove(NotifyCollectionChangedEventArgs args)
    {
        var startIndex = args.OldStartingIndex;
        if (startIndex < 0 || args.OldItems == null)
        {
            // The INCC implementation did not say where the removed items were: rebind everything.
            _notifier.NotifyDataSetChanged();
            return;
        }

        startIndex = AdjustPositionForHeader(startIndex);
        var count = args.OldItems.Count;
        if (count == 1)
        {
            _notifier.NotifyItemRemoved(this, startIndex);
            return;
        }

        _notifier.NotifyItemRangeRemoved(this, startIndex, count);
    }

    private void Replace(NotifyCollectionChangedEventArgs args)
    {
        if (args.NewStartingIndex < 0 || args.NewItems == null || args.OldItems == null)
        {
            _notifier.NotifyDataSetChanged();
            return;
        }

        var startIndex = AdjustPositionForHeader(args.NewStartingIndex);
        var newCount = args.NewItems.Count;
        if (newCount == args.OldItems.Count)
        {
            // Replacing one set of items with a set of equal size: an item or range notification.
            if (newCount == 1)
            {
                _notifier.NotifyItemChanged(this, startIndex);
            }
            else
            {
                _notifier.NotifyItemRangeChanged(this, startIndex, newCount);
            }

            return;
        }

        // Sets of unequal size: everything in view has to be updated.
        _notifier.NotifyDataSetChanged();
    }

    private static void RunOnMainThread(Action action)
    {
        var main = global::Android.OS.Looper.MainLooper;
        if (main == null || main.IsCurrentThread)
        {
            action();
            return;
        }

        new global::Android.OS.Handler(main).Post(action);
    }
}
