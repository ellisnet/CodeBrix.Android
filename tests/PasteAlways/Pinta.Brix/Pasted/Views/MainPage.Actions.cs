// MainPage.Actions.cs
//
// Attaches handlers to the engine's action model. Upstream spread the
// equivalent code over Pinta.Core/Actions/*.cs (which both declared and
// handled) and Pinta/Actions/** (handler classes). Here declaration lives in
// Pinta.Brix.Engine.Actions and handling lives here, so the engine half stays
// headless and testable.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Pinta.Brix.Views;

public sealed partial class MainPage
{
    private void WireActions()
    {
        ActionManager actions = PintaCore.Actions;

        // ---- File ---------------------------------------------------------

        actions.File.New.Activated += (_, _) => NewImage();
        OnActivated(actions.File.Open, OpenImageAsync);
        OnActivated(actions.File.Save, () => SaveActiveDocumentAsync(saveAs: false));
        OnActivated(actions.File.SaveAs, () => SaveActiveDocumentAsync(saveAs: true));
        OnActivated(actions.File.Close, () => CloseDocumentAsync(PintaCore.Workspace.ActiveDocument));
        OnActivated(actions.File.NewScreenshot, () =>
            PintaCore.Chrome.ShowMessageDialog(
                "Not available yet",
                "Taking a screenshot is not implemented in this port yet."));

        // ---- Edit ---------------------------------------------------------

        actions.Edit.Undo.Activated += (_, _) => Undo();
        actions.Edit.Redo.Activated += (_, _) => Redo();
        actions.Edit.Cut.Activated += (_, _) => CutToClipboard();
        actions.Edit.Copy.Activated += (_, _) => CopyToClipboard(merged: false);
        actions.Edit.CopyMerged.Activated += (_, _) => CopyToClipboard(merged: true);
        OnActivated(actions.Edit.Paste, () => PasteAsync(PasteTarget.CurrentLayer));
        OnActivated(actions.Edit.PasteIntoNewLayer, () => PasteAsync(PasteTarget.NewLayer));
        OnActivated(actions.Edit.PasteIntoNewImage, () => PasteAsync(PasteTarget.NewImage));
        actions.Edit.SelectAll.Activated += (_, _) => SelectAll();
        actions.Edit.Deselect.Activated += (_, _) => Deselect();
        actions.Edit.EraseSelection.Activated += (_, _) => EraseSelection();
        actions.Edit.FillSelection.Activated += (_, _) => FillSelection();
        actions.Edit.InvertSelection.Activated += (_, _) => InvertSelection();
        OnActivated(actions.Edit.OffsetSelection, OffsetSelectionAsync);

        // ---- View ---------------------------------------------------------

        actions.View.ZoomIn.Activated += (_, _) => WithWorkspace(w => w.ZoomIn());
        actions.View.ZoomOut.Activated += (_, _) => WithWorkspace(w => w.ZoomOut());
        actions.View.ActualSize.Activated += (_, _) => WithWorkspace(w => w.ZoomManually(1.0));
        actions.View.ZoomToWindow.Activated += (_, _) => ZoomToWindow();
        actions.View.ZoomToSelection.Activated += (_, _) => ZoomToSelection();
        actions.View.Fullscreen.Activated += (_, _) => ToggleFullscreen();
        OnActivated(actions.View.EditCanvasGrid, ShowCanvasGridDialogAsync);

        actions.View.MenuBar.Toggled += (value, _) => MainMenuBar.Visibility = ToVisibility(value);
        actions.View.ToolBar.Toggled += (value, _) => MainToolbarBorder.Visibility = ToVisibility(value);
        actions.View.StatusBar.Toggled += (value, _) => StatusBarGrid.Visibility = ToVisibility(value);
        actions.View.ToolBox.Toggled += (value, _) => ToolboxScroll.Visibility = ToVisibility(value);
        actions.View.ToolWindows.Toggled += (value, _) => PadsColumn.Visibility = ToVisibility(value);
        //DEFERRED: the platform's TabView exposes no tab-strip visibility, so
        //View > Image Tabs cannot hide the strip yet. Left wired so the menu
        //item's checked state is still honest about what it would do.

        // ---- Image --------------------------------------------------------

        actions.Image.CropToSelection.Activated += (_, _) => CropToSelection();
        actions.Image.AutoCrop.Activated += (_, _) => AutoCrop();
        OnActivated(actions.Image.Resize, ResizeImageAsync);
        OnActivated(actions.Image.CanvasSize, ResizeCanvasAsync);
        actions.Image.FlipHorizontal.Activated += (_, _) => WithDocument(d =>
        {
            d.FlipImageHorizontal();
            d.History.PushNewItem(new InvertHistoryItem(InvertType.FlipHorizontal));
        });
        actions.Image.FlipVertical.Activated += (_, _) => WithDocument(d =>
        {
            d.FlipImageVertical();
            d.History.PushNewItem(new InvertHistoryItem(InvertType.FlipVertical));
        });
        actions.Image.RotateCW.Activated += (_, _) => WithDocument(d =>
        {
            d.RotateImageCW();
            d.History.PushNewItem(new InvertHistoryItem(InvertType.Rotate90CW));
        });
        actions.Image.RotateCCW.Activated += (_, _) => WithDocument(d =>
        {
            d.RotateImageCCW();
            d.History.PushNewItem(new InvertHistoryItem(InvertType.Rotate90CCW));
        });
        actions.Image.Rotate180.Activated += (_, _) => WithDocument(d =>
        {
            d.RotateImage180();
            d.History.PushNewItem(new InvertHistoryItem(InvertType.Rotate180));
        });
        actions.Image.Flatten.Activated += (_, _) => FlattenImage();

        // ---- Layers -------------------------------------------------------

        actions.Layers.AddNewLayer.Activated += (_, _) => AddLayer();
        actions.Layers.DeleteLayer.Activated += (_, _) => DeleteLayer();
        actions.Layers.DuplicateLayer.Activated += (_, _) => DuplicateLayer();
        actions.Layers.MergeLayerDown.Activated += (_, _) => MergeLayerDown();
        actions.Layers.MoveLayerUp.Activated += (_, _) => MoveLayer(up: true);
        actions.Layers.MoveLayerDown.Activated += (_, _) => MoveLayer(up: false);
        actions.Layers.FlipHorizontal.Activated += (_, _) => FlipLayer(horizontal: true);
        actions.Layers.FlipVertical.Activated += (_, _) => FlipLayer(horizontal: false);
        OnActivated(actions.Layers.Properties, ShowLayerPropertiesAsync);
        OnActivated(actions.Layers.ImportFromFile, ImportLayerFromFileAsync);
        OnActivated(actions.Layers.RotateZoom, () =>
            PintaCore.Chrome.ShowMessageDialog(
                "Not available yet",
                "Rotate / Zoom Layer is not implemented in this port yet."));

        // ---- Window -------------------------------------------------------

        OnActivated(actions.Window.SaveAll, SaveAllAsync);
        OnActivated(actions.Window.CloseAll, () => CloseAllAsync());

        // ---- Help / App ---------------------------------------------------

        actions.Help.Contents.Activated += (_, _) => LaunchUri("https://www.pinta-project.com/user-guide/");
        actions.Help.Website.Activated += (_, _) => LaunchUri("https://www.pinta-project.com/");
        actions.Help.Bugs.Activated += (_, _) => LaunchUri("https://github.com/PintaProject/Pinta/issues");
        actions.Help.Translate.Activated += (_, _) => LaunchUri("https://translate.pinta-project.com/");
        OnActivated(actions.App.About, ShowAboutAsync);
        OnActivated(actions.App.KeyboardShortcuts, ShowKeyboardShortcutsAsync);
    }

