using CodeBrix.Platform.Simple;
using KenneyAssetBrowser.AssetRead.Models;
using Microsoft.UI.Xaml.Media;
using System;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// One row of the spritesheet viewer's region list: a named TextureAtlas region.
/// Selecting the row spotlights the region on the sheet.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class AtlasRegionCellViewModel : SimpleViewModel
{
    private static readonly Brush SelectedBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x26, 0x2B, 0x34));
    private static readonly Brush NormalBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));

    private readonly Action<AtlasRegionCellViewModel> _select;
    private bool _isSelected;

    /// <summary>
    /// Creates a row for one atlas region. The owning view model supplies what selecting
    /// the row does (select).
    /// </summary>
    public AtlasRegionCellViewModel(SpriteRegion region, Action<AtlasRegionCellViewModel> select)
    {
        Region = region ?? throw new ArgumentNullException(nameof(region));
        _select = select ?? throw new ArgumentNullException(nameof(select));

        BoundsText = $"{region.Width}×{region.Height} at {region.X},{region.Y}";
    }

    /// <summary>The atlas region this row represents.</summary>
    public SpriteRegion Region { get; }

    /// <summary>The region name (usually the original sprite file name).</summary>
    public string Name => Region.Name;

    /// <summary>The region's size and position caption, e.g. <c>22×22 at 27,338</c>.</summary>
    public string BoundsText { get; }

    /// <summary>Whether this row's region is the spotlighted one.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) { return; }
            _isSelected = value;
            NotifyPropertyChanged(nameof(IsSelected));
            NotifyPropertyChanged(nameof(RowBackground));
        }
    }

    /// <summary>The row's background brush (highlighted while selected).</summary>
    public Brush RowBackground => _isSelected ? SelectedBackground : NormalBackground;

    /// <summary>Spotlights this row's region on the sheet (or clears the spotlight when re-selected).</summary>
    public SimpleCommand SelectCommand => field ??= new SimpleCommand(() => _select(this));
}
