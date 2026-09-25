using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pinta.Brix.Bridges;
using Pinta.Brix.Controls;
using Pinta.Brix.Engine;
using Windows.Storage;

namespace Pinta.Brix.Views;

public sealed partial class MainPage : Page
{
    private readonly Dictionary<Document, TabViewItem> documentTabs = new();
    private ToolBarRenderer toolOptionsRenderer;
    private bool updatingLayerSelection;

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            //Hand over the save-prompt loop: it needs this page's XamlRoot and
            //its file dialogs, so only a page can run it. The view model
            //forwards it to the service the window close resolves.
            if (DataContext is IShellCloseBridge shellClose)
            {
                shellClose.ConfirmCloseApplicationAsync = ConfirmCloseApplicationAsync;
            }
        };

        this.InitializeComponent(); //Leave this line last

        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        //Chrome wiring: dialogs need a XamlRoot, so this happens on Loaded
        PintaCore.Chrome.InitializeErrorDialogHandler(ShowErrorDialogAsync);
        PintaCore.Chrome.InitializeMessageDialog(ShowMessageDialogAsync);
        PintaCore.Chrome.InitializeProgessDialog(new ContentProgressDialog(() => XamlRoot));
        //Custom effect dialogs route by effect type; everything else gets the
        //reflection-generated dialog. Upstream's effects each opened their own
        //Gtk dialog directly; here the Effects library stays UI-free, so the
        //routing lives at this seam instead.
        PintaCore.Chrome.InitializeSimpleEffectDialog(
            (effect, _) => effect switch
            {
                Effects.AlignObjectEffect align => Dialogs.AlignmentDialog.ShowAsync(align, XamlRoot),
                Effects.CurvesEffect curves => Dialogs.CurvesDialog.ShowAsync(curves, XamlRoot),
                Effects.LevelsEffect levels => Dialogs.LevelsDialog.ShowAsync(levels, XamlRoot),
                Effects.PosterizeEffect posterize => Dialogs.PosterizeDialog.ShowAsync(posterize, XamlRoot),
                _ => EffectOptionsDialog.ShowAsync(effect, XamlRoot),
            });
        PintaCore.Workspace.SaveDocumentHandler = SaveDocumentAsync;

        //The clipboard is a UI-layer service; the engine and the tools reach it
        //through PintaCore.Clipboard.
        PintaCore.InitializeClipboard(new PlatformClipboardService());

        //Tool options toolbar renders the engine's descriptor model
        toolOptionsRenderer = new ToolBarRenderer(PintaCore.Chrome.ToolToolBar, ToolOptionsPanel);

        //Menus, toolbar and command handlers all come from the action model
        WireActions();
        WirePaletteActions();
        BuildMenus();
        BuildMainToolbar();
        BuildPadToolbars();
        BuildPaletteWidget();

        //Workspace events drive the document tabs and pads
        PintaCore.Workspace.DocumentActivated += (_, args) => AddDocumentTab(args.Document);
        PintaCore.Workspace.DocumentClosed += (_, args) => RemoveDocumentTab(args.Document);
        PintaCore.Workspace.ActiveDocumentChanged += (_, _) => OnActiveDocumentChanged();
        PintaCore.Workspace.LayerAdded += (_, _) => OnDocumentStateChanged();
        PintaCore.Workspace.LayerRemoved += (_, _) => OnDocumentStateChanged();
        PintaCore.Workspace.SelectedLayerChanged += (_, _) => OnDocumentStateChanged();

        //The status bar's three readouts and the zoom control follow the
        //view model; see MainViewModel.

        //Adjustments/Effects menus follow the engine's registries
        PintaCore.Effects.AdjustmentsChanged += (_, _) => RebuildAdjustmentsMenu();
        PintaCore.Effects.EffectsChanged += (_, _) => RebuildEffectsMenu();
        RebuildAdjustmentsMenu();
        RebuildEffectsMenu();

        //Toolbox follows the tool manager
        PintaCore.Tools.ToolAdded += (_, _) => RefreshToolbox();
        PintaCore.Tools.ToolRemoved += (_, _) => RefreshToolbox();
        PintaCore.Tools.ToolActivated += (_, _) => RefreshToolbox();
        RefreshToolbox();

        //The tool box re-flows into more or fewer columns as the window height
        //changes, so it has to be rebuilt on resize.
        ToolboxScroll.SizeChanged += (_, args) =>
        {
            if (Math.Abs(args.NewSize.Height - args.PreviousSize.Height) > 1)
                RefreshToolbox();
        };

        UpdateActionSensitivity();

        //Pad splitters - upstream's Gtk.Paned equivalents (the platform has
        //no GridSplitter). Positions persist through settings.sqlite.
        BuildSplitters();

        //Match upstream: the application opens with a blank document already
        //created (upstream Main.cs makes an 800x600 white document when no
        //files are given on the command line). Goes through the same path as
        //File > New so the two can never drift apart. If command-line file
        //opening is ever ported, this must be skipped for that path, exactly
        //as upstream skips it.
        NewImage();
    }

    private void BuildSplitters()
    {
        PadsColumn.Width = Math.Clamp(
            PintaCore.Settings.GetSetting("pads-width", 230), 200, 800);

        int savedLayersHeight = PintaCore.Settings.GetSetting("layers-pad-height", 0);
        if (savedLayersHeight > 0)
        {
            PadsColumn.RowDefinitions[0].Height = new GridLength(Math.Max(120, savedLayersHeight));
        }

        ThumbSplitter columnSplitter = new(Orientation.Vertical);
        Grid.SetColumn(columnSplitter, 2);
        ContentGrid.Children.Add(columnSplitter);
        columnSplitter.DragDelta += (_, delta) =>
        {
            double width = Math.Clamp(PadsColumn.ActualWidth - delta, 200, 800);
            PadsColumn.Width = width;
            PintaCore.Settings.PutSetting("pads-width", (int)width);
        };

        ThumbSplitter padSplitter = new(Orientation.Horizontal);
        Grid.SetRow(padSplitter, 1);
        PadsColumn.Children.Add(padSplitter);
        padSplitter.DragDelta += (_, delta) =>
        {
            double maxHeight = PadsColumn.ActualHeight - padSplitter.ActualHeight - 120;
            double height = Math.Clamp(
                PadsColumn.RowDefinitions[0].ActualHeight + delta, 120, Math.Max(120, maxHeight));
            PadsColumn.RowDefinitions[0].Height = new GridLength(height);
            PintaCore.Settings.PutSetting("layers-pad-height", (int)height);
        };
    }

    // ---- Documents and tabs ------------------------------------------------

    private void AddDocumentTab(Document document)
    {
        PintaCanvasView view = new() { Document = document };
        TabViewItem tab = new()
        {
            Header = document.DisplayName,
            Content = view,
        };
        documentTabs[document] = tab;
        DocumentTabs.TabItems.Add(tab);
        DocumentTabs.SelectedItem = tab;

        document.Renamed += (_, _) => { tab.Header = document.DisplayName; RebuildWindowMenu(); };
        document.IsDirtyChanged += (_, _) =>
        {
            tab.Header = document.IsDirty ? $"{document.DisplayName}*" : document.DisplayName;
            RebuildWindowMenu();
        };

        //History changes drive Undo/Redo enablement and the history pad.
        document.History.HistoryItemAdded += (_, _) => OnDocumentStateChanged();
        document.History.ActionUndone += (_, _) => OnDocumentStateChanged();
        document.History.ActionRedone += (_, _) => OnDocumentStateChanged();
        document.SelectionChanged += (_, _) => UpdateActionSensitivity();

        RebuildWindowMenu();
        UpdateActionSensitivity();
    }

    private void RemoveDocumentTab(Document document)
    {
        if (!documentTabs.TryGetValue(document, out TabViewItem tab)) { return; }
        documentTabs.Remove(document);
        DocumentTabs.TabItems.Remove(tab);
        RebuildWindowMenu();
        UpdateActionSensitivity();
    }

    //An event handler, so it cannot return a task; the body is wrapped because
    //an exception escaping an async void handler has nowhere to go but the
    //process, and losing the application is worse than losing the close.
    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        try
        {
            Document document = documentTabs.FirstOrDefault(kv => kv.Value == args.Tab).Key;
            if (document is null) { return; }

            //Prompt before discarding unsaved work - the tab close is the most
            //likely way to lose a document.
            await CloseDocumentAsync(document);
        }
        catch (Exception ex)
        {
            await ReportHandlerFailure(ex);
        }
    }

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is not TabViewItem tab) { return; }
        Document document = documentTabs.FirstOrDefault(kv => kv.Value == tab).Key;
        if (document is null) { return; }
        int index = PintaCore.Workspace.OpenDocuments.IndexOf(document);
        if (index >= 0 && index != PintaCore.Workspace.ActiveDocumentIndex)
        {
            PintaCore.Workspace.SetActiveDocument(index);
        }
    }

    private void OnActiveDocumentChanged()
    {
        if (PintaCore.Workspace.HasOpenDocuments)
        {
            Document document = PintaCore.Workspace.ActiveDocument;
            if (documentTabs.TryGetValue(document, out TabViewItem tab))
            {
                DocumentTabs.SelectedItem = tab;
            }
        }
        OnDocumentStateChanged();
    }

    /// <summary>
    /// One place for "something about the document changed" - the pads and the
    /// command enablement both follow from it.
    /// </summary>
    private void OnDocumentStateChanged()
    {
        RefreshLayersPad();
        RefreshHistoryPad();
        UpdateActionSensitivity();
    }

    private async Task<bool> SaveDocumentAsync(Document document, bool saveAs)
    {
        string path = document.File;
        string fileType = document.FileType;

        if (path is null || saveAs)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary,
                SuggestedFileName = document.DisplayName,
            };
            //The registry owns which formats a save dialog offers and under
            //what name; the page just hands the list to the picker.
            foreach (FileDialogFilter filter in PintaCore.ImageFormats.GetExportFilters())
            {
                picker.FileTypeChoices.Add(filter.Name, [.. filter.Extensions]);
            }

            StorageFile file = await picker.PickSaveFileAsync();
            if (file is null) { return false; }
            path = file.Path;
            fileType = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        }

        var descriptor = PintaCore.ImageFormats.GetFormatByFile(path);
        if (descriptor is null || !descriptor.IsExportAvailable())
        {
            await PintaCore.Chrome.ShowErrorDialog(
                "Cannot save file", $"No exporter is available for '{path}'.", string.Empty);
            return false;
        }

        //Saving to a format that cannot hold layers flattens the image; upstream
        //asks first, because the layers are gone from the FILE either way.
        if (document.Layers.Count() > 1 && !descriptor.SupportsLayers)
        {
            ContentDialog flattenDialog = new()
            {
                Title = "Flatten Image?",
                Content = $"The {descriptor.FilterName} format does not support layers. "
                    + "The saved file will contain a flattened copy of the image.",
                PrimaryButtonText = "Flatten",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            if (await flattenDialog.ShowAsync() != ContentDialogResult.Primary) { return false; }
        }

        descriptor.Exporter.Export(document, path);
        document.File = path;
        document.FileType = fileType;
        document.Workspace.History.SetClean();
        PintaCore.Tools.DoAfterSave(document);
        RebuildWindowMenu();
        return true;
    }

    // ---- Toolbox -----------------------------------------------------------

    /// <summary>Upstream's icon size for the tool box (CSS -gtk-icon-size: 2rem).</summary>
    private const int ToolboxIconSize = 32;

    /// <summary>
    /// Upstream's FlowBox uses MinChildrenPerLine = 8, i.e. at least eight
    /// buttons per column before a second column is started.
    /// </summary>
    private const int ToolboxMinimumPerColumn = 8;

    private const double ToolboxButtonExtent = 46;

    private void RefreshToolbox()
    {
        ToolboxPanel.Children.Clear();

        List<BaseTool> tools = [.. PintaCore.Tools];
        if (tools.Count == 0) { return; }

        //Flow into columns: a tall window shows one column, a short one shows
        //two or three, exactly as upstream's vertical FlowBox does.
        double available = ToolboxScroll.ActualHeight > 0 ? ToolboxScroll.ActualHeight : ActualHeight;
        int perColumn = Math.Max(ToolboxMinimumPerColumn, (int)(available / ToolboxButtonExtent));

        StackPanel columnsPanel = new() { Orientation = Orientation.Horizontal };
        ToolboxPanel.Children.Add(columnsPanel);

        StackPanel column = null;

        for (int i = 0; i < tools.Count; i++)
        {
            if (i % perColumn == 0)
            {
                column = new StackPanel { Orientation = Orientation.Vertical };
                columnsPanel.Children.Add(column);
            }

            BaseTool tool = tools[i];

            ToggleButton button = new()
            {
                Content = new Image
                {
                    Width = ToolboxIconSize,
                    Height = ToolboxIconSize,
                    Source = IconImageSource.Create(tool.Icon, ToolboxIconSize),
                },
                IsChecked = PintaCore.Tools.CurrentTool == tool,
                Margin = new Thickness(1),
                Padding = new Thickness(4),
            };

            ToolTipService.SetToolTip(button, BuildToolTooltip(tool));
            BaseTool captured = tool;

            //Radio behaviour: clicking the active tool must not turn it off.
            button.Click += (_, _) =>
            {
                if (PintaCore.Tools.CurrentTool == captured)
                {
                    button.IsChecked = true;
                    return;
                }
                PintaCore.Tools.SetCurrentTool(captured);
            };

            column.Children.Add(button);
        }
    }

    /// <summary>
    /// Upstream's toolbox tooltip: name, shortcut key, then the status-bar hint.
    /// </summary>
    private static string BuildToolTooltip(BaseTool tool)
    {
        string tooltip = tool.Name;

        if (tool.ShortcutKey != default)
        {
            tooltip += $"\nShortcut key: {tool.ShortcutKey}";
        }

        if (!string.IsNullOrEmpty(tool.StatusBarText))
        {
            tooltip += $"\n\n{tool.StatusBarText}";
        }

        return tooltip;
    }

    // ---- Pads --------------------------------------------------------------

    private void RefreshLayersPad()
    {
        LayersList.Items.Clear();
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        var layers = PintaCore.Workspace.ActiveDocument.Layers;

        //Topmost layer first, matching the upstream layers pad
        for (int i = layers.Count() - 1; i >= 0; i--)
        {
            LayersList.Items.Add(LayerRowFactory.Create(layers[i]));
        }

        updatingLayerSelection = true;
        LayersList.SelectedIndex = layers.Count() - 1 - layers.CurrentUserLayerIndex;
        updatingLayerSelection = false;
    }

    private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingLayerSelection || !PintaCore.Workspace.HasOpenDocuments) { return; }
        var layers = PintaCore.Workspace.ActiveDocument.Layers;
        int index = layers.Count() - 1 - LayersList.SelectedIndex;
        if (index >= 0 && index < layers.Count())
        {
            layers.SetCurrentUserLayer(index);
        }
    }

    private void RefreshHistoryPad()
    {
        HistoryList.Items.Clear();
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        DocumentHistory history = PintaCore.Workspace.ActiveWorkspace.History;
        int pointer = history.Pointer;
        List<BaseHistoryItem> items = [.. history.Items];

        for (int i = 0; i < items.Count; i++)
        {
            HistoryList.Items.Add(HistoryRowFactory.Create(items[i], undone: i > pointer));
        }

        updatingHistorySelection = true;
        HistoryList.SelectedIndex = pointer;
        updatingHistorySelection = false;
    }

    private bool updatingHistorySelection;

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingHistorySelection || !PintaCore.Workspace.HasOpenDocuments) { return; }

        DocumentHistory history = PintaCore.Workspace.ActiveWorkspace.History;
        int target = HistoryList.SelectedIndex;

        if (target < 0 || target == history.Pointer) { return; }

        //Travel to the clicked point, one step at a time so every history item's
        //own Undo/Redo runs.
        while (history.Pointer > target && history.CanUndo) { history.Undo(); }
        while (history.Pointer < target && history.CanRedo) { history.Redo(); }
    }

    // ---- Dialog handlers ---------------------------------------------------

    private async Task<ErrorDialogResponse> ShowErrorDialogAsync(string message, string body, string details)
    {
        StackPanel panel = new() { Spacing = 8, MinWidth = 360 };
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrEmpty(details))
        {
            //Upstream puts the detail behind an expander with a "file a bug"
            //button; the expander is the part that matters for readability.
            Expander expander = new()
            {
                Header = "Details",
                Content = new ScrollViewer
                {
                    MaxHeight = 240,
                    Content = new TextBlock
                    {
                        Text = details,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("monospace"),
                    },
                },
            };
            panel.Children.Add(expander);
        }

        ContentDialog dialog = new()
        {
            Title = message,
            Content = panel,
            PrimaryButtonText = "File a Bug",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            PintaCore.Actions.Help.Bugs.Activate();
        }

        return ErrorDialogResponse.OK;
    }

    private async Task ShowMessageDialogAsync(string message, string body)
    {
        ContentDialog dialog = new()
        {
            Title = message,
            Content = body,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