    /// <summary>
    /// Attaches an asynchronous handler to a command. The action model's
    /// <c>Activated</c> event is synchronous, so the subscriber is an
    /// <c>async void</c> one; wrapping it here is what stops a failure inside
    /// a command from taking the application down, and keeps the wiring above
    /// at one line per command.
    /// </summary>
    private void OnActivated(Command command, Func<Task> handler)
    {
        command.Activated += async (_, _) =>
        {
            try
            {
                await handler();
            }
            catch (Exception ex)
            {
                await ReportHandlerFailure(ex);
            }
        };
    }

    /// <summary>
    /// Reports a failure that escaped an event handler. An event handler has
    /// nowhere to hand an exception back to, so it lands here instead of
    /// ending the application; the report itself is guarded, because showing a
    /// dialog is one more UI operation that can fail.
    /// </summary>
    private static async Task ReportHandlerFailure(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled failure in a page event handler: {ex}");

        try
        {
            await PintaCore.Chrome.ShowErrorDialog("Something went wrong", ex.Message, ex.ToString());
        }
        catch (Exception)
        {
            //Nothing more can be done here, and a failed report must not be
            //worse than the failure it was reporting.
        }
    }

    private static Microsoft.UI.Xaml.Visibility ToVisibility(bool visible) =>
        visible ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    // ---- Sensitivity -------------------------------------------------------

