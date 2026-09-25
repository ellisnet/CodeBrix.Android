using CodeBrix.Platform.Simple;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml;
using PolyHavenBrowser.CreateDocument;
using PolyHavenBrowser.Helpers;
using PolyHavenBrowser.PolyHavenApiClient;
using PolyHavenBrowser.Rendering;
using PolyHavenBrowser.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

// ReSharper disable once CheckNamespace
namespace PolyHavenBrowser.ViewModels;

/// <summary>One label/value row of the Model View's facts panel.</summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class ModelFact
{
    /// <summary>Creates a fact row.</summary>
    public ModelFact(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>The fact's label, e.g. <c>Triangles</c>.</summary>
    public string Label { get; }

    /// <summary>The fact's display value, e.g. <c>12,204</c>.</summary>
    public string Value { get; }
}

/// <summary>
/// Drives the whole PolyHavenBrowser main page. The page has two modes, toggled by
/// visibility: the <b>Browsing View</b> (a lazily-loading catalog grid of every Poly Haven
/// 3D model, with search, sorting and a download-folder picker) and the <b>Model View</b>
/// (everything the API knows about one downloaded model, beside an interactive OpenGL
/// preview the user can rotate and zoom).
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, ICatalogGridBridge, IModelViewBridge
{
    private const string SortMostPopular = "Most popular";
    private const string SortNewest = "Newest";
    private const string SortNameAscending = "Name A–Z";

    private readonly ModelCatalogService _catalog;
    private readonly ModelDownloadService _downloads;
    private readonly DocumentBackdropService _backdrops;
    private readonly IModelLoader _modelLoader;

    private IReadOnlyList<PolyHavenAsset> _allModels = [];
    private string _selectedSortOption = SortMostPopular;
    private CancellationTokenSource _searchDebounce;

    private string _downloadFolder;

    private LoadedModel _currentModel;
    private PolyHavenAsset _currentAsset;
    private ModelFileStats _currentStats;

    /// <summary>Creates the view model and begins loading the model catalog.</summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; }

        _catalog = GetService<ModelCatalogService>();
        _downloads = GetService<ModelDownloadService>();
        _backdrops = GetService<DocumentBackdropService>();
        _modelLoader = GetService<IModelLoader>();

        _ = LoadCatalogAsync();
    }

    #region | Browsing View: catalog, search, sort |

    /// <summary>The lazily-loading catalog cells the grid displays.</summary>
    public ModelCellCollection Cells
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// The page's catalog scroller, filled in by the page: a new cell collection means the
    /// user re-searched or re-sorted, so the grid jumps back to the top.
    /// </summary>
    public Action ScrollCatalogToTop { get; set; }

    /// <summary>Whether the initial catalog fetch is still in flight.</summary>
    [AffectsProperties(nameof(CatalogLoadingVisibility))]
    public bool IsCatalogLoading
    {
        get;
        private set => SetProperty(ref field, value);
    } = true;

    /// <summary>The visibility of the initial catalog-loading indicator.</summary>
    public Visibility CatalogLoadingVisibility => IsCatalogLoading ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The status line shown while the catalog loads (or when it fails).</summary>
    public string CatalogStatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Loading the Poly Haven model catalog…";

    /// <summary>The result-count caption, e.g. <c>312 models</c>.</summary>
    public string ResultCountText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The search text; matching cells re-populate shortly after each keystroke.</summary>
    public string SearchText
    {
        get;
        set
        {
            var newValue = value ?? string.Empty;
            if (newValue == field) { return; }

            SetProperty(ref field, newValue);
            _ = DebounceRebuildAsync();
        }
    } = string.Empty;

    /// <summary>The sort options shown in the sort selector.</summary>
    public List<string> SortOptions { get; } = [SortMostPopular, SortNewest, SortNameAscending];

    /// <summary>The selected sort option; changing it re-populates the grid.</summary>
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _selectedSortOption) { return; }

            SetProperty(ref _selectedSortOption, value);
            RebuildCells();
        }
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            _allModels = await _catalog.GetModelsAsync(CancellationToken.None);
            IsCatalogLoading = false;
            RebuildCells();
        }
        catch (Exception ex)
        {
            CatalogStatusText = $"Could not load the Poly Haven catalog: {ex.Message}";
        }
    }

    //Re-applies search + sort and swaps in a fresh lazily-loading cell collection.
    private void RebuildCells()
    {
        if (_allModels.Count == 0 && IsCatalogLoading) { return; }

        var matching = ModelCatalogService.SortAndFilter(_allModels, SelectedSortOrder, SearchText);
        Cells = new ModelCellCollection(matching,
            asset => new ModelCellViewModel(asset, _catalog, DownloadAsync, () => !IsDownloading));
        ResultCountText = matching.Count == 1 ? "1 model" : $"{matching.Count:N0} models";

        //The grid is showing a different set of models now, so start it at the first one.
        ScrollCatalogToTop?.Invoke();
    }

    /// <summary>
    /// Tells the view model where the catalog grid has scrolled to, so the cell collection
    /// can decide whether to materialize its next batch. The page supplies only the scroll
    /// geometry it can see; how near the bottom is near enough, and how big a batch is, are
    /// <see cref="ModelCellCollection"/>'s own policy.
    /// </summary>
    /// <param name="extentHeight">The full scrollable height of the grid.</param>
    /// <param name="verticalOffset">How far down that extent the viewport currently sits.</param>
    /// <param name="viewportHeight">The height of the visible part of the grid.</param>
    public void NotifyCatalogScrolled(double extentHeight, double verticalOffset, double viewportHeight) =>
        Cells?.RequestMoreIfNearEnd(extentHeight, verticalOffset, viewportHeight);

    private CatalogSortOrder SelectedSortOrder => _selectedSortOption switch
    {
        SortNewest => CatalogSortOrder.Newest,
        SortNameAscending => CatalogSortOrder.NameAscending,
        _ => CatalogSortOrder.MostPopular,
    };

    //Waits a beat after the last keystroke before rebuilding, so typing stays smooth. It
    //returns a Task rather than being `async void`, so a failure is captured in the task
    //instead of escaping onto the UI thread from a property setter.
    private async Task DebounceRebuildAsync()
    {
        _searchDebounce?.Cancel();
        var debounce = new CancellationTokenSource();
        _searchDebounce = debounce;
        try
        {
            await Task.Delay(300, debounce.Token);
            RebuildCells();
        }
        catch (OperationCanceledException)
        {
            //Superseded by more typing.
        }
    }

    #endregion

    #region | Download folder |

    /// <summary>Whether the user has chosen a download folder yet.</summary>
    public bool HasDownloadFolder => !string.IsNullOrWhiteSpace(_downloadFolder);

    /// <summary>The folder-picker button's caption: an invitation, or the chosen path.</summary>
    public string DownloadFolderLabel => HasDownloadFolder ? _downloadFolder : "Choose download folder…";

    /// <summary>Opens the folder picker to choose where models download to.</summary>
    public SimpleCommand PickFolderCommand => field ??=
        new SimpleCommand((Func<object, Task>)(_ => PickFolderAsync()));

    private async Task PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) { return; }

        //Same encoding trap as the save picker: a folder called "My Models" would otherwise
        //  come back as "My%20Models" and every download would go to the wrong place.
        _downloadFolder = FileDialogHelper.ToFileSystemPath(folder.Path);
        NotifyPropertyChanged(nameof(HasDownloadFolder));
        NotifyPropertyChanged(nameof(DownloadFolderLabel));
    }

    #endregion

    #region | Downloading |

    /// <summary>Whether a model download is in flight (drives the bottom progress bar).</summary>
    [AffectsProperties(nameof(DownloadBarVisibility))]
    public bool IsDownloading
    {
        get;
        private set
        {
            SetProperty(ref field, value);

            //The download gate lives on each cell's own command; tell every materialized
            //cell to re-query it. (Cells materialized later evaluate the gate fresh anyway.)
            if (Cells is { } cells)
            {
                foreach (var cell in cells) { cell.NotifyCanDownloadChanged(); }
            }
        }
    }

    /// <summary>The bottom progress bar's visibility.</summary>
    public Visibility DownloadBarVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The download progress in [0, 100].</summary>
    public double DownloadProgress
    {
        get;
        private set
        {
            //No SetProperty overload takes a double; compare-and-notify by hand.
            if (field.Equals(value)) { return; }
            field = value;
            NotifyPropertyChanged(nameof(DownloadProgress));
        }
    }

    /// <summary>The caption beside the bottom progress bar, e.g. the downloading model's name.</summary>
    public string DownloadStatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    //Each cell owns its Download command; this is the shared implementation the cells'
    //commands delegate to (see the cell factory in RebuildCells). With no download folder
    //chosen yet, explains itself with a dialog instead.
    private async Task DownloadAsync(ModelCellViewModel cell)
    {
        if (cell == null || IsDownloading) { return; }

        if (!HasDownloadFolder)
        {
            using (var alert = CreateDialog(
                "Downloading is disabled until you choose a download folder.\n\n" +
                "Use the folder button at the top of the window to pick where models should be saved.",
                "Choose a Download Folder"))
            {
                _ = await alert.ShowAsync();
            }
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = $"Downloading “{cell.Title}”…";
        try
        {
            var progress = new Progress<double>(fraction => DownloadProgress = fraction * 100d);
            var downloaded = await _downloads.EnsureDownloadedAsync(
                cell.Asset, _downloadFolder, progress, CancellationToken.None);

            await OpenModelViewAsync(cell.Asset, downloaded);
        }
        catch (Exception ex)
        {
            await ShowError(ex, $"Could not download “{cell.Title}”.");
        }
        finally
        {
            IsDownloading = false;
            DownloadStatusText = string.Empty;
        }
    }

    #endregion

    #region | Model View |

    /// <summary>
    /// The page's Model View hook, filled in by the page: the view model invokes it as the
    /// Model View opens so the page can report a 3D preview that could not initialize.
    /// </summary>
    public Action ModelViewOpened { get; set; }

    /// <summary>Whether the Model View is active (otherwise the Browsing View shows).</summary>
    [AffectsCommands(nameof(DocumentCommand))]
    [AffectsProperties(nameof(BrowsingViewVisibility), nameof(ModelViewVisibility))]
    public bool IsModelViewActive
    {
        get;
        private set
        {
            var wasActive = field;
            SetProperty(ref field, value);

            //Only the page can see whether the preview canvas managed to initialize OpenGL,
            //so it is told when there is something to look at.
            if (value && !wasActive) { ModelViewOpened?.Invoke(); }
        }
    }

    /// <summary>The Browsing View's visibility.</summary>
    public Visibility BrowsingViewVisibility => IsModelViewActive ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The Model View's visibility.</summary>
    public Visibility ModelViewVisibility => IsModelViewActive ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The model shown in the 3D preview (null while browsing); the preview control binds to this.</summary>
    public LoadedModel CurrentModel => _currentModel;

    /// <summary>The Model View's title (the model's name).</summary>
    public string ModelTitle
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The creator credit line under the title.</summary>
    public string ModelAuthorLine
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The full (synthesized) description paragraph.</summary>
    public string ModelDescription
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The model's tags as one flowing line.</summary>
    public string ModelTagsText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The label/value fact rows shown beside the 3D preview.</summary>
    public ObservableCollection<ModelFact> ModelFacts { get; } = new();

    /// <summary>How to drive the 3D preview, shown under the canvas.</summary>
    public string ViewerHint => "drag to rotate · scroll to zoom";

    //The Model View's info pane is a fixed column beside the viewer while the window is
    //  landscape, and half the height above it when the window is portrait.
    private const double InfoPaneLandscapeWidth = 420d;
    private const float InfoPaneStackedHeightBasis = 0.5f;

    /// <summary>
    /// Whether the window is taller than it is wide, so the Model View stacks its info pane
    /// above the 3D viewer instead of placing the two side by side.
    /// </summary>
    [AffectsProperties(nameof(ModelInfoPaneWidth), nameof(ModelInfoPaneMargin))]
    public bool IsModelViewStacked
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// The Model View info pane's width: an explicit column while the panes sit side by side
    /// (so the text inside measures - and wraps - against it), and automatic when they stack,
    /// where the pane is sized by <see cref="ModelInfoPaneStackedHeightBasis"/> instead.
    /// </summary>
    public double ModelInfoPaneWidth => IsModelViewStacked ? double.NaN : InfoPaneLandscapeWidth;

    /// <summary>The share of the Model View's height the info pane takes when the panes stack.</summary>
    public float ModelInfoPaneStackedHeightBasis => InfoPaneStackedHeightBasis;

    /// <summary>The gap the info pane leaves for the 3D viewer: below it when stacked, beside it otherwise.</summary>
    public Thickness ModelInfoPaneMargin => IsModelViewStacked
        ? new Thickness(0, 0, 0, 20)
        : new Thickness(0, 0, 20, 0);

    /// <summary>
    /// Tells the view model the window's new size, so it can decide which way round the
    /// Model View's panes belong. The page reads the pane properties back and applies them
    /// to the layout panel.
    /// </summary>
    /// <param name="width">The window's new width.</param>
    /// <param name="height">The window's new height.</param>
    public void NotifyWindowSizeChanged(double width, double height) =>
        IsModelViewStacked = width < height;

    /// <summary>Returns from the Model View to the Browsing View.</summary>
    public SimpleCommand BackCommand => field ??=
        new SimpleCommand((Func<object, Task>)(_ => { CloseModelView(); return Task.CompletedTask; }));

    /// <summary>
    /// Creates the glossy marketing one-sheet PDF for the model on display: a native
    /// "Save PDF as…" picker chooses the destination, then the document generates and
    /// saves in one go, with progress reported on <see cref="DocumentStatusText"/>.
    /// </summary>
    public SimpleCommand DocumentCommand => field ??=
        new SimpleCommand(CanCreateDocument, (Func<object, Task>)(_ => CreateDocumentAsync()));

    private async Task OpenModelViewAsync(PolyHavenAsset asset, DownloadedModel downloaded)
    {
        DownloadStatusText = $"Loading “{asset.Name ?? downloaded.Slug}”…";

        //Parse the glTF and gather its stats off the UI thread; GPU upload happens lazily
        //at first paint.
        var (model, stats) = await Task.Run(() =>
        {
            var loaded = _modelLoader.LoadFile(downloaded.GltfPath);
            return (loaded, ModelFileStats.FromLoadedModel(loaded, downloaded.ModelFolder));
        });

        // Hand the model to the preview control via its bound CurrentModel; the control frames
        // the camera and repaints itself. The GPU upload happens lazily at its first render.
        _currentModel = model;
        _currentAsset = asset;
        _currentStats = stats;

        ModelTitle = string.IsNullOrWhiteSpace(asset.Name) ? downloaded.Slug : asset.Name;
        ModelAuthorLine = BuildAuthorLine(asset);
        ModelDescription = ModelDescriptionBuilder.BuildFullDescription(asset, stats);
        ModelTagsText = string.Join("   ", (asset.Tags ?? []).Select(t => $"#{t}"));
        PopulateFacts(asset, stats);
        DocumentStatusText = string.Empty;

        NotifyPropertyChanged(nameof(CurrentModel));
        IsModelViewActive = true;
    }

    private void CloseModelView()
    {
        IsModelViewActive = false;

        _currentModel = null;
        _currentAsset = null;
        _currentStats = null;
        NotifyPropertyChanged(nameof(CurrentModel));
    }

    /// <summary>
    /// Shows a dialog explaining why the 3D preview cannot render. Called from the view when
    /// the Model View is active and the preview's GLCanvasElement reports that its OpenGL
    /// initialization failed (e.g. on systems without OpenGL 3.0+ support, where the preview
    /// would otherwise just be an empty pane).
    /// </summary>
    public async Task ShowRenderingUnavailableAsync(GLInitializationState state)
    {
        var message =
            "The interactive 3D model preview is not available on this system, so the preview " +
            "pane will stay empty.\n\n";

        //On Windows, the usual cause is a missing OpenGL driver; Microsoft's free "OpenCL and
        //OpenGL Compatibility Pack" adds one. Only show this hint when actually on Windows.
        var osInfo = await SimpleOsInfo.GatherInfo(withConsoleOutput: false);
        if (osInfo.IsWindows)
        {
            message +=
                "On Windows, you may be able to fix this by installing the free Microsoft " +
                "\"OpenCL and OpenGL Compatibility Pack\". Download and install it from:\n" +
                "https://apps.microsoft.com/detail/9NQPSL29BFFF\n\n" +
                "After installing it, restart this app.\n\n";
        }

        message += $"Details:\nStatus: {state.Status}\n{state.FailedReason ?? "(none reported)"}";

        using var dialog = CreateDialog(message, "3D Preview Unavailable");
        _ = await dialog.ShowAsync();
    }

    private void PopulateFacts(PolyHavenAsset asset, ModelFileStats stats)
    {
        ModelFacts.Clear();

        if (asset.Categories is { Length: > 0 })
        {
            ModelFacts.Add(new ModelFact("Categories", string.Join(", ", asset.Categories)));
        }
        ModelFacts.Add(new ModelFact("Published", asset.DatePublishedUtc.ToString("MMMM d, yyyy")));
        ModelFacts.Add(new ModelFact("Downloads", $"{asset.DownloadCount:N0}"));
        if (asset.MaxResolution is { Length: > 0 })
        {
            ModelFacts.Add(new ModelFact("Max texture size", string.Join(" × ", asset.MaxResolution)));
        }
        ModelFacts.Add(new ModelFact("Triangles", $"{stats.Triangles:N0}"));
        ModelFacts.Add(new ModelFact("Vertices", $"{stats.Vertices:N0}"));
        ModelFacts.Add(new ModelFact("Materials",
            stats.TexturedMaterials > 0 ? $"{stats.Materials:N0} ({stats.TexturedMaterials:N0} textured)" : $"{stats.Materials:N0}"));
        ModelFacts.Add(new ModelFact("Size on disk", ModelDescriptionBuilder.FormatBytes(stats.DiskBytes)));
        ModelFacts.Add(new ModelFact("License", "CC0 (public domain)"));
    }

    private static string BuildAuthorLine(PolyHavenAsset asset)
    {
        var authors = asset.Authors?.Keys.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray() ?? [];
        return authors.Length == 0 ? "from Poly Haven" : $"by {string.Join(", ", authors)}   ·   Poly Haven";
    }

    #endregion

    #region | Document one-sheet |

    //The product-shot render sizes: the hero at its layout box's aspect, the gallery
    //  shots at theirs — both at 4x the box's point size, comfortably print-resolution.
    private const uint HeroShotWidth = 1344;
    private const uint HeroShotHeight = 1120;
    private const uint GalleryShotWidth = 496;
    private const uint GalleryShotHeight = 416;

    /// <summary>The Model View footer's status line for document creation.</summary>
    public string DocumentStatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Whether a marketing one-sheet is being built right now (re-entry gate).</summary>
    [AffectsCommands(nameof(DocumentCommand))]
    public bool IsCreatingDocument
    {
        get;
        private set => SetProperty(ref field, value);
    }

    private bool CanCreateDocument() => IsModelViewActive && !IsCreatingDocument;

    private async Task CreateDocumentAsync()
    {
        if (!CanCreateDocument()) { return; }

        //Snapshot everything the document needs: the user can navigate Back (or open a
        //  different model) while it builds, and the run continues from this snapshot.
        var asset = _currentAsset;
        var stats = _currentStats;
        var model = _currentModel;
        if (asset == null || stats == null || model == null) { return; }

        var title = ModelTitle;
        var authorLine = ModelAuthorLine;
        var description = ModelDescription;
        var facts = ModelFacts.Select(f => new MarketingSheetFact(f.Label, f.Value)).ToList();
        var downloadFolder = _downloadFolder;

        //The native "Save PDF as…" dialog decides the destination; cancelling is a no-op.
        string outputPath;
        try
        {
            outputPath = await PickSavePdfPathAsync(GetSuggestedFileName(title));
            if (string.IsNullOrWhiteSpace(outputPath)) { return; }
        }
        catch (NotSupportedException)
        {
            //Some heads register no picker — there is no window to host a dialog.
            await ShowInfo("File dialogs are not supported on this head, so there is nowhere " +
                "to choose where the document should be saved.");
            return;
        }
        catch (Exception e)
        {
            await ShowError(e, "Could not open the file dialog.");
            return;
        }

        IsCreatingDocument = true;
        var saved = false;
        try
        {
            //Stage 1: the CC0 backdrop textures (cached beside the downloaded models).
            DocumentStatusText = "Setting the stage (downloading backdrop textures)…";
            var backdropCache = Path.Combine(
                string.IsNullOrWhiteSpace(downloadFolder) ? Path.GetTempPath() : downloadFolder,
                "_document-backdrops");
            var stages = await _backdrops.GetStagesAsync(backdropCache, CancellationToken.None);

            //Stage 2: build the photography sets (pure CPU — off the UI thread).
            var scenes = await Task.Run(() => (
                Tabletop: ShotSceneBuilder.Build(model, stages.Tabletop),
                Light: ShotSceneBuilder.Build(model, stages.Light),
                Dark: ShotSceneBuilder.Build(model, stages.Dark)));

            //Stage 3: the product shots, on this head's off-screen GL context. GL work must
            //  stay on the UI thread; MakeCurrent saves/restores the head's own context.
            //  With no GL available the sheet still composes, led by the catalog thumbnail.
            DocumentStatusText = "Rendering product shots…";
            byte[] heroShot = null;
            var galleryShots = new List<MarketingSheetShot>();
            if (OffscreenGLContext.TryCreate(GetXamlRoot(), out var glContext))
            {
                using (glContext)
                using (glContext.MakeCurrent())
                using (var shotRenderer = new ModelShotRenderer(glContext.Gl))
                {
                    heroShot = shotRenderer.RenderPng(
                        scenes.Tabletop, stages.Tabletop, ShotAngle.Hero, HeroShotWidth, HeroShotHeight);
                    galleryShots.Add(new MarketingSheetShot("Front", shotRenderer.RenderPng(
                        scenes.Light, stages.Light, ShotAngle.Front, GalleryShotWidth, GalleryShotHeight)));
                    galleryShots.Add(new MarketingSheetShot("Side", shotRenderer.RenderPng(
                        scenes.Light, stages.Light, ShotAngle.Side, GalleryShotWidth, GalleryShotHeight)));
                    galleryShots.Add(new MarketingSheetShot("Back", shotRenderer.RenderPng(
                        scenes.Dark, stages.Dark, ShotAngle.Back, GalleryShotWidth, GalleryShotHeight)));
                    galleryShots.Add(new MarketingSheetShot("Top", shotRenderer.RenderPng(
                        scenes.Dark, stages.Dark, ShotAngle.Top, GalleryShotWidth, GalleryShotHeight)));
                }
            }

            //Stage 4: the catalog thumbnail (in-memory cached), then compose and save.
            DocumentStatusText = "Composing the one-sheet…";
            byte[] thumbnail = null;
            try
            {
                thumbnail = await _catalog.GetThumbnailAsync(asset, CancellationToken.None);
            }
            catch
            {
                //The sheet composes without it; the accent color falls back to its default.
            }

            var request = BuildMarketingSheetRequest(
                asset, stats, title, authorLine, description, facts, thumbnail, heroShot, galleryShots);
            await Task.Run(() => new MarketingSheetCreator().CreateToFile(request, outputPath));

            DocumentStatusText = $"Saved: {outputPath}";
            saved = true;
        }
        catch (Exception e)
        {
            DocumentStatusText = string.Empty;
            await ShowError(e, $"Could not create the marketing one-sheet for “{title}”.");
        }
        finally
        {
            IsCreatingDocument = false;
        }

        if (saved)
        {
            //Say so plainly: creating the sheet takes a while, and the footer status line is
            //  easy to miss. Announced after the finally block so the Document button is live
            //  again by the time the user dismisses this.
            using var alert = CreateDialog(
                $"The marketing one-sheet for “{title}” has been created.\n\n" +
                $"It was saved to:\n{outputPath}",
                "Document Created");
            _ = await alert.ShowAsync();
        }
    }

    private static async Task<string> PickSavePdfPathAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".pdf",
        };
        picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });

        var file = await picker.PickSaveFileAsync();
        if (file == null) { return null; }

        //Some heads percent-encode the path they return, which would save "One Sheet.pdf" as
        //  "One%20Sheet.pdf"; decode it before anything touches the disk.
        var path = FileDialogHelper.ToFileSystemPath(file.Path);

        //The picker leaves an empty placeholder file at a brand-new path; remove it so the
        //  chosen path behaves like a pure destination.
        FileDialogHelper.RemoveEmptyPlaceholder(path);
        return path;
    }

    private static string GetSuggestedFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return (cleaned.Length == 0 ? "Model" : cleaned) + " one-sheet.pdf";
    }

    private static MarketingSheetRequest BuildMarketingSheetRequest(
        PolyHavenAsset asset, ModelFileStats stats, string title, string authorLine,
        string description, List<MarketingSheetFact> facts, byte[] thumbnail,
        byte[] heroShot, List<MarketingSheetShot> galleryShots)
    {
        var maxTextureLabel = asset.MaxResolution is { Length: > 0 }
            ? $"{asset.MaxResolution.Max() / 1024}k"
            : string.Empty;

        return new MarketingSheetRequest
        {
            ModelName = title,
            AuthorLine = authorLine,
            Description = description,
            Facts = facts,
            Tags = asset.Tags ?? [],
            AssetUrl = string.IsNullOrWhiteSpace(asset.Id) ? string.Empty : $"https://polyhaven.com/a/{asset.Id}",
            Category = asset.Categories?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty,
            CatalogThumbnailBytes = thumbnail,
            HeroShotBytes = heroShot,
            GalleryShots = galleryShots,
            TriangleCount = stats.Triangles,
            VertexCount = stats.Vertices,
            MaterialCount = stats.Materials,
            MaxTextureLabel = maxTextureLabel,
            DownloadCount = asset.DownloadCount,
            PublishedUtc = asset.DatePublishedUtc.UtcDateTime,
        };
    }

    #endregion
}
