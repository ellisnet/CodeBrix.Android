using CodeBrix.Platform.Simple;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using KenneyAssetBrowser.AssetRead;
using KenneyAssetBrowser.AssetRead.Models;
using KenneyAssetBrowser.Helpers;
using KenneyAssetBrowser.Rendering;
using KenneyAssetBrowser.Services;
using KenneyAssetBrowser.Settings;
using Microsoft.UI.Xaml;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>One label/value row of the viewer's facts panel.</summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class AssetFact
{
    /// <summary>Creates a fact row.</summary>
    public AssetFact(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>The fact's label, e.g. <c>Dimensions</c>.</summary>
    public string Label { get; }

    /// <summary>The fact's display value, e.g. <c>128 × 128 px</c>.</summary>
    public string Value { get; }
}

/// <summary>
/// Drives the whole KenneyAssetBrowser main page. The page has two modes, toggled by
/// visibility: the <b>Browsing View</b> (the bundle sidebar beside a lazily-loading grid
/// of the selected bundle's assets, with search and a category filter) and the
/// <b>Viewer View</b> (one asset up close: a zoomable 2D canvas for images and
/// spritesheets, an interactive OpenGL preview for 3D models, and a text pane for
/// documents, beside a facts panel).
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IImageCanvasBridge, IAudioPlayerBridge,
    ICatalogGridBridge, IViewerPaneBridge
{
    /// <summary>The settings.sqlite key holding the user's chosen assets folder.</summary>
    public const string AssetsFolderKey = "KenneyAssetBrowser.Settings.AssetsFolder";

    /// <summary>The settings.sqlite key holding the file name of the last-browsed bundle.</summary>
    public const string LastBundleKey = "KenneyAssetBrowser.Settings.LastBundleFile";

    private const string AllCategories = "All categories";
    private const string CategoryModels = "3D Models";
    private const string CategorySheets = "Spritesheets";

    //Fluent-symbol glyphs for the cell placeholders (resolved through the symbols font
    //  every CodeBrix.Platform application ships)
    private const string ModelGlyph = "";
    private const string ImageGlyph = "";
    private const string AtlasGlyph = "";
    private const string DocumentGlyph = "";
    private const string VectorGlyph = "";
    private const string AudioGlyph = "";
    private const string FontGlyph = "";
    private const string ArchiveGlyph = "";
    private const string MapGlyph = "";

    private enum ViewerMode { None, Image, Model, Text, Audio }

    private readonly AssetCatalogService _catalogService;

    private AssetFolderCatalog _catalog;
    private AssetBundle _selectedBundle;
    private BundleArchive _archive;
    private string _assetsFolder;
    private string _selectedCategory = AllCategories;
    private bool _suppressCategoryRebuild;
    private CancellationTokenSource _searchDebounce;

    private ViewerMode _viewerMode = ViewerMode.None;
    private SKBitmap _viewerBitmap;
    private LoadedModel _currentModel;

    //Whether the current clip has run to its end. A finished clip leaves the transport parked
    //at the end, where Play() has nothing left to play, so the next Play rewinds first.
    private bool _audioPlaybackEnded;

    //How close to the duration still counts as "parked at the end". The player refreshes its
    //position on an interval (150 ms by default), so the last value it reports before ending
    //can sit just short of the duration.
    private static readonly TimeSpan AudioEndTolerance = TimeSpan.FromMilliseconds(250);

    /// <summary>Creates the view model and, when an assets folder is already configured, loads its catalog.</summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        _catalogService = GetService<AssetCatalogService>();

        _assetsFolder = SettingsService.Get<string>(AssetsFolderKey);
        if (HasAssetsFolder)
        {
            _ = ReloadCatalogAsync();
        }
    }

    #region | Assets folder |

    /// <summary>Whether the user has chosen their bundle folder yet.</summary>
    public bool HasAssetsFolder => !string.IsNullOrWhiteSpace(_assetsFolder);

    /// <summary>The folder-picker button's caption: an invitation, or the chosen path.</summary>
    public string AssetsFolderLabel => HasAssetsFolder ? _assetsFolder : "Choose assets folder…";

    /// <summary>The first-launch prompt's visibility (shown until an assets folder is chosen).</summary>
    public Visibility FolderPromptVisibility => HasAssetsFolder ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The catalog area's visibility (the inverse of the first-launch prompt).</summary>
    public Visibility CatalogAreaVisibility => HasAssetsFolder ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Opens the folder picker to choose where the bundle zips live; the choice persists in settings.sqlite.</summary>
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

        _assetsFolder = folder.Path;
        SettingsService.Set(AssetsFolderKey, _assetsFolder);
        NotifyPropertyChanged(nameof(HasAssetsFolder));
        NotifyPropertyChanged(nameof(AssetsFolderLabel));
        NotifyPropertyChanged(nameof(FolderPromptVisibility));
        NotifyPropertyChanged(nameof(CatalogAreaVisibility));

        await ReloadCatalogAsync();
    }

    #endregion

    #region | Catalog and bundle sidebar |

    /// <summary>The bundle cards shown in the sidebar.</summary>
    public ObservableCollection<BundleCellViewModel> BundleCells { get; } = new();

    /// <summary>Whether the bundle catalog is (re)loading.</summary>
    public bool IsCatalogLoading
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(CatalogLoadingVisibility));
        }
    }

    /// <summary>The catalog-loading indicator's visibility.</summary>
    public Visibility CatalogLoadingVisibility => IsCatalogLoading ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The sidebar caption, e.g. <c>8 bundles</c>.</summary>
    public string BundleCountText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Warnings for zip files that could not be read (empty when all loaded cleanly).</summary>
    public string CatalogWarningsText
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(CatalogWarningsVisibility));
        }
    } = string.Empty;

    /// <summary>The warnings caption's visibility.</summary>
    public Visibility CatalogWarningsVisibility =>
        string.IsNullOrEmpty(CatalogWarningsText) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Shows the selected bundle's License.txt in a dialog.</summary>
    public SimpleCommand ShowLicenseCommand => field ??=
        new SimpleCommand((Func<object, Task>)(_ => ShowLicenseAsync()));

    //The constructor starts this without awaiting it, so a failure has nowhere to surface:
    //it becomes the sidebar's caption rather than an unobserved task.
    private async Task ReloadCatalogAsync()
    {
        try
        {
            await ReloadCatalogCoreAsync();
        }
        catch (Exception ex)
        {
            IsCatalogLoading = false;
            BundleCountText = $"Could not read this folder: {ex.Message}";
        }
    }

    private async Task ReloadCatalogCoreAsync()
    {
        IsCatalogLoading = true;
        CloseViewer();
        DisposeArchive();
        _selectedBundle = null;
        BundleCells.Clear();
        Cells = new AssetCellCollection([]);
        ResultCountText = string.Empty;

        _catalog = await _catalogService.LoadCatalogAsync(_assetsFolder);

        CatalogWarningsText = _catalog.Warnings.Count == 0
            ? string.Empty
            : "Could not read: " + string.Join("; ", _catalog.Warnings);

        foreach (var bundle in _catalog.Bundles)
        {
            var cellBundle = bundle;
            var cell = new BundleCellViewModel(cellBundle, SelectBundleAsync,
                cellBundle.PreviewEntryPath == null
                    ? null
                    : () => _catalogService.ReadEntryBytesAsync(cellBundle, cellBundle.PreviewEntryPath));
            BundleCells.Add(cell);

            //Fire-and-forget: the card fetches its cover in the background
            _ = cell.LoadThumbnailAsync();
        }

        BundleCountText = _catalog.Bundles.Count == 0
            ? "No asset bundles (.zip) found in this folder"
            : FormatHelper.FormatCount(_catalog.Bundles.Count, "bundle");
        IsCatalogLoading = false;

        //Restore the bundle the user browsed last time, or start with the first one
        if (BundleCells.Count > 0)
        {
            var lastBundleFile = SettingsService.Get<string>(LastBundleKey);
            var restored = BundleCells.FirstOrDefault(c =>
                c.Bundle.FileName.Equals(lastBundleFile ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            await SelectBundleAsync(restored ?? BundleCells[0]);
        }
    }

    private async Task SelectBundleAsync(BundleCellViewModel cell)
    {
        if (cell == null) { return; }

        foreach (var bundleCell in BundleCells)
        {
            bundleCell.IsSelected = bundleCell == cell;
        }

        if (cell.Bundle == _selectedBundle) { return; }

        DisposeArchive();
        _selectedBundle = cell.Bundle;
        _archive = await _catalogService.OpenArchiveAsync(cell.Bundle);
        SettingsService.Set(LastBundleKey, cell.Bundle.FileName);

        RebuildCategories();
        RebuildCells();
    }

    private async Task ShowLicenseAsync()
    {
        var bundle = _selectedBundle;
        if (bundle == null) { return; }

        var licenseText = string.IsNullOrWhiteSpace(bundle.LicenseText)
            ? "This bundle contains no License.txt file."
            : bundle.LicenseText.Trim();

        using var dialog = CreateDialog(licenseText, $"{bundle.DisplayName} — License");
        _ = await dialog.ShowAsync();
    }

    private void DisposeArchive()
    {
        var archive = _archive;
        _archive = null;
        archive?.Dispose();
    }

    //Reads entry bytes from the selected bundle's open archive on a worker thread
    private Task<byte[]> ReadArchiveBytesAsync(string entryPath)
    {
        var archive = _archive;
        return archive == null
            ? Task.FromResult<byte[]>(null)
            : Task.Run(() => archive.ReadEntryBytes(entryPath));
    }

    #endregion

    #region | Search and category filter |

    /// <summary>The search text; matching cells re-populate shortly after each keystroke.</summary>
    public string SearchText
    {
        get;
        set
        {
            var newValue = value ?? string.Empty;
            if (newValue == field) { return; }

            SetProperty(ref field, newValue);
            DebounceRebuild();
        }
    } = string.Empty;

    /// <summary>The category filter options for the selected bundle.</summary>
    public List<string> Categories
    {
        get;
        private set => SetProperty(ref field, value);
    } = [AllCategories];

    /// <summary>The selected category filter; changing it re-populates the grid.</summary>
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _selectedCategory) { return; }

            SetProperty(ref _selectedCategory, value);
            if (!_suppressCategoryRebuild) { RebuildCells(); }
        }
    }

    private void RebuildCategories()
    {
        var bundle = _selectedBundle;
        var categories = new List<string> { AllCategories };
        if (bundle != null)
        {
            if (bundle.HasModels) { categories.Add(CategoryModels); }
            if (bundle.HasAtlases) { categories.Add(CategorySheets); }
            foreach (var category in bundle.Categories)
            {
                //The per-format model folders, the sheet folder and the per-model preview
                //  renders are represented by the grouped Model and Spritesheet cards instead
                if (bundle.HasModels &&
                    (category.StartsWith("Models", StringComparison.OrdinalIgnoreCase) ||
                     category.StartsWith("Previews", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (bundle.HasAtlases && category.StartsWith("Spritesheet", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                categories.Add(category);
            }
        }

        _suppressCategoryRebuild = true;
        Categories = categories;
        _selectedCategory = AllCategories;
        NotifyPropertyChanged(nameof(SelectedCategory));
        _suppressCategoryRebuild = false;
    }

    //Waits a beat after the last keystroke before rebuilding, so typing stays smooth.
    private async void DebounceRebuild()
    {
        _searchDebounce?.Cancel();
        var debounce = new CancellationTokenSource();
        _searchDebounce = debounce;
        try
        {
            await Task.Delay(250, debounce.Token);
            RebuildCells();
        }
        catch (OperationCanceledException)
        {
            //Superseded by more typing.
        }
        catch (Exception ex)
        {
            //An async void handler: nothing can catch a failure out here, so the rebuild's
            //own failure becomes the grid's caption instead of escaping.
            ResultCountText = $"Could not filter this bundle: {ex.Message}";
        }
    }

    #endregion

    #region | Browsing grid |

    /// <inheritdoc />
    public Action ScrollCatalogToTop { get; set; }

    /// <summary>The lazily-loading asset cells the grid displays.</summary>
    public AssetCellCollection Cells
    {
        get;
        private set
        {
            SetProperty(ref field, value);

            //A fresh collection means the user switched bundle, searched or re-filtered:
            //the grid goes back to the top.
            ScrollCatalogToTop?.Invoke();
        }
    }

    /// <summary>The result-count caption, e.g. <c>296 assets · Brick Kit</c>.</summary>
    public string ResultCountText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    //Re-applies the category filter and search, and swaps in a fresh lazily-loading collection.
    private void RebuildCells()
    {
        var cellList = BuildCellList();
        Cells = new AssetCellCollection(cellList);
        ResultCountText = _selectedBundle == null
            ? string.Empty
            : $"{FormatHelper.FormatCount(cellList.Count, "asset")} · {_selectedBundle.DisplayName}";
    }

    private List<AssetCellViewModel> BuildCellList()
    {
        var cells = new List<AssetCellViewModel>();
        var bundle = _selectedBundle;
        if (bundle == null) { return cells; }

        var search = SearchText.Trim();
        bool Matches(string name) =>
            search.Length == 0 || name.Contains(search, StringComparison.OrdinalIgnoreCase);
        var all = _selectedCategory == AllCategories;

        if (bundle.HasModels && (all || _selectedCategory == CategoryModels))
        {
            foreach (var model in bundle.ModelAssets)
            {
                if (!Matches(model.Name)) { continue; }
                var cellModel = model;
                cells.Add(new AssetCellViewModel(
                    cellModel.Name, AssetCellKind.Model, "MODEL", ModelGlyph,
                    "3D model", cellModel.FormatList, cellModel, OpenAssetAsync,
                    cellModel.PreviewEntryPath == null
                        ? null
                        : () => ReadArchiveBytesAsync(cellModel.PreviewEntryPath)));
            }
        }

        if (bundle.HasAtlases && (all || _selectedCategory == CategorySheets))
        {
            foreach (var atlas in bundle.Atlases)
            {
                if (!Matches(atlas.Name)) { continue; }
                var cellAtlas = atlas;
                cells.Add(new AssetCellViewModel(
                    cellAtlas.Name, AssetCellKind.Atlas, "SHEET", AtlasGlyph,
                    "Spritesheet", FormatHelper.FormatCount(cellAtlas.Regions.Count, "sprite"),
                    cellAtlas, OpenAssetAsync,
                    () => ReadArchiveBytesAsync(cellAtlas.ImageEntryPath)));
            }
        }

        //Sheet images and their XML files are represented by the Spritesheet cards
        var atlasPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var atlas in bundle.Atlases)
        {
            atlasPaths.Add(atlas.ImageEntryPath);
            atlasPaths.Add(atlas.XmlEntryPath);
        }

        foreach (var entry in bundle.Entries)
        {
            //Model files (and their materials) are represented by the grouped Model cards
            if (entry.Kind is AssetKind.Model3D or AssetKind.Material) { continue; }

            if (all)
            {
                if (entry.Kind == AssetKind.Unknown) { continue; }
                if (atlasPaths.Contains(entry.EntryPath)) { continue; }
                if (bundle.HasModels &&
                    entry.EntryPath.StartsWith("Previews/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else if (!entry.Category.Equals(_selectedCategory, StringComparison.Ordinal))
            {
                continue;
            }

            if (!Matches(entry.FileName)) { continue; }
            cells.Add(CreateEntryCell(entry));
        }

        return cells;
    }

    private AssetCellViewModel CreateEntryCell(AssetEntry entry)
    {
        var subtitle = entry.Category.Length == 0 ? "Bundle root" : entry.Category;
        var sizeText = FormatHelper.FormatBytes(entry.SizeBytes);

        var (kind, kindLabel, glyph) = entry.Kind switch
        {
            AssetKind.Image => (AssetCellKind.Image, "IMAGE", ImageGlyph),
            AssetKind.Document => (AssetCellKind.Document, "DOC", DocumentGlyph),
            AssetKind.Vector => (AssetCellKind.Vector, "VECTOR", VectorGlyph),
            AssetKind.Audio => (AssetCellKind.Audio, "AUDIO", AudioGlyph),
            AssetKind.Font => (AssetCellKind.Font, "FONT", FontGlyph),
            AssetKind.TiledMap => (AssetCellKind.TiledMap, "MAP", MapGlyph),
            AssetKind.Flash => (AssetCellKind.Other, "FLASH", DocumentGlyph),
            AssetKind.SourceFile => (AssetCellKind.Other, "SOURCE", DocumentGlyph),
            AssetKind.EnginePackage => (AssetCellKind.Other, "ENGINE", DocumentGlyph),
            AssetKind.Archive => (AssetCellKind.Other, "ZIP", ArchiveGlyph),
            _ => (AssetCellKind.Other, "FILE", DocumentGlyph),
        };

        var thumbnailLoader = kind switch
        {
            AssetCellKind.Image => (Func<Task<byte[]>>)(() => ReadArchiveBytesAsync(entry.EntryPath)),
            AssetCellKind.Vector => () => ReadSvgThumbnailAsync(entry.EntryPath),
            _ => null,
        };

        return new AssetCellViewModel(
            entry.Name, kind, kindLabel, glyph, subtitle, sizeText, entry, OpenAssetAsync, thumbnailLoader);
    }

    //Rasterizes an SVG entry to PNG bytes for its grid thumbnail
    private async Task<byte[]> ReadSvgThumbnailAsync(string entryPath)
    {
        var bytes = await ReadArchiveBytesAsync(entryPath);
        return bytes == null ? null : await Task.Run(() => SvgImageDecoder.RenderToPngBytes(bytes));
    }

    #endregion

    #region | Viewer |

    /// <inheritdoc />
    public Action ViewerOpened { get; set; }

    /// <summary>Whether the Viewer View is active (otherwise the Browsing View shows).</summary>
    public bool IsViewerActive
    {
        get;
        private set
        {
            var wasActive = field;
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(BrowsingViewVisibility));
            NotifyPropertyChanged(nameof(ViewerViewVisibility));

            //The viewer has just opened: the page checks whether its 3D preview canvas can
            //render at all, which is the one thing about this view only the view knows.
            if (value && !wasActive) { ViewerOpened?.Invoke(); }
        }
    }

    /// <summary>The Browsing View's visibility.</summary>
    public Visibility BrowsingViewVisibility => IsViewerActive ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The Viewer View's visibility.</summary>
    public Visibility ViewerViewVisibility => IsViewerActive ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The viewer's title (the asset's name).</summary>
    public string ViewerTitle
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The line under the viewer title: bundle · category · size.</summary>
    public string ViewerSubtitle
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The label/value fact rows shown beside the viewer.</summary>
    public ObservableCollection<AssetFact> ViewerFacts { get; } = new();

    /// <summary>How to drive the active viewer, shown under it.</summary>
    public string ViewerHint
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The 2D canvas painter the page's SkiaSharp canvas paints with.</summary>
    public ImageCanvasPainter ImagePainter { get; } = new();

    /// <inheritdoc />
    public Action InvalidateImageCanvas { get; set; }

    /// <inheritdoc />
    public Action<Stream> LoadAudioSource { get; set; }

    /// <inheritdoc />
    public Action PlayAudio { get; set; }

    /// <inheritdoc />
    public Action PauseAudio { get; set; }

    /// <inheritdoc />
    public Action StopAudio { get; set; }

    /// <inheritdoc />
    public Action<bool> SetAudioLooping { get; set; }

    /// <inheritdoc />
    public Func<bool> IsAudioPlaying { get; set; }

    /// <inheritdoc />
    public Func<TimeSpan> AudioPosition { get; set; }

    /// <inheritdoc />
    public Func<TimeSpan> AudioDuration { get; set; }

    /// <inheritdoc />
    public Action<TimeSpan> SeekAudio { get; set; }

    /// <summary>Whether a clip is loaded in the player (the transport buttons gate on this).</summary>
    [AffectsCommands(nameof(PlayAudioCommand), nameof(PauseAudioCommand),
        nameof(StopAudioCommand), nameof(ToggleAudioLoopCommand))]
    public bool HasAudioClip
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Starts (or resumes) audio playback; a clip parked at its end replays instead.</summary>
    public SimpleCommand PlayAudioCommand => field ??= new SimpleCommand(CanUseAudioTransport, DoPlayAudio);

    /// <summary>Pauses audio playback.</summary>
    public SimpleCommand PauseAudioCommand => field ??=
        new SimpleCommand(CanUseAudioTransport, () => PauseAudio?.Invoke());

    /// <summary>Stops audio playback and rewinds.</summary>
    public SimpleCommand StopAudioCommand => field ??=
        new SimpleCommand(CanUseAudioTransport, StopAudioPlayback);

    /// <summary>Toggles whether audio playback loops.</summary>
    public SimpleCommand ToggleAudioLoopCommand => field ??= new SimpleCommand(CanUseAudioTransport, () =>
    {
        IsAudioLooping = !IsAudioLooping;
        SetAudioLooping?.Invoke(IsAudioLooping);
    });

    private bool CanUseAudioTransport() => HasAudioClip;

    //Starts (or resumes) the audio clip. A clip that has played through to its end leaves the
    //transport parked at the end, where Play() alone has nothing left to play - so rewind first
    //and let one click replay the clip. Two things deliberately do NOT rewind: a player that is
    //still going (a looping clip reports playback ended on every pass), and a clip the user has
    //scrubbed away from the end since it finished - there, the thumb is the intent, so resume
    //from where they left it.
    private void DoPlayAudio()
    {
        if (!CanUseAudioTransport()) { return; }

        if (_audioPlaybackEnded
            && IsAudioPlaying?.Invoke() == false
            && AudioDuration?.Invoke() > TimeSpan.Zero
            && AudioPosition?.Invoke() >= AudioDuration.Invoke() - AudioEndTolerance)
        {
            SeekAudio?.Invoke(TimeSpan.Zero);
        }

        _audioPlaybackEnded = false;
        PlayAudio?.Invoke();
    }

    //Stops the transport and forgets that the clip was parked at its end
    private void StopAudioPlayback()
    {
        _audioPlaybackEnded = false;
        StopAudio?.Invoke();
    }

    /// <summary>
    /// Tells the view model that the clip has run through to its end, which is what makes the
    /// next Play replay it rather than do nothing. The page forwards its player element's
    /// playback-ended event here in one line.
    /// </summary>
    public void NotifyAudioPlaybackEnded() => _audioPlaybackEnded = true;

    /// <summary>Whether audio playback loops (drives the loop button's caption).</summary>
    public bool IsAudioLooping
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(AudioLoopLabel));
        }
    }

    /// <summary>The loop button's caption.</summary>
    public string AudioLoopLabel => IsAudioLooping ? "Loop: On" : "Loop: Off";

    private AnimatedModel _animatedModel;
    private string _selectedAnimation;

    /// <summary>The animation names of the current model (empty for a static model).</summary>
    public List<string> AnimationNames
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    /// <summary>The animation bar's visibility (3D viewer with an animated model only).</summary>
    public Visibility AnimationBarVisibility =>
        _viewerMode == ViewerMode.Model && AnimationNames.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The selected animation; changing it bakes and starts that clip.</summary>
    public string SelectedAnimation
    {
        get => _selectedAnimation;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _selectedAnimation) { return; }

            SetProperty(ref _selectedAnimation, value);
            _ = BakeSelectedClipAsync(value);
        }
    }

    /// <summary>The baked clip the preview canvas plays (null for a still pose).</summary>
    public ModelAnimationClip CurrentAnimationClip
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Whether the animation is advancing (bound to the preview canvas).</summary>
    public bool IsAnimationPlaying
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(AnimationPlayLabel));
        }
    }

    /// <summary>The animation play/pause button's caption.</summary>
    public string AnimationPlayLabel => IsAnimationPlaying ? "Pause" : "Play";

    /// <summary>Toggles animation playback.</summary>
    public SimpleCommand ToggleAnimationCommand => field ??=
        new SimpleCommand(() => IsAnimationPlaying = !IsAnimationPlaying);

    //Bakes the chosen clip off the UI thread and hands it to the canvas when still current
    private async Task BakeSelectedClipAsync(string animationName)
    {
        var animated = _animatedModel;
        if (animated == null || !animated.HasAnimations) { return; }

        try
        {
            var clip = await Task.Run(() => animated.BakeClip(animationName));
            if (_selectedAnimation == animationName && _animatedModel == animated)
            {
                CurrentAnimationClip = clip;
            }
        }
        catch (Exception)
        {
            //A clip that fails to bake leaves the model in its rest pose.
            CurrentAnimationClip = null;
        }
    }

    private void ResetAnimationState()
    {
        IsAnimationPlaying = false;
        CurrentAnimationClip = null;
        _animatedModel = null;
        _selectedAnimation = null;
        NotifyPropertyChanged(nameof(SelectedAnimation));
        AnimationNames = [];
        NotifyPropertyChanged(nameof(AnimationBarVisibility));
    }

    /// <summary>The model shown in the 3D preview (null otherwise); the preview control binds to this.</summary>
    public LoadedModel CurrentModel => _currentModel;

    /// <summary>The document text shown in the text viewer.</summary>
    public string ViewerText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The caption explaining why an asset has no preview.</summary>
    public string NoPreviewText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The 2D image viewer's visibility.</summary>
    public Visibility ImageViewerVisibility => _viewerMode == ViewerMode.Image ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The 3D model viewer's visibility.</summary>
    public Visibility ModelViewerVisibility => _viewerMode == ViewerMode.Model ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The text viewer's visibility.</summary>
    public Visibility TextViewerVisibility => _viewerMode == ViewerMode.Text ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The no-preview caption's visibility.</summary>
    public Visibility NoPreviewVisibility => _viewerMode == ViewerMode.None ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The audio player panel's visibility.</summary>
    public Visibility AudioViewerVisibility => _viewerMode == ViewerMode.Audio ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The zoom toolbar's visibility (2D viewer only).</summary>
    [AffectsCommands(nameof(ZoomInCommand), nameof(ZoomOutCommand), nameof(ZoomResetCommand))]
    public Visibility ZoomBarVisibility => ImageViewerVisibility;

    /// <summary>The spritesheet region list's visibility.</summary>
    public Visibility RegionListVisibility =>
        _viewerMode == ViewerMode.Image && AtlasRegions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The spritesheet's regions (empty for plain images).</summary>
    public ObservableCollection<AtlasRegionCellViewModel> AtlasRegions { get; } = new();

    /// <summary>Returns from the Viewer View to the Browsing View.</summary>
    public SimpleCommand BackCommand => field ??= new SimpleCommand(CloseViewer);

    private async Task OpenAssetAsync(AssetCellViewModel cell)
    {
        if (cell == null) { return; }

        //Whatever was playing or animating stops before the next asset opens
        StopAudioPlayback();
        HasAudioClip = false;
        ResetAnimationState();

        try
        {
            switch (cell.Kind)
            {
                case AssetCellKind.Model:
                    await OpenModelAsync((ModelAsset)cell.Payload);
                    break;
                case AssetCellKind.Image:
                    await OpenImageAsync((AssetEntry)cell.Payload);
                    break;
                case AssetCellKind.Atlas:
                    await OpenAtlasAsync((SpriteAtlas)cell.Payload);
                    break;
                case AssetCellKind.Document:
                    await OpenDocumentAsync((AssetEntry)cell.Payload);
                    break;
                case AssetCellKind.Audio:
                    await OpenAudioAsync((AssetEntry)cell.Payload);
                    break;
                case AssetCellKind.Vector:
                    await OpenVectorAsync((AssetEntry)cell.Payload);
                    break;
                case AssetCellKind.Font:
                    await OpenFontAsync((AssetEntry)cell.Payload);
                    break;
                case AssetCellKind.TiledMap:
                    await OpenTiledAsync((AssetEntry)cell.Payload);
                    break;
                default:
                    OpenInfoOnly((AssetEntry)cell.Payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            await ShowError(ex, $"Could not open “{cell.Title}”.");
        }
    }

    private async Task OpenImageAsync(AssetEntry entry)
    {
        var bytes = await ReadArchiveBytesAsync(entry.EntryPath)
            ?? throw new InvalidDataException($"The bundle has no entry “{entry.EntryPath}”.");
        var bitmap = await Task.Run(() => LdrImageDecoder.Decode(bytes));

        ReplaceViewerBitmap(bitmap);
        AtlasRegions.Clear();

        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("File", entry.FileName),
            new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
            new AssetFact("Dimensions", $"{bitmap.Width} × {bitmap.Height} px"),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));

        SetViewerMode(ViewerMode.Image, "scroll to zoom · use the buttons to fit or reset");
    }

    private async Task OpenAtlasAsync(SpriteAtlas atlas)
    {
        var bytes = await ReadArchiveBytesAsync(atlas.ImageEntryPath)
            ?? throw new InvalidDataException($"The bundle has no entry “{atlas.ImageEntryPath}”.");
        var bitmap = await Task.Run(() => LdrImageDecoder.Decode(bytes));

        ReplaceViewerBitmap(bitmap);

        AtlasRegions.Clear();
        foreach (var region in atlas.Regions.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            AtlasRegions.Add(new AtlasRegionCellViewModel(region, SelectRegion));
        }

        SetViewerHeader(atlas.Name,
            $"{_selectedBundle?.DisplayName} · Spritesheet · {FormatHelper.FormatCount(atlas.Regions.Count, "sprite")}");
        PopulateFacts(
            new AssetFact("Sheet image", atlas.ImageEntryPath),
            new AssetFact("Atlas XML", atlas.XmlEntryPath),
            new AssetFact("Sheet size", $"{bitmap.Width} × {bitmap.Height} px"),
            new AssetFact("Sprites", $"{atlas.Regions.Count:N0}"));

        SetViewerMode(ViewerMode.Image, "select a sprite to spotlight it · scroll to zoom");
    }

    private async Task OpenModelAsync(ModelAsset model)
    {
        var variant = model.GetVariant("glb") ?? model.GetVariant("gltf");
        if (variant == null)
        {
            SetViewerHeader(model.Name, $"{_selectedBundle?.DisplayName} · 3D model");
            PopulateFacts(new AssetFact("Formats", model.FormatList));
            NoPreviewText = $"This model ships only as {model.FormatList} — the 3D preview needs a GLB/glTF file.";
            SetViewerMode(ViewerMode.None, string.Empty);
            return;
        }

        var bytes = await ReadArchiveBytesAsync(variant.EntryPath)
            ?? throw new InvalidDataException($"The bundle has no entry “{variant.EntryPath}”.");

        //Parse the GLB off the UI thread; the GPU upload happens lazily at first paint.
        //Kenney GLBs reference their colormap texture beside themselves rather than embedding
        //it, so external references resolve back into the bundle archive.
        var archive = _archive;
        var animated = await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return new GltfModelLoader().LoadAnimated(stream,
                name => archive?.ReadDependencyBytes(variant.EntryPath, name));
        });
        var loaded = animated.Model;

        _animatedModel = animated;
        _currentModel = loaded;
        NotifyPropertyChanged(nameof(CurrentModel));

        SetViewerHeader(model.Name, $"{_selectedBundle?.DisplayName} · 3D model · {model.FormatList}");
        var facts = new List<AssetFact>
        {
            new("Formats", string.Join(", ", model.Variants
                .Select(v => $"{v.Extension.ToUpperInvariant()} ({FormatHelper.FormatBytes(v.SizeBytes)})"))),
            new("Triangles", $"{loaded.TriangleCount:N0}"),
            new("Vertices", $"{loaded.Primitives.Sum(p => p.VertexCount):N0}"),
            new("Materials", $"{loaded.Materials.Count:N0}"),
        };
        if (animated.HasAnimations)
        {
            facts.Add(new AssetFact("Animations", $"{animated.AnimationNames.Count:N0}"));
        }
        if (model.PreviewEntryPath != null) { facts.Add(new AssetFact("Kit preview", model.PreviewEntryPath)); }
        PopulateFacts(facts.ToArray());

        SetViewerMode(ViewerMode.Model, animated.HasAnimations
            ? "drag to rotate · scroll to zoom · pick an animation below"
            : "drag to rotate · scroll to zoom");

        //Start on "idle" when the model has one — the most character-like default
        if (animated.HasAnimations)
        {
            AnimationNames = animated.AnimationNames.ToList();
            NotifyPropertyChanged(nameof(AnimationBarVisibility));
            IsAnimationPlaying = true;
            SelectedAnimation = AnimationNames.FirstOrDefault(n =>
                n.Equals("idle", StringComparison.OrdinalIgnoreCase)) ?? AnimationNames[0];
        }
    }

    private async Task OpenAudioAsync(AssetEntry entry)
    {
        var bytes = await ReadArchiveBytesAsync(entry.EntryPath)
            ?? throw new InvalidDataException($"The bundle has no entry “{entry.EntryPath}”.");

        //Kenney audio is Ogg Vorbis, which the AudioPlayer add-in decodes itself (as it does
        //WAV, MP3 and FLAC) — the bytes go straight to the player, whatever the format.
        var audioStream = new MemoryStream(bytes, writable: false);

        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("File", entry.FileName),
            new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));

        IsAudioLooping = false;
        SetAudioLooping?.Invoke(false);
        _audioPlaybackEnded = false;
        LoadAudioSource?.Invoke(audioStream);
        HasAudioClip = LoadAudioSource != null;
        SetViewerMode(ViewerMode.Audio,
            LoadAudioSource == null ? "audio playback is not available on this head" : string.Empty);
    }

    private async Task OpenVectorAsync(AssetEntry entry)
    {
        var bytes = await ReadArchiveBytesAsync(entry.EntryPath)
            ?? throw new InvalidDataException($"The bundle has no entry “{entry.EntryPath}”.");
        var bitmap = await Task.Run(() => SvgImageDecoder.Render(bytes, maxDimension: 1600));

        ReplaceViewerBitmap(bitmap);
        AtlasRegions.Clear();

        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("File", entry.FileName),
            new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
            new AssetFact("Rendered at", $"{bitmap.Width} × {bitmap.Height} px (vector)"),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));

        SetViewerMode(ViewerMode.Image, "vector art (rasterized) · scroll to zoom");
    }

    private async Task OpenFontAsync(AssetEntry entry)
    {
        var bytes = await ReadArchiveBytesAsync(entry.EntryPath)
            ?? throw new InvalidDataException($"The bundle has no entry “{entry.EntryPath}”.");
        var bitmap = await Task.Run(() => FontSpecimenRenderer.Render(bytes));

        ReplaceViewerBitmap(bitmap);
        AtlasRegions.Clear();

        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("File", entry.FileName),
            new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));

        SetViewerMode(ViewerMode.Image, "font specimen · scroll to zoom");
    }

    private async Task OpenTiledAsync(AssetEntry entry)
    {
        var archive = _archive;
        var text = archive == null ? null : await Task.Run(() => archive.ReadEntryText(entry.EntryPath));

        //Tilesets (.tsx) and unparseable maps show as XML text instead
        if (entry.Extension != "tmx" || !TiledMapParser.TryParseMap(text, out var map))
        {
            ViewerText = string.IsNullOrWhiteSpace(text) ? "(This file is empty.)" : text.Trim();
            SetViewerHeader(entry.Name, EntrySubtitle(entry));
            PopulateFacts(
                new AssetFact("File", entry.FileName),
                new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
                new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));
            SetViewerMode(ViewerMode.Text, string.Empty);
            return;
        }

        //Resolve every tileset (external .tsx or inline) and its image out of the archive,
        //then composite the layers — all off the UI thread
        var bitmap = await Task.Run(() =>
        {
            var resolved = new List<(int FirstGid, TiledTilesetInfo Info, SKBitmap Image)>();
            try
            {
                foreach (var tilesetRef in map.Tilesets)
                {
                    var info = tilesetRef.Inline;
                    var basePath = entry.EntryPath;
                    if (tilesetRef.Source != null)
                    {
                        basePath = archive?.ResolveDependencyPath(entry.EntryPath, tilesetRef.Source);
                        if (basePath == null) { continue; }
                        TiledMapParser.TryParseTileset(archive.ReadEntryText(basePath), out info);
                    }
                    if (info == null) { continue; }

                    var imagePath = archive?.ResolveDependencyPath(basePath, info.ImagePath);
                    var imageBytes = imagePath == null ? null : archive.ReadEntryBytes(imagePath);
                    if (imageBytes == null) { continue; }

                    resolved.Add((tilesetRef.FirstGid, info, LdrImageDecoder.Decode(imageBytes)));
                }

                if (resolved.Count == 0)
                {
                    throw new InvalidDataException("None of the map's tilesets could be resolved from the bundle.");
                }

                return TiledMapRenderer.Render(map, resolved);
            }
            finally
            {
                foreach (var (_, _, image) in resolved) { image.Dispose(); }
            }
        });

        ReplaceViewerBitmap(bitmap);
        AtlasRegions.Clear();

        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("Map size", $"{map.Width} × {map.Height} tiles ({bitmap.Width} × {bitmap.Height} px)"),
            new AssetFact("Tile size", $"{map.TileWidth} × {map.TileHeight} px"),
            new AssetFact("Layers", $"{map.Layers.Count:N0}"),
            new AssetFact("Tilesets", $"{map.Tilesets.Count:N0}"),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));

        SetViewerMode(ViewerMode.Image, "Tiled map (composited) · scroll to zoom");
    }

    private async Task OpenDocumentAsync(AssetEntry entry)
    {
        var archive = _archive;
        var text = archive == null
            ? null
            : await Task.Run(() => archive.ReadEntryText(entry.EntryPath));

        ViewerText = string.IsNullOrWhiteSpace(text) ? "(This file is empty.)" : text.Trim();

        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("File", entry.FileName),
            new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));

        SetViewerMode(ViewerMode.Text, string.Empty);
    }

    private void OpenInfoOnly(AssetEntry entry)
    {
        SetViewerHeader(entry.Name, EntrySubtitle(entry));
        PopulateFacts(
            new AssetFact("File", entry.FileName),
            new AssetFact("Folder", entry.Category.Length == 0 ? "(bundle root)" : entry.Category),
            new AssetFact("Size", FormatHelper.FormatBytes(entry.SizeBytes)));
        NoPreviewText = $"No preview is available for .{entry.Extension} files — the file is listed for reference.";
        SetViewerMode(ViewerMode.None, string.Empty);
    }

    private void CloseViewer()
    {
        IsViewerActive = false;
        StopAudioPlayback();
        HasAudioClip = false;
        ResetAnimationState();
        SetViewerMode(ViewerMode.None, string.Empty, activateViewer: false);

        ImagePainter.Bitmap = null;
        ImagePainter.HighlightRegion = null;
        var bitmap = _viewerBitmap;
        _viewerBitmap = null;
        bitmap?.Dispose();

        _currentModel = null;
        NotifyPropertyChanged(nameof(CurrentModel));
        AtlasRegions.Clear();
    }

    private string EntrySubtitle(AssetEntry entry) =>
        $"{_selectedBundle?.DisplayName} · {(entry.Category.Length == 0 ? "bundle root" : entry.Category)} · {FormatHelper.FormatBytes(entry.SizeBytes)}";

    private void SetViewerHeader(string title, string subtitle)
    {
        ViewerTitle = title;
        ViewerSubtitle = subtitle;
    }

    private void PopulateFacts(params AssetFact[] facts)
    {
        ViewerFacts.Clear();
        foreach (var fact in facts) { ViewerFacts.Add(fact); }
        ViewerFacts.Add(new AssetFact("Bundle", _selectedBundle?.DisplayName ?? string.Empty));
        ViewerFacts.Add(new AssetFact("License", "CC0 (public domain) — kenney.nl"));
    }

    private void SetViewerMode(ViewerMode mode, string hint, bool activateViewer = true)
    {
        _viewerMode = mode;
        ViewerHint = hint;
        if (mode == ViewerMode.Image)
        {
            ImagePainter.ZoomFactor = 1f;
            ImagePainter.HighlightRegion = null;
            NotifyPropertyChanged(nameof(ZoomText));
        }

        NotifyPropertyChanged(nameof(ImageViewerVisibility));
        NotifyPropertyChanged(nameof(ModelViewerVisibility));
        NotifyPropertyChanged(nameof(TextViewerVisibility));
        NotifyPropertyChanged(nameof(NoPreviewVisibility));
        NotifyPropertyChanged(nameof(AudioViewerVisibility));
        NotifyPropertyChanged(nameof(ZoomBarVisibility));
        NotifyPropertyChanged(nameof(RegionListVisibility));
        NotifyPropertyChanged(nameof(AnimationBarVisibility));

        if (activateViewer)
        {
            IsViewerActive = true;
            InvalidateImageCanvas?.Invoke();
        }
    }

    private void ReplaceViewerBitmap(SKBitmap bitmap)
    {
        var previous = _viewerBitmap;
        _viewerBitmap = bitmap;
        ImagePainter.Bitmap = bitmap;
        previous?.Dispose();
    }

    private void SelectRegion(AtlasRegionCellViewModel row)
    {
        //Re-selecting the spotlighted region clears the spotlight
        var selecting = !row.IsSelected;
        foreach (var region in AtlasRegions)
        {
            region.IsSelected = selecting && region == row;
        }

        ImagePainter.HighlightRegion = selecting
            ? new SKRectI(row.Region.X, row.Region.Y,
                row.Region.X + row.Region.Width, row.Region.Y + row.Region.Height)
            : null;
        InvalidateImageCanvas?.Invoke();
    }

    /// <summary>
    /// Shows a dialog explaining why the 3D preview cannot render. Called from the view when
    /// the Viewer View is active and the preview's GLCanvasElement reports that its OpenGL
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

    #endregion

    #region | Image zoom |

    /// <summary>The zoom caption, e.g. <c>125%</c>.</summary>
    public string ZoomText => $"{ImagePainter.ZoomFactor * 100:0}%";

    /// <summary>Zooms the 2D viewer in one step.</summary>
    public SimpleCommand ZoomInCommand => field ??= new SimpleCommand(CanZoom, () => AdjustZoom(1.25f));

    /// <summary>Zooms the 2D viewer out one step.</summary>
    public SimpleCommand ZoomOutCommand => field ??= new SimpleCommand(CanZoom, () => AdjustZoom(0.8f));

    /// <summary>Resets the 2D viewer's zoom to fit.</summary>
    public SimpleCommand ZoomResetCommand => field ??= new SimpleCommand(CanZoom, () =>
    {
        ImagePainter.ZoomFactor = 1f;
        NotifyPropertyChanged(nameof(ZoomText));
        InvalidateImageCanvas?.Invoke();
    });

    //Only the 2D viewer has a zoom; the toolbar carrying these is shown with the same test,
    //and SetViewerMode's notification for it is what refreshes them.
    private bool CanZoom() => _viewerMode == ViewerMode.Image;

    /// <summary>
    /// Applies one wheel notch of zoom from the page's pointer-wheel handler.
    /// </summary>
    /// <param name="wheelDelta">The wheel delta (positive zooms in).</param>
    public void AdjustZoomFromWheel(int wheelDelta) => AdjustZoom(wheelDelta > 0 ? 1.25f : 0.8f);

    private void AdjustZoom(float factor)
    {
        ImagePainter.ZoomFactor = Math.Clamp(ImagePainter.ZoomFactor * factor, 0.25f, 16f);
        NotifyPropertyChanged(nameof(ZoomText));
        InvalidateImageCanvas?.Invoke();
    }

    #endregion

    #region | IDisposable implementation |

    /// <inheritdoc />
    public override void Dispose()
    {
        //Every bridge delegate captures the page, and would hold it alive through the view model
        InvalidateImageCanvas = null;
        ScrollCatalogToTop = null;
        ViewerOpened = null;
        LoadAudioSource = null;
        PlayAudio = null;
        PauseAudio = null;
        StopAudio = null;
        SetAudioLooping = null;
        IsAudioPlaying = null;
        AudioPosition = null;
        AudioDuration = null;
        SeekAudio = null;

        //Both of these are released on every normal transition (bundle switch, catalog reload,
        //viewer close); this is the one that closes the last of each at shutdown.
        CloseViewer();
        DisposeArchive();

        var debounce = _searchDebounce;
        _searchDebounce = null;
        debounce?.Dispose();

        base.Dispose();
    }

    #endregion
}
