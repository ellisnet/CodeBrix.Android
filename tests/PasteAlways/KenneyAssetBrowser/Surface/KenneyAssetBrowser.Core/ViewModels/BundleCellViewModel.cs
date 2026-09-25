using CodeBrix.Platform.Simple;
using KenneyAssetBrowser.AssetRead.Models;
using KenneyAssetBrowser.Helpers;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// One bundle card of the sidebar: the bundle's cover thumbnail, display name, version
/// and content summary. Selecting the card makes its bundle the one whose assets fill
/// the browsing grid.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class BundleCellViewModel : SimpleViewModel
{
    private static readonly Brush SelectedBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x26, 0x2B, 0x34));
    private static readonly Brush NormalBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1F, 0x23, 0x2B));
    private static readonly Brush SelectedBorder = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x4F, 0xA6, 0xE8));
    private static readonly Brush NormalBorder = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2A, 0x2F, 0x39));

    private readonly Func<BundleCellViewModel, Task> _selectAsync;
    private readonly Func<Task<byte[]>> _coverBytesAsync;
    private ImageSource _thumbnail;
    private bool _thumbnailFailed;
    private bool _isSelected;

    /// <summary>
    /// Creates a sidebar card for one bundle. The owning view model supplies what selecting
    /// the card does (selectAsync) and how the cover image's bytes are fetched (coverBytesAsync).
    /// </summary>
    public BundleCellViewModel(AssetBundle bundle,
        Func<BundleCellViewModel, Task> selectAsync, Func<Task<byte[]>> coverBytesAsync)
    {
        Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _selectAsync = selectAsync ?? throw new ArgumentNullException(nameof(selectAsync));
        _coverBytesAsync = coverBytesAsync;

        Title = bundle.DisplayName;
        VersionText = string.IsNullOrWhiteSpace(bundle.Version) ? string.Empty : $"v{bundle.Version}";
        SubtitleText = BuildSubtitle(bundle);
    }

    /// <summary>The parsed bundle this card represents.</summary>
    public AssetBundle Bundle { get; }

    /// <summary>The bundle's display name.</summary>
    public string Title { get; }

    /// <summary>The bundle version caption, e.g. <c>v1.0</c> (empty when unknown).</summary>
    public string VersionText { get; }

    /// <summary>The content summary line, e.g. <c>296 models · 301 images</c>.</summary>
    public string SubtitleText { get; }

    /// <summary>Whether this card's bundle is the one showing in the grid.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) { return; }
            _isSelected = value;
            NotifyPropertyChanged(nameof(IsSelected));
            NotifyPropertyChanged(nameof(CardBackground));
            NotifyPropertyChanged(nameof(CardBorder));
        }
    }

    /// <summary>The card's background brush (highlighted while selected).</summary>
    public Brush CardBackground => _isSelected ? SelectedBackground : NormalBackground;

    /// <summary>The card's border brush (accent while selected).</summary>
    public Brush CardBorder => _isSelected ? SelectedBorder : NormalBorder;

    /// <summary>Makes this card's bundle the selected one.</summary>
    public SimpleCommand SelectCommand => field ??=
        new SimpleCommand((Func<object, Task>)(_ => _selectAsync(this)));

    /// <summary>The bundle's cover image, populated asynchronously after the card appears.</summary>
    public ImageSource Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>
    /// Fetches the bundle's cover image and hands it to the Image control. Failures leave
    /// the placeholder glyph showing.
    /// </summary>
    public async Task LoadThumbnailAsync()
    {
        if (_thumbnail != null || _thumbnailFailed || _coverBytesAsync == null) { return; }

        try
        {
            var bytes = await _coverBytesAsync();
            if (bytes == null) { _thumbnailFailed = true; return; }

            //Back on the UI thread here (the awaiter restores the dispatcher context), which
            //is where BitmapImage wants to be touched.
            var image = new BitmapImage();
            using (var stream = new MemoryStream(bytes))
            {
                await image.SetSourceAsync(stream.AsRandomAccessStream());
            }
            Thumbnail = image;
        }
        catch (Exception)
        {
            //A missing cover is cosmetic; the card simply keeps its placeholder.
            _thumbnailFailed = true;
        }
    }

    private static string BuildSubtitle(AssetBundle bundle)
    {
        var parts = new List<string>();
        if (bundle.ModelCount > 0) { parts.Add(FormatHelper.FormatCount(bundle.ModelCount, "model")); }
        if (bundle.HasAtlases) { parts.Add(FormatHelper.FormatCount(bundle.Atlases.Count, "sheet")); }
        if (bundle.ImageCount > 0) { parts.Add(FormatHelper.FormatCount(bundle.ImageCount, "image")); }
        if (parts.Count == 0) { parts.Add(FormatHelper.FormatCount(bundle.Entries.Count, "file")); }
        return string.Join(" · ", parts);
    }
}
