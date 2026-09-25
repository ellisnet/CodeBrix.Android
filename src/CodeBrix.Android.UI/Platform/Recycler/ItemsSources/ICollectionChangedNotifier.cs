// Derived from .NET MAUI, src/Controls/src/Core/Handlers/Items/Android/ItemsSources/ICollectionChangedNotifier.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

namespace CodeBrix.Android.UI.Platform.Recycler.ItemsSources;

/// <summary>
/// Lets an observable items source tell its observer (a RecyclerView adapter) which adapter positions
/// changed, so the list animates and rebinds only what changed.
/// </summary>
internal interface ICollectionChangedNotifier
{
    /// <summary>Everything changed (a reset).</summary>
    void NotifyDataSetChanged();

    /// <summary>One item changed in place.</summary>
    void NotifyItemChanged(IItemsViewSource source, int startIndex);

    /// <summary>One item was inserted.</summary>
    void NotifyItemInserted(IItemsViewSource source, int startIndex);

    /// <summary>One item moved.</summary>
    void NotifyItemMoved(IItemsViewSource source, int fromPosition, int toPosition);

    /// <summary>A range of items changed in place (<paramref name="end"/> exclusive).</summary>
    void NotifyItemRangeChanged(IItemsViewSource source, int start, int end);

    /// <summary>A range of items was inserted.</summary>
    void NotifyItemRangeInserted(IItemsViewSource source, int startIndex, int count);

    /// <summary>A range of items was removed.</summary>
    void NotifyItemRangeRemoved(IItemsViewSource source, int startIndex, int count);

    /// <summary>One item was removed.</summary>
    void NotifyItemRemoved(IItemsViewSource source, int startIndex);
}
