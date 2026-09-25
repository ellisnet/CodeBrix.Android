using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// The browsing grid's lazily-loading item source: it holds the full (filtered) cell list
/// but only materializes cells into the grid in batches - a first screenful up front, then
/// more whenever the grid scrolls near its bottom edge (the page watches the ScrollViewer
/// and calls <see cref="RequestMore"/>) - so hundreds of cells and thumbnails are never
/// created before they can be seen.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class AssetCellCollection : ObservableCollection<AssetCellViewModel>
{
    //Enough cells to overfill the first screen even on a wide monitor.
    private const int InitialBatch = 36;

    /// <summary>
    /// How many further cells to materialize each time the grid asks for more. How near the
    /// bottom edge counts as "near" is a measurement only the view can make, but how much to
    /// add when it does is this collection's own policy, beside its initial batch.
    /// </summary>
    public const int ScrollBatch = 24;

    private readonly IReadOnlyList<AssetCellViewModel> _source;

    /// <summary>Creates the collection over an already filtered and ordered cell list.</summary>
    public AssetCellCollection(IReadOnlyList<AssetCellViewModel> source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        RequestMore(InitialBatch);
    }

    /// <summary>The total number of assets behind the collection (materialized or not).</summary>
    public int TotalCount => _source.Count;

    /// <summary>Whether assets remain that are not showing in the grid yet.</summary>
    public bool HasMoreItems => Count < _source.Count;

    /// <summary>
    /// Materializes up to <c>count</c> further cells (each one starts fetching its
    /// thumbnail as it appears). Safe to call repeatedly; extra calls simply no-op once
    /// every asset is showing.
    /// </summary>
    /// <param name="count">The maximum number of cells to add.</param>
    public void RequestMore(int count)
    {
        var toLoad = Math.Min(count, _source.Count - Count);

        for (var i = 0; i < toLoad; i++)
        {
            var cell = _source[Count];
            Add(cell);

            //Fire-and-forget: the cell fetches its thumbnail in the background and raises
            //a property change when the image arrives.
            _ = cell.LoadThumbnailAsync();
        }
    }
}
