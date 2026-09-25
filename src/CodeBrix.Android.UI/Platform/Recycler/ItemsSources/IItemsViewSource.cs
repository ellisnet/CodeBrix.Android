// Derived from .NET MAUI, src/Controls/src/Core/Handlers/Items/Android/ItemsSources/IItemsViewSource.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;

namespace CodeBrix.Android.UI.Platform.Recycler.ItemsSources;

/// <summary>
/// The items a RecyclerView adapter shows, by ADAPTER POSITION: the source's items plus an optional
/// header (position 0) and footer (last position).
/// </summary>
internal interface IItemsViewSource : IDisposable
{
    /// <summary>The number of adapter positions (items + header + footer).</summary>
    int Count { get; }

    /// <summary>The adapter position of an item, or -1.</summary>
    int GetPosition(object item);

    /// <summary>The item at an adapter position (not valid for the header/footer positions).</summary>
    object GetItem(int position);

    /// <summary>True when position 0 is a header.</summary>
    bool HasHeader { get; set; }

    /// <summary>True when the last position is a footer.</summary>
    bool HasFooter { get; set; }

    /// <summary>True when <paramref name="position"/> is the header.</summary>
    bool IsHeader(int position);

    /// <summary>True when <paramref name="position"/> is the footer.</summary>
    bool IsFooter(int position);

    /// <summary>The item index (in the source) of an adapter position.</summary>
    int ToItemIndex(int position);
}