    /// <summary>
    /// Enables and disables commands to match the current document, selection
    /// and history state. Upstream drove this from a scattering of event
    /// handlers; doing it in one pass makes the rules visible in one place.
    /// </summary>
    private void UpdateActionSensitivity()
    {
        ActionManager actions = PintaCore.Actions;

        bool hasDocument = PintaCore.Workspace.HasOpenDocuments;

        actions.File.Save.Sensitive = hasDocument;
        actions.File.SaveAs.Sensitive = hasDocument;
        actions.File.Close.Sensitive = hasDocument;
        actions.Window.SaveAll.Sensitive = hasDocument;
        actions.Window.CloseAll.Sensitive = hasDocument;

        foreach (Command command in actions.Image.Commands())
            command.Sensitive = hasDocument;

        foreach (Command command in actions.Layers.Commands())
            command.Sensitive = hasDocument;

        foreach (Command command in actions.View.Commands())
        {
            //The visibility toggles stay usable with no document open; only the
            //zoom commands need one.
            if (command is not ToggleCommand)
                command.Sensitive = hasDocument;
        }

        actions.Edit.Cut.Sensitive = hasDocument;
        actions.Edit.Copy.Sensitive = hasDocument;
        actions.Edit.CopyMerged.Sensitive = hasDocument;
        actions.Edit.Paste.Sensitive = hasDocument;
        actions.Edit.PasteIntoNewLayer.Sensitive = hasDocument;
        actions.Edit.SelectAll.Sensitive = hasDocument;
        actions.Edit.EraseSelection.Sensitive = hasDocument;
        actions.Edit.FillSelection.Sensitive = hasDocument;
        actions.Edit.InvertSelection.Sensitive = hasDocument;
        actions.Edit.OffsetSelection.Sensitive = hasDocument;

        if (!hasDocument)
        {
            actions.Edit.Undo.Sensitive = false;
            actions.Edit.Redo.Sensitive = false;
            actions.Edit.Deselect.Sensitive = false;
            actions.Image.CropToSelection.Sensitive = false;
            actions.Layers.DeleteLayer.Sensitive = false;
            actions.Layers.MergeLayerDown.Sensitive = false;
            actions.Layers.MoveLayerUp.Sensitive = false;
            actions.Layers.MoveLayerDown.Sensitive = false;
            actions.Image.Flatten.Sensitive = false;
            return;
        }

        Document document = PintaCore.Workspace.ActiveDocument;
        DocumentHistory history = document.History;

        actions.Edit.Undo.Sensitive = history.CanUndo;
        actions.Edit.Redo.Sensitive = history.CanRedo;

        bool hasSelection = document.Selection.Visible;
        actions.Edit.Deselect.Sensitive = hasSelection;
        actions.Image.CropToSelection.Sensitive = hasSelection;
        actions.View.ZoomToSelection.Sensitive = hasSelection;

        int layerCount = document.Layers.Count();
        int currentIndex = document.Layers.CurrentUserLayerIndex;

        actions.Layers.DeleteLayer.Sensitive = layerCount > 1;
        actions.Layers.MergeLayerDown.Sensitive = currentIndex > 0;
        actions.Layers.MoveLayerUp.Sensitive = currentIndex < layerCount - 1;
        actions.Layers.MoveLayerDown.Sensitive = currentIndex > 0;
        actions.Image.Flatten.Sensitive = layerCount > 1;
    }

