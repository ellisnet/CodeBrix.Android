using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>The broad kind of asset a browsing-grid cell represents (drives the cell's chip and viewer mode).</summary>
public enum AssetCellKind
{
    /// <summary>A viewable raster image.</summary>
    Image,

    /// <summary>A 3D model (grouped across its format variants).</summary>
    Model,

    /// <summary>A spritesheet with a parsed TextureAtlas.</summary>
    Atlas,

    /// <summary>A readable text document.</summary>
    Document,

    /// <summary>A playable audio clip.</summary>
    Audio,

    /// <summary>SVG vector art, rasterized for viewing.</summary>
    Vector,

    /// <summary>A font file, shown as a specimen sheet.</summary>
    Font,

    /// <summary>A Tiled map (.tmx, composited preview) or tileset (.tsx, shown as text).</summary>
    TiledMap,

    /// <summary>Any other file (source, engine package, nested archive, …).</summary>
    Other,
}

/// <summary>
/// One cell of the browsing grid: a single asset's thumbnail (when it has one), name,
/// category and size, plus the View command that opens it in the viewer. Cells are
/// materialized lazily as the user scrolls, and each cell fetches its own thumbnail
/// when created.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class AssetCellViewModel : SimpleViewModel
{
    private readonly Func<AssetCellViewModel, Task> _openAsync;
    private readonly Func<Task<byte[]>> _thumbnailBytesAsync;
    private ImageSource _thumbnail;
    private bool _thumbnailFailed;

    /// <summary>
    /// Creates a cell for one asset. The owning view model supplies what opening the asset
    /// does (openAsync) and how the thumbnail's bytes are fetched (thumbnailBytesAsync,
    /// <c>null</c> for kinds with no thumbnail).
    /// </summary>
    public AssetCellViewModel(string title, AssetCellKind kind, string kindLabel, string glyph,
        string subtitle, string detailText, object payload,
        Func<AssetCellViewModel, Task> openAsync, Func<Task<byte[]>> thumbnailBytesAsync)
    {
        Title = title ?? string.Empty;
        Kind = kind;
        KindLabel = kindLabel ?? string.Empty;
        Glyph = glyph ?? "";
        Subtitle = subtitle ?? string.Empty;
        DetailText = detailText ?? string.Empty;
        Payload = payload;
        _openAsync = openAsync ?? throw new ArgumentNullException(nameof(openAsync));
        _thumbnailBytesAsync = thumbnailBytesAsync;
    }

    /// <summary>The asset's display name.</summary>
    public string Title { get; }

    /// <summary>The broad kind of asset this cell represents.</summary>
    public AssetCellKind Kind { get; }

    /// <summary>The kind chip's caption, e.g. <c>MODEL</c>.</summary>
    public string KindLabel { get; }

    /// <summary>The Fluent-symbol glyph shown while there is no thumbnail.</summary>
    public string Glyph { get; }

    /// <summary>The category line under the title (the folder chain inside the bundle).</summary>
    public string Subtitle { get; }

    /// <summary>The footer detail, e.g. the file size or the model's format list.</summary>
    public string DetailText { get; }

    /// <summary>The underlying asset object (an AssetEntry, ModelAsset or SpriteAtlas).</summary>
    public object Payload { get; }

    /// <summary>
    /// Opens this cell's asset in the viewer. Living on the cell itself keeps the cell
    /// template's binding a plain <c>{Binding OpenCommand}</c> - a template binds to its own item.
    /// </summary>
    public SimpleCommand OpenCommand => field ??=
        new SimpleCommand((Func<object, Task>)(_ => _openAsync(this)));

    /// <summary>The thumbnail image, populated asynchronously after the cell appears.</summary>
    public ImageSource Thumbnail
    {
        get => _thumbnail;
        private set
        {
            SetProperty(ref _thumbnail, value);
            NotifyPropertyChanged(nameof(PlaceholderVisibility));
        }
    }

    /// <summary>The placeholder glyph's visibility (shown until a thumbnail arrives, or always for kinds without one).</summary>
    public Visibility PlaceholderVisibility => _thumbnail == null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Fetches the cell's thumbnail from the bundle archive and hands it to the Image
    /// control. Failures leave the placeholder glyph showing.
    /// </summary>
    public async Task LoadThumbnailAsync()
    {
        if (_thumbnail != null || _thumbnailFailed || _thumbnailBytesAsync == null) { return; }

        try
        {
            var bytes = await _thumbnailBytesAsync();
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
            //A missing thumbnail is cosmetic; the cell simply keeps its placeholder.
            _thumbnailFailed = true;
        }
    }
}
