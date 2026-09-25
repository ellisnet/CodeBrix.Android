using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NotionDocumentCreator.CreateDocument;
using NotionDocumentCreator.CreateDocument.Models;
using NotionDocumentCreator.CreateDocument.Services;
using NotionDocumentCreator.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NotionDocumentCreator.ViewModels;

/// <summary>
/// Lets the hosting page give the view model a native "Save PDF as…" file dialog. Each head
/// wires this up with the file dialog appropriate to its UI stack (the CodeBrix.Platform
/// <c>FileSavePicker</c> on the Skia heads).
/// </summary>
public interface IFileSaveBridge
{
    /// <summary>
    /// Shows a "save PDF" dialog seeded with suggestedFileName and returns the
    /// full path the user chose, or <c>null</c> if they cancelled. The head leaves this null when
    /// it has no file dialog, in which case the user types the path directly into the box.
    /// Signature: <c>Func&lt;suggestedFileName, Task&lt;chosenPathOrNull&gt;&gt;</c>.
    /// </summary>
    Func<string, Task<string>> PickSavePdfPathAsync { get; set; }
}

[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IFileSaveBridge
{
    private INotionDocumentService _documentSvc;

    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        _documentSvc = GetService<INotionDocumentService>();

        StatusText = "Paste your Notion integration token and a page or database ID, then click Connect.";
    }

    #region | Bindable properties |

    [AffectsCommands(nameof(ConnectCommand))]
    public string IntegrationToken
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    [AffectsCommands(nameof(ConnectCommand))]
    public string PageOrDatabaseId
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    public string ConnectionStatus
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = "Not connected";

    [AffectsCommands(nameof(CreateCommand), nameof(LoadWholeTreeCommand))]
    [AffectsProperties(nameof(TreePlaceholderVisibility), nameof(TreeVisibility))]
    public bool IsConnected
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ObservableCollection<NotionPageNodeViewModel> RootNodes { get; } = new();

    [AffectsProperties(nameof(PreviewContentVisibility), nameof(PreviewPlaceholderVisibility))]
    public NotionPageNodeViewModel SelectedNode
    {
        get;
        private set => SetProperty(ref field, value);
    }

    //Preview pane content (flattened to plain values; the pane never scrolls)
    public string PreviewTitle
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    public string PreviewMeta
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    public string PreviewSnippets
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    [AffectsProperties(nameof(PreviewCoverVisibility))]
    public ImageSource PreviewCoverSource
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public Visibility PreviewContentVisibility => GetVisibility(SelectedNode is not null);
    public Visibility PreviewPlaceholderVisibility => GetVisibility(SelectedNode is null);
    public Visibility PreviewCoverVisibility => GetVisibility(PreviewCoverSource is not null);
    public Visibility TreePlaceholderVisibility => GetVisibility(!IsConnected);
    public Visibility TreeVisibility => GetVisibility(IsConnected);

    [AffectsCommands(nameof(CreateCommand))]
    public string OutputFilePath
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>Set by the hosting head (see <see cref="IFileSaveBridge"/>); null on heads with no file dialog.</summary>
    public Func<string, Task<string>> PickSavePdfPathAsync { get; set; }

    /// <summary>The trim sizes offered by the page-size picker, in the library's display order.</summary>
    public IReadOnlyList<PageSizeInfo> PageSizes => PageSizeInfo.All;

    /// <summary>
    /// The chosen trim, two-way bound to the picker's SelectedItem. Binding the record itself
    /// (the ComboBox shows its DisplayName) keeps the display strings in the library that owns
    /// them and needs no mapping back from text.
    /// </summary>
    public PageSizeInfo SelectedPageSize
    {
        get;
        set => SetProperty(ref field, value ?? PageSizeInfo.All[0]);
    } = PageSizeInfo.All[0];

    [AffectsCommands(nameof(ConnectCommand), nameof(LoadWholeTreeCommand),
        nameof(SelectOutputFileCommand), nameof(CreateCommand))]
    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    public int ProgressValue
    {
        get;
        set => SetProperty(ref field, value);
    }

    [AffectsCommands(nameof(CreateCommand))]
    [AffectsProperties(nameof(CheckedCountText))]
    public int CheckedCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string CheckedCountText => CheckedCount switch
    {
        0 => "No pages selected yet",
        1 => "1 page selected",
        _ => $"{CheckedCount} pages selected"
    };

    #endregion

    #region | Commands and their implementations |

    #region ConnectCommand

    private SimpleCommand _connectCommand;
    public SimpleCommand ConnectCommand =>
        (_connectCommand ??= new SimpleCommand(CanConnect, DoConnect));

    private bool CanConnect() =>
        (!IsBusy)
        && (!string.IsNullOrWhiteSpace(IntegrationToken))
        && (!string.IsNullOrWhiteSpace(PageOrDatabaseId));

    private async Task DoConnect()
    {
        if (!CanConnect()) { return; }

        try
        {
            IsBusy = true;
            StatusText = "Connecting to Notion…";
            var botName = await _documentSvc.ConnectAsync(IntegrationToken.Trim());

            StatusText = "Loading the root page…";
            var roots = await _documentSvc.LoadRootsAsync(PageOrDatabaseId.Trim());

            RootNodes.Clear();
            SelectedNode = null;
            ResetPreview();
            foreach (var root in roots)
            {
                RootNodes.Add(new NotionPageNodeViewModel(root, this));
            }

            IsConnected = true;
            ConnectionStatus = $"Connected as {botName}";
            OnNodeCheckedChanged();
            StatusText = "Check the pages to include — the first checked page becomes the cover.";

            if (RootNodes.Count == 1)
            {
                RootNodes[0].IsExpanded = true; //Auto-expand the root; children load lazily
            }
        }
        catch (Exception e)
        {
            IsConnected = false;
            ConnectionStatus = "Not connected";
            StatusText = "Connection failed.";
            await ShowError($"Could not connect: {e.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region LoadWholeTreeCommand

    private SimpleCommand _loadWholeTreeCommand;
    public SimpleCommand LoadWholeTreeCommand =>
        (_loadWholeTreeCommand ??= new SimpleCommand(CanLoadWholeTree, DoLoadWholeTree));

    private bool CanLoadWholeTree() => IsConnected && !IsBusy;

    private async Task DoLoadWholeTree()
    {
        if (!CanLoadWholeTree()) { return; }

        try
        {
            IsBusy = true;
            StatusText = "Loading the whole tree…";
            foreach (var root in RootNodes.ToList())
            {
                await LoadSubtreeAsync(root);
            }
            foreach (var node in Flatten().ToList())
            {
                if (!node.IsPlaceholder && node.Children.Count > 0) { node.IsExpanded = true; }
            }
            StatusText = $"Loaded {Flatten().Count(n => !n.IsPlaceholder)} pages.";
        }
        catch (Exception e)
        {
            StatusText = "Loading the tree failed.";
            await ShowError($"Could not load the whole tree: {e.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSubtreeAsync(NotionPageNodeViewModel node)
    {
        await node.EnsureChildrenLoadedAsync();
        foreach (var child in node.Children.ToList())
        {
            await LoadSubtreeAsync(child);
        }
    }

    #endregion

    #region SelectOutputFileCommand

    private SimpleCommand _selectOutputFileCommand;
    public SimpleCommand SelectOutputFileCommand =>
        (_selectOutputFileCommand ??= new SimpleCommand(CanSelectOutputFile, DoSelectOutputFile));

    private bool CanSelectOutputFile() => !IsBusy;

    private async Task DoSelectOutputFile()
    {
        if (!CanSelectOutputFile()) { return; }

        if (PickSavePdfPathAsync == null)
        {
            //No native file dialog on this head — the user types the destination
            //  path directly into the box instead.
            await ShowInfo(
                "This head has no file dialog. Type the full path (including the .pdf file name) " +
                "for the PDF into the “Save PDF to” box.");
            return;
        }

        try
        {
            var chosenPath = await PickSavePdfPathAsync(GetSuggestedFileName());
            if (!string.IsNullOrWhiteSpace(chosenPath))
            {
                var destination = chosenPath.Trim();

                //Deciding that a brand-new, still-empty file the picker created is a destination
                //  rather than a document is this application's policy, so it lives here and not
                //  in the page: the create-time "replace existing file?" prompt should fire only
                //  for a file that really has content in it.
                FileDialogHelper.RemoveEmptyPlaceholder(destination);

                OutputFilePath = destination;
                StatusText = $"Will save to: {OutputFilePath}";
            }
        }
        catch (NotSupportedException)
        {
            //Some heads register no picker — there is no window to host a dialog
            await ShowInfo(
                "File dialogs are not supported on this head. Type the full path (including the " +
                ".pdf file name) for the PDF into the “Save PDF to” box.");
        }
        catch (Exception e)
        {
            await ShowError($"Could not open the file dialog: {e.Message}");
        }
    }

    #endregion

    #region CreateCommand

    private SimpleCommand _createCommand;
    public SimpleCommand CreateCommand =>
        (_createCommand ??= new SimpleCommand(CanCreate, DoCreate));

    private bool CanCreate() =>
        (!IsBusy)
        && IsConnected
        && (!string.IsNullOrWhiteSpace(OutputFilePath))
        && CheckedCount > 0;

    private async Task DoCreate()
    {
        if (!CanCreate()) { return; }

        var outputPath = OutputFilePath.Trim();

        //Confirm before clobbering an existing file
        if (File.Exists(outputPath))
        {
            var replace = await ConfirmDialog(
                $"A file already exists at:\n{outputPath}\n\nDo you want to replace it?",
                "Replace existing file?");
            if (!replace)
            {
                StatusText = "Creation cancelled — the existing file was kept.";
                return;
            }
        }

        try
        {
            IsBusy = true;
            ProgressValue = 0;

            //Checked pages, in the order shown in the tree (depth-first, top to bottom)
            var pageIds = SelectionFlattening
                .FlattenDepthFirst(RootNodes, n => n.Children)
                .Where(n => !n.IsPlaceholder && n.IsChecked)
                .Select(n => n.Id)
                .ToList();

            var request = new CreateRequest
            {
                PageIds = pageIds,
                OutputFilePath = outputPath,
                PageSize = SelectedPageSize.Option
            };

            var progress = new Progress<CreateProgress>(p => InvokeOnMainThread(() =>
            {
                StatusText = p.Message;
                ProgressValue = p.PercentComplete;
            }));

            var result = await _documentSvc.CreateDocumentAsync(request, progress);

            StatusText = $"Saved: {result.OutputFilePath}";
            await ShowInfo(BuildResultMessage(result));
        }
        catch (Exception e)
        {
            StatusText = "Creation failed.";
            await ShowError($"Error while creating the document: {e.Message}");
        }
        finally
        {
            ProgressValue = 0;
            IsBusy = false;
        }
    }

    private static string BuildResultMessage(CreatedDocument result)
    {
        var message =
            $"Created “{result.Title}”\n\n" +
            $"{result.ChapterCount} chapters · {result.PageCount} pages · {result.ImageCount} images · " +
            $"{result.Elapsed.TotalSeconds:F0} seconds\n\n" +
            $"Saved to: {result.OutputFilePath}";

        if (result.Warnings.Count > 0)
        {
            const int shown = 6;
            message += "\n\nNotes:\n• " + string.Join("\n• ", result.Warnings.Take(shown));
            if (result.Warnings.Count > shown)
            {
                message += $"\n…and {result.Warnings.Count - shown} more.";
            }
        }
        return message;
    }

    #endregion

    #endregion

    #region | Tree and preview plumbing |

    /// <summary>Loads a node's children from Notion (called on first expand).</summary>
    internal async Task LoadChildrenForNodeAsync(NotionPageNodeViewModel node)
    {
        try
        {
            var children = await _documentSvc.LoadChildrenAsync(node.Id);
            InvokeOnMainThread(() =>
                node.SetChildren(children.Select(c => new NotionPageNodeViewModel(c, this))));
        }
        catch (Exception e)
        {
            InvokeOnMainThread(() => StatusText = $"Could not load child pages: {e.Message}");
        }
    }

    /// <summary>Recomputes the checked count (drives the Create! button).</summary>
    internal void OnNodeCheckedChanged()
    {
        CheckedCount = Flatten().Count(n => !n.IsPlaceholder && n.IsChecked);
    }

    /// <summary>Shows the preview pane for a tapped row.</summary>
    internal void ShowPreview(NotionPageNodeViewModel node)
    {
        if (node is null || node.IsPlaceholder) { return; }
        SelectedNode = node;

        //The selection has to take effect now, and the load cannot be awaited from here, so it
        //  is started through a helper that observes it instead of a bare discard.
        BackgroundWork.StartAndObserve(() => LoadPreviewForNodeAsync(node),
            e => InvokeOnMainThread(() => StatusText = $"Preview failed: {e.Message}"));
    }

    private async Task LoadPreviewForNodeAsync(NotionPageNodeViewModel node)
    {
        try
        {
            var preview = await _documentSvc.LoadPreviewAsync(node.Id);
            InvokeOnMainThread(() =>
            {
                if (SelectedNode != node) { return; } //A newer selection superseded this preview

                PreviewTitle = preview.Title;
                PreviewMeta = string.Join("  ·  ",
                    preview.ChildPageCount == 1 ? "1 child page" : $"{preview.ChildPageCount} child pages",
                    $"edited {preview.LastEditedTime.ToLocalTime():yyyy-MM-dd}");
                PreviewSnippets = string.Join("\n\n", preview.TextSnippets);

                PreviewCoverSource = null;
                var imageUrl = preview.CoverUrl.Length > 0 ? preview.CoverUrl : preview.IconUrl;
                if (imageUrl.Length > 0)
                {
                    try { PreviewCoverSource = new BitmapImage(new Uri(imageUrl)); }
                    catch (Exception) { } //A malformed URL just leaves the pane imageless
                }
            });
        }
        catch (Exception e)
        {
            InvokeOnMainThread(() => StatusText = $"Preview failed: {e.Message}");
        }
    }

    private void ResetPreview()
    {
        PreviewTitle = "";
        PreviewMeta = "";
        PreviewSnippets = "";
        PreviewCoverSource = null;
    }

    private IEnumerable<NotionPageNodeViewModel> Flatten() =>
        SelectionFlattening.FlattenDepthFirst(RootNodes, n => n.Children);

    /// <summary>A sensible default PDF file name: the first checked page's title.</summary>
    private string GetSuggestedFileName()
    {
        var name = Flatten().FirstOrDefault(n => !n.IsPlaceholder && n.IsChecked)?.Title;
        if (string.IsNullOrWhiteSpace(name)) { name = "NotionBook"; }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return (cleaned.Length == 0 ? "NotionBook" : cleaned) + ".pdf";
    }

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        _documentSvc = null;
        _connectCommand?.Dispose();
        _connectCommand = null;
        _loadWholeTreeCommand?.Dispose();
        _loadWholeTreeCommand = null;
        _selectOutputFileCommand?.Dispose();
        _selectOutputFileCommand = null;
        _createCommand?.Dispose();
        _createCommand = null;
        PickSavePdfPathAsync = null;
        base.Dispose();
    }

    #endregion
}