    // ---- Small helpers -----------------------------------------------------

    private static void WithDocument(Action<Document> action)
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        PintaCore.Tools.Commit();
        action(PintaCore.Workspace.ActiveDocument);
    }

    private static void WithWorkspace(Action<DocumentWorkspace> action)
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        action(PintaCore.Workspace.ActiveWorkspace);
    }

    private static void LaunchUri(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            //No browser, or the platform refused to launch one. A help link
            //failing must never take the application down.
        }
    }

    // ---- File --------------------------------------------------------------

    private void NewImage()
    {
        //V1: fixed default size with a white background. The full New Image
        //dialog (presets, clipboard-size detection, orientation) is P13.6.
        PintaCore.Workspace.NewDocument(new Size(800, 600), new Color(1, 1, 1));
    }

    private async Task OpenImageAsync()
    {
        FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.PicturesLibrary };

        //The registry owns which extensions an open dialog accepts.
        foreach (string extension in PintaCore.ImageFormats.GetImportExtensions())
        {
            picker.FileTypeFilter.Add(extension);
        }

        StorageFile file = await picker.PickSingleFileAsync();
        if (file is null) { return; }

        PintaCore.Workspace.OpenFile(file.Path);
        PintaCore.RecentFiles.AddFile(file.Path);
    }

    private async Task SaveActiveDocumentAsync(bool saveAs)
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        await PintaCore.Workspace.ActiveDocument.Save(saveAs);
    }

    private async Task SaveAllAsync()
    {
        foreach (Document document in PintaCore.Workspace.OpenDocuments.ToList())
        {
            if (document.IsDirty)
            {
                await document.Save(saveAs: false);
            }
        }
    }

    // ---- Edit --------------------------------------------------------------

    private void Undo()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        DocumentHistory history = PintaCore.Workspace.ActiveWorkspace.History;
        if (history.CanUndo) { history.Undo(); }
    }

    private void Redo()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        DocumentHistory history = PintaCore.Workspace.ActiveWorkspace.History;
        if (history.CanRedo) { history.Redo(); }
    }

    private void CopyToClipboard(bool merged)
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        Document document = PintaCore.Workspace.ActiveDocument;

        //Give the active tool first refusal - the text tool copies its own
        //buffer rather than pixels.
        if (!merged && PintaCore.Tools.DoHandleCopy(document, PintaCore.Clipboard)) { return; }

        PintaCore.Tools.Commit();

        ImageSurface image = merged
            ? document.GetFlattenedImage(clip_to_selection: true)
            : GetClippedCurrentLayer(document);

        PintaCore.Clipboard.SetImage(image);
    }

    private void CutToClipboard()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }
        Document document = PintaCore.Workspace.ActiveDocument;

        if (PintaCore.Tools.DoHandleCut(document, PintaCore.Clipboard)) { return; }

        PintaCore.Tools.Commit();
        PintaCore.Clipboard.SetImage(GetClippedCurrentLayer(document));
        EraseSelection(historyText: "Cut", icon: StandardIcons.EditCut);
    }

    private static ImageSurface GetClippedCurrentLayer(Document document)
    {
        RectangleI bounds = document.Selection.GetBounds().ToInt();

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = new RectangleI(0, 0, document.ImageSize.Width, document.ImageSize.Height);
        }

        ImageSurface result = new(Format.Argb32, bounds.Width, bounds.Height);

        using Context g = new(result);
        g.Translate(-bounds.X, -bounds.Y);
        g.AppendPath(document.Selection.SelectionPath);
        g.FillRule = FillRule.EvenOdd;
        g.Clip();
        g.SetSourceSurface(document.Layers.CurrentUserLayer.Surface, 0, 0);
        g.Paint();

        return result;
    }

    private enum PasteTarget
    {
        CurrentLayer,
        NewLayer,
        NewImage,
    }

    private async Task PasteAsync(PasteTarget target)
    {
        Document document = PintaCore.Workspace.HasOpenDocuments ? PintaCore.Workspace.ActiveDocument : null;

        if (target == PasteTarget.CurrentLayer
            && document is not null
            && await PintaCore.Tools.DoHandlePaste(document, PintaCore.Clipboard))
        {
            return;
        }

        ImageSurface image = await PintaCore.Clipboard.GetImageAsync();

        if (image is null)
        {
            await PintaCore.Chrome.ShowMessageDialog(
                "Paste",
                "The clipboard does not contain an image.");
            return;
        }

        if (target == PasteTarget.NewImage || document is null)
        {
            PintaCore.Workspace.NewDocumentFromImage(image);
            return;
        }

        PintaCore.Tools.Commit();

        //Upstream offers to grow the canvas when the pasted image will not
        //fit; do the same rather than silently cropping.
        if (image.Width > document.ImageSize.Width || image.Height > document.ImageSize.Height)
        {
            ContentDialog resizeDialog = new()
            {
                Title = "Image larger than canvas",
                Content = "The image being pasted is larger than the canvas size. What would you like to do?",
                PrimaryButtonText = "Resize canvas",
                SecondaryButtonText = "Keep canvas size",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
            };

            ContentDialogResult result = await resizeDialog.ShowAsync();

            if (result == ContentDialogResult.None) { return; }

            if (result == ContentDialogResult.Primary)
            {
                document.ResizeCanvas(
                    new Size(
                        Math.Max(image.Width, document.ImageSize.Width),
                        Math.Max(image.Height, document.ImageSize.Height)),
                    Anchor.Center,
                    null);
            }
        }

        if (target == PasteTarget.NewLayer)
        {
            document.Layers.AddNewLayer(string.Empty);
            document.History.PushNewItem(new AddLayerHistoryItem(
                Icons.LayerNew, "Paste Into New Layer", document.Layers.CurrentUserLayerIndex));
        }

        DocumentSelection oldSelection = document.Selection.Clone();

        using (Context g = new(document.Layers.CurrentUserLayer.Surface))
        {
            g.SetSourceSurface(image, 0, 0);
            g.Paint();
        }

        document.History.PushNewItem(new PasteHistoryItem(image, oldSelection));
        document.Workspace.Invalidate();
    }

    private void SelectAll()
    {
        WithDocument(document =>
        {
            SelectionHistoryItem history = new(
                PintaCore.Workspace, StandardIcons.EditSelectAll, "Select All");
            history.TakeSnapshot();

            document.ResetSelectionPaths();
            document.Selection.Visible = true;

            document.History.PushNewItem(history);
            document.Workspace.Invalidate();
        });
    }

    private void Deselect()
    {
        WithDocument(document =>
        {
            SelectionHistoryItem history = new(
                PintaCore.Workspace, Icons.EditSelectionNone, "Deselect");
            history.TakeSnapshot();

            document.ResetSelectionPaths();

            document.History.PushNewItem(history);
            document.Workspace.Invalidate();
        });
    }

    private void EraseSelection(string historyText = "Erase Selection", string icon = null)
    {
        WithDocument(document =>
        {
            ImageSurface old = document.Layers.CurrentUserLayer.Surface.Clone();

            using (Context g = new(document.Layers.CurrentUserLayer.Surface))
            {
                g.AppendPath(document.Selection.SelectionPath);
                g.FillRule = FillRule.EvenOdd;
                g.Operator = Operator.Clear;
                g.Fill();
            }

            document.Workspace.Invalidate();
            document.History.PushNewItem(new SimpleHistoryItem(
                icon ?? Icons.EditSelectionErase,
                historyText,
                old,
                document.Layers.CurrentUserLayerIndex));
        });
    }

    private void FillSelection()
    {
        WithDocument(document =>
        {
            ImageSurface old = document.Layers.CurrentUserLayer.Surface.Clone();

            using (Context g = new(document.Layers.CurrentUserLayer.Surface))
            {
                g.AppendPath(document.Selection.SelectionPath);
                g.FillRule = FillRule.EvenOdd;
                g.SetSourceColor(PintaCore.Palette.PrimaryColor);
                g.Fill();
            }

            document.Workspace.Invalidate();
            document.History.PushNewItem(new SimpleHistoryItem(
                Icons.EditSelectionFill,
                "Fill Selection",
                old,
                document.Layers.CurrentUserLayerIndex));
        });
    }

    private void InvertSelection()
    {
        WithDocument(document =>
        {
            SelectionHistoryItem history = new(
                PintaCore.Workspace, Icons.EditSelectionInvert, "Invert Selection");
            history.TakeSnapshot();

            document.Selection.Invert(document.ImageSize);
            document.Selection.Visible = true;

            document.History.PushNewItem(history);
            document.Workspace.Invalidate();
        });
    }

    // ---- Image -------------------------------------------------------------

    private void CropToSelection()
    {
        WithDocument(document =>
            CropImageToRectangle(document, document.GetSelectedBounds(true), document.Selection.SelectionPath));
    }

    private void AutoCrop()
    {
        WithDocument(document =>
        {
            ImageSurface image = document.GetFlattenedImage();

            //Upstream ignores the current selection when auto-cropping.
            CropImageToRectangle(document, Utility.GetObjectBounds(image), null);
        });
    }

    private static void CropImageToRectangle(Document document, RectangleI rect, Path selection)
    {
        if (rect.Width <= 0 || rect.Height <= 0) { return; }

        ResizeHistoryItem history = new(PintaCore.Workspace, document.ImageSize)
        {
            Icon = Icons.ImageCrop,
            Text = "Crop to Selection",
        };

        history.StartSnapshotOfImage();
        history.RestoreSelection = document.Selection.Clone();

        //The zoom level must survive the resize, so it is captured and put back.
        double originalScale = document.Workspace.Scale;
        document.ImageSize = rect.Size;
        document.Workspace.ViewSize = rect.Size;
        document.Workspace.Scale = originalScale;

        document.Workspace.UpdateCanvasScale();

        foreach (UserLayer layer in document.Layers.UserLayers)
        {
            layer.Crop(rect, selection);
        }

        history.FinishSnapshotOfImage();

        document.History.PushNewItem(history);
        document.ResetSelectionPaths();
        document.Workspace.Invalidate();
    }

    private void FlattenImage()
    {
        WithDocument(document =>
        {
            if (document.Layers.Count() < 2) { return; }
            document.Layers.FlattenLayers();
        });
    }

    // ---- Layers ------------------------------------------------------------

    private void AddLayer()
    {
        WithDocument(document =>
        {
            document.Layers.AddNewLayer(string.Empty);
            document.History.PushNewItem(new AddLayerHistoryItem(
                Icons.LayerNew, "Add New Layer", document.Layers.CurrentUserLayerIndex));
        });
    }

    private void DeleteLayer()
    {
        WithDocument(document =>
        {
            if (document.Layers.Count() <= 1) { return; }

            DeleteLayerHistoryItem history = new(
                Icons.LayerDelete, "Delete Layer",
                document.Layers.CurrentUserLayer, document.Layers.CurrentUserLayerIndex);

            document.Layers.DeleteCurrentLayer();
            document.History.PushNewItem(history);
        });
    }

    private void DuplicateLayer()
    {
        WithDocument(document =>
        {
            document.Layers.DuplicateCurrentLayer();
            document.History.PushNewItem(new AddLayerHistoryItem(
                Icons.LayerDuplicate, "Duplicate Layer", document.Layers.CurrentUserLayerIndex));
        });
    }

    private void MergeLayerDown()
    {
        WithDocument(document =>
        {
            if (document.Layers.CurrentUserLayerIndex <= 0) { return; }

            CompoundHistoryItem history = new(Icons.LayerMergeDown, "Merge Layer Down");
            int bottomIndex = document.Layers.CurrentUserLayerIndex - 1;

            history.Push(new DeleteLayerHistoryItem(
                Icons.LayerMergeDown, "Merge Layer Down",
                document.Layers.CurrentUserLayer, document.Layers.CurrentUserLayerIndex));
            history.Push(new SimpleHistoryItem(
                Icons.LayerMergeDown, "Merge Layer Down",
                document.Layers[bottomIndex].Surface.Clone(), bottomIndex));

            document.Layers.MergeCurrentLayerDown();
            document.History.PushNewItem(history);
        });
    }

    private void MoveLayer(bool up)
    {
        WithDocument(document =>
        {
            int index = document.Layers.CurrentUserLayerIndex;
            int target = up ? index + 1 : index - 1;

            if (target < 0 || target >= document.Layers.Count()) { return; }

            if (up)
            {
                document.Layers.MoveCurrentLayerUp();
            }
            else
            {
                document.Layers.MoveCurrentLayerDown();
            }

            document.History.PushNewItem(new SwapLayersHistoryItem(
                up ? StandardIcons.LayerMoveUp : StandardIcons.LayerMoveDown,
                up ? "Move Layer Up" : "Move Layer Down",
                index,
                target));
        });
    }

    private void FlipLayer(bool horizontal)
    {
        WithDocument(document =>
        {
            ImageSurface old = document.Layers.CurrentUserLayer.Surface.Clone();

            if (horizontal)
            {
                document.Layers.CurrentUserLayer.FlipHorizontal();
            }
            else
            {
                document.Layers.CurrentUserLayer.FlipVertical();
            }

            document.History.PushNewItem(new SimpleHistoryItem(
                horizontal ? Icons.LayerFlipHorizontal : Icons.LayerFlipVertical,
                horizontal ? "Flip Layer Horizontal" : "Flip Layer Vertical",
                old,
                document.Layers.CurrentUserLayerIndex));

            document.Workspace.Invalidate();
        });
    }

    // ---- View --------------------------------------------------------------

    private void ZoomToWindow()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        DocumentWorkspace workspace = PintaCore.Workspace.ActiveWorkspace;
        Size imageSize = PintaCore.Workspace.ActiveDocument.ImageSize;
        Size viewport = workspace.CanvasWindow?.ViewportSize ?? new Size(0, 0);

        if (viewport.Width <= 0 || viewport.Height <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0) { return; }

        double ratio = Math.Min(
            (double)viewport.Width / imageSize.Width,
            (double)viewport.Height / imageSize.Height);

        workspace.ZoomManually(ratio);
    }

    private void ZoomToSelection()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        Document document = PintaCore.Workspace.ActiveDocument;
        RectangleI bounds = document.Selection.GetBounds().ToInt();

        if (bounds.Width <= 0 || bounds.Height <= 0) { return; }

        DocumentWorkspace workspace = document.Workspace;
        Size viewport = workspace.CanvasWindow?.ViewportSize ?? new Size(0, 0);

        if (viewport.Width <= 0 || viewport.Height <= 0) { return; }

        workspace.ZoomManually(Math.Min(
            (double)viewport.Width / bounds.Width,
            (double)viewport.Height / bounds.Height));
    }
}
