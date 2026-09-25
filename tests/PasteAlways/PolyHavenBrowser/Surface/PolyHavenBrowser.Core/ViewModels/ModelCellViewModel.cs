using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PolyHavenBrowser.PolyHavenApiClient;
using PolyHavenBrowser.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace PolyHavenBrowser.ViewModels;

/// <summary>
/// One catalog cell of the Browsing View: a single Poly Haven model's hero thumbnail,
/// title, creator credit, short description and download stats. Cells are materialized
/// lazily as the user scrolls, and each cell fetches its own thumbnail when created.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class ModelCellViewModel : SimpleViewModel
{
    private readonly ModelCatalogService _catalog;
    private readonly Func<ModelCellViewModel, Task> _downloadAsync;
    private readonly Func<bool> _canDownload;
    private ImageSource _thumbnail;
    private bool _thumbnailFailed;

    /// <summary>
    /// Creates a cell for one model of the catalog. The owning view model supplies what
    /// downloading actually does (<paramref name="downloadAsync"/>) and the app-wide
    /// "may a download start right now?" gate (<paramref name="canDownload"/>); the cell
    /// only owns the command itself.
    /// </summary>
    public ModelCellViewModel(PolyHavenAsset asset, ModelCatalogService catalog,
        Func<ModelCellViewModel, Task> downloadAsync, Func<bool> canDownload)
    {
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _downloadAsync = downloadAsync ?? throw new ArgumentNullException(nameof(downloadAsync));
        _canDownload = canDownload ?? throw new ArgumentNullException(nameof(canDownload));

        Title = string.IsNullOrWhiteSpace(asset.Name) ? asset.Id : asset.Name;
        AuthorCredit = BuildAuthorCredit(asset);
        Blurb = ModelDescriptionBuilder.BuildCellBlurb(asset);
        DownloadsText = ModelDescriptionBuilder.FormatCompactCount(asset.DownloadCount);
        CategoriesText = BuildCategoriesText(asset);
    }

    private SimpleCommand _downloadCommand;

    /// <summary>
    /// Downloads this cell's model and opens the Model View. Living on the cell itself keeps
    /// the cell template's binding a plain <c>{Binding DownloadCommand}</c> - a template binds
    /// to its own item.
    /// </summary>
    public SimpleCommand DownloadCommand => _downloadCommand ??=
        new SimpleCommand(() => _canDownload(), _ => _downloadAsync(this));

    /// <summary>
    /// Lets the owning view model tell this cell's Download button to re-query its enabled
    /// state (called on every cell when a download starts or finishes).
    /// </summary>
    public void NotifyCanDownloadChanged() => _downloadCommand?.RaiseCanExecuteChanged();

    /// <summary>The Poly Haven asset this cell represents.</summary>
    public PolyHavenAsset Asset { get; }

    /// <summary>The model's display name.</summary>
    public string Title { get; }

    /// <summary>The creator credit line, e.g. <c>by Rico Cilliers</c>.</summary>
    public string AuthorCredit { get; }

    /// <summary>The short description shown under the title.</summary>
    public string Blurb { get; }

    /// <summary>The compact download count, e.g. <c>44.1k</c>.</summary>
    public string DownloadsText { get; }

    /// <summary>The model's categories, dot-separated, e.g. <c>furniture · props</c>.</summary>
    public string CategoriesText { get; }

    /// <summary>The hero thumbnail, populated asynchronously after the cell appears.</summary>
    public ImageSource Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>
    /// Fetches the cell's thumbnail (through the catalog service's cache) and hands it to the
    /// Image control. Failures leave the placeholder showing; scrolling back re-tries once more.
    /// </summary>
    public async Task LoadThumbnailAsync()
    {
        if (_thumbnail != null || _thumbnailFailed) { return; }

        try
        {
            var bytes = await _catalog.GetThumbnailAsync(Asset, System.Threading.CancellationToken.None);

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

    private static string BuildAuthorCredit(PolyHavenAsset asset)
    {
        var authors = asset.Authors?.Keys.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray() ?? [];
        return authors.Length == 0 ? "Poly Haven" : string.Join(", ", authors);
    }

    private static string BuildCategoriesText(PolyHavenAsset asset)
    {
        var categories = (asset.Categories ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(2)
            .ToArray();
        return string.Join(" · ", categories);
    }
}
