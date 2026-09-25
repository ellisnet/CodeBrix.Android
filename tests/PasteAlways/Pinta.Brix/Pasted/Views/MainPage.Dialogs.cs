// MainPage.Dialogs.cs
//
// The application's dialogs, and the data-loss prompts that go with them.
// Upstream carried these as one class per dialog under Pinta/Dialogs; they are
// gathered here because each is a ContentDialog assembled in code rather than
// a .ui file, and because they share the save-confirmation flow.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Pinta.Brix.Views;

public sealed partial class MainPage
{
    /// <summary>
    /// What the user chose when asked about unsaved changes.
    /// </summary>
    private enum SaveConfirmation
    {
        Save,
        Discard,
        Cancel,
    }

    // ---- Data-loss prompts -------------------------------------------------

    private async Task<SaveConfirmation> ConfirmDiscardAsync(Document document)
    {
        ContentDialog dialog = new()
        {
            Title = $"Save changes to \"{document.DisplayName}\" before closing?",
            Content = "If you don't save, all changes will be permanently lost.",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Close without saving",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => SaveConfirmation.Save,
            ContentDialogResult.Secondary => SaveConfirmation.Discard,
            _ => SaveConfirmation.Cancel,
        };
    }

    /// <summary>
    /// Closes a document, prompting first when it has unsaved changes.
    /// </summary>
    /// <returns>False when the user cancelled.</returns>
    private async Task<bool> CloseDocumentAsync(Document document)
    {
        if (document is null) { return true; }

        if (document.IsDirty)
        {
            switch (await ConfirmDiscardAsync(document))
            {
                case SaveConfirmation.Cancel:
                    return false;

                case SaveConfirmation.Save:
                    //A failed or cancelled save must not lose the document.
                    if (!await document.Save(saveAs: false)) { return false; }
                    break;
            }
        }

        PintaCore.Workspace.CloseDocument(document);
        return true;
    }

    private async Task<bool> CloseAllAsync()
    {
        foreach (Document document in PintaCore.Workspace.OpenDocuments.ToList())
        {
            if (!await CloseDocumentAsync(document)) { return false; }
        }

        return true;
    }

    /// <summary>
    /// Runs the save-prompt loop over every dirty document. The page hands this
    /// to the view model as <c>IShellCloseBridge.ConfirmCloseApplicationAsync</c>,
    /// which is how the window close reaches it; a false result means the close
    /// should be abandoned.
    /// </summary>
    /// <remarks>
    /// There is deliberately no File &gt; Quit command to reach this from - on
    /// a chrome-less head there is no way out of the application by design.
    /// </remarks>
    private async Task<bool> ConfirmCloseApplicationAsync() => await CloseAllAsync();

    // ---- Image dialogs -----------------------------------------------------

    private async Task ResizeImageAsync()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        Document document = PintaCore.Workspace.ActiveDocument;
        Size original = document.ImageSize;

        NumberBox widthBox = new() { Header = "Width:", Value = original.Width, Minimum = 1, Maximum = 32000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox heightBox = new() { Header = "Height:", Value = original.Height, Minimum = 1, Maximum = 32000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        CheckBox aspectBox = new() { Content = "Maintain aspect ratio", IsChecked = true };

        bool updating = false;

        widthBox.ValueChanged += (_, _) =>
        {
            if (updating || aspectBox.IsChecked != true || double.IsNaN(widthBox.Value)) { return; }
            updating = true;
            heightBox.Value = Math.Max(1, Math.Round(widthBox.Value * original.Height / original.Width));
            updating = false;
        };

        heightBox.ValueChanged += (_, _) =>
        {
            if (updating || aspectBox.IsChecked != true || double.IsNaN(heightBox.Value)) { return; }
            updating = true;
            widthBox.Value = Math.Max(1, Math.Round(heightBox.Value * original.Width / original.Height));
            updating = false;
        };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(widthBox);
        panel.Children.Add(heightBox);
        panel.Children.Add(aspectBox);

        ContentDialog dialog = new()
        {
            Title = "Resize Image",
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return; }

        Size newSize = new((int)widthBox.Value, (int)heightBox.Value);
        if (newSize == original) { return; }

        document.ResizeImage(newSize, ResamplingMode.Bilinear);
    }

    private async Task ResizeCanvasAsync()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        Document document = PintaCore.Workspace.ActiveDocument;
        Size original = document.ImageSize;

        NumberBox widthBox = new() { Header = "Width:", Value = original.Width, Minimum = 1, Maximum = 32000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox heightBox = new() { Header = "Height:", Value = original.Height, Minimum = 1, Maximum = 32000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

        //Upstream's 9-way anchor grid: the button that is pressed says where the
        //existing image sits inside the new canvas.
        Anchor anchor = Anchor.Center;
        Grid anchorGrid = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        for (int i = 0; i < 3; i++)
        {
            anchorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            anchorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        Anchor[,] anchors = {
            { Anchor.NW, Anchor.N, Anchor.NE },
            { Anchor.W, Anchor.Center, Anchor.E },
            { Anchor.SW, Anchor.S, Anchor.SE },
        };

        List<RadioButton> anchorButtons = [];

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                Anchor value = anchors[row, column];

                RadioButton button = new()
                {
                    GroupName = "CanvasAnchor",
                    MinWidth = 40,
                    MinHeight = 32,
                    IsChecked = value == Anchor.Center,
                    Margin = new Thickness(1),
                };

                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                button.Checked += (_, _) => anchor = value;
                anchorButtons.Add(button);
                anchorGrid.Children.Add(button);
            }
        }

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(widthBox);
        panel.Children.Add(heightBox);
        panel.Children.Add(new TextBlock { Text = "Anchor:", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(anchorGrid);

        ContentDialog dialog = new()
        {
            Title = "Resize Canvas",
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return; }

        Size newSize = new((int)widthBox.Value, (int)heightBox.Value);
        if (newSize == original) { return; }

        document.ResizeCanvas(newSize, anchor, null);
    }

    // ---- Edit dialogs ------------------------------------------------------

    private async Task OffsetSelectionAsync()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        Document document = PintaCore.Workspace.ActiveDocument;

        NumberBox offsetBox = new()
        {
            Header = "Offset (pixels). Positive grows the selection, negative shrinks it:",
            Value = 0,
            Minimum = -1000,
            Maximum = 1000,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };

        ContentDialog dialog = new()
        {
            Title = "Offset Selection",
            Content = offsetBox,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return; }
        if (double.IsNaN(offsetBox.Value) || offsetBox.Value == 0) { return; }

        PintaCore.Tools.Commit();

        SelectionHistoryItem history = new(
            PintaCore.Workspace, Icons.EditSelectionOffset, "Offset Selection");
        history.TakeSnapshot();

        document.Selection.Offset(offsetBox.Value);

        document.History.PushNewItem(history);
        document.Workspace.Invalidate();
    }

    // ---- Layer dialogs -----------------------------------------------------

    private async Task ShowLayerPropertiesAsync()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        Document document = PintaCore.Workspace.ActiveDocument;
        UserLayer layer = document.Layers.CurrentUserLayer;

        LayerProperties initial = new(layer.Name, layer.Hidden, layer.Opacity, layer.BlendMode);

        TextBox nameBox = new() { Header = "Name:", Text = layer.Name };
        CheckBox visibleBox = new() { Content = "Visible", IsChecked = !layer.Hidden };
        Slider opacitySlider = new() { Header = "Opacity:", Minimum = 0, Maximum = 100, Value = layer.Opacity * 100 };
        ComboBox blendBox = new() { Header = "Blend Mode:" };

        BlendMode[] blendModes = Enum.GetValues<BlendMode>();
        foreach (BlendMode mode in blendModes)
        {
            blendBox.Items.Add(mode.ToString());
        }
        blendBox.SelectedIndex = Array.IndexOf(blendModes, layer.BlendMode);

        //Upstream updates the canvas live while the dialog is open, and puts
        //everything back if the user cancels.
        void Apply()
        {
            layer.Name = nameBox.Text;
            layer.Hidden = visibleBox.IsChecked != true;
            layer.Opacity = opacitySlider.Value / 100.0;
            if (blendBox.SelectedIndex >= 0) { layer.BlendMode = blendModes[blendBox.SelectedIndex]; }
            document.Workspace.Invalidate();
        }

        nameBox.TextChanged += (_, _) => Apply();
        visibleBox.Checked += (_, _) => Apply();
        visibleBox.Unchecked += (_, _) => Apply();
        opacitySlider.ValueChanged += (_, _) => Apply();
        blendBox.SelectionChanged += (_, _) => Apply();

        StackPanel panel = new() { Spacing = 8, MinWidth = 320 };
        panel.Children.Add(nameBox);
        panel.Children.Add(visibleBox);
        panel.Children.Add(opacitySlider);
        panel.Children.Add(blendBox);

        ContentDialog dialog = new()
        {
            Title = "Layer Properties",
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            LayerProperties updated = new(layer.Name, layer.Hidden, layer.Opacity, layer.BlendMode);

            if (!updated.Equals(initial))
            {
                document.History.PushNewItem(new UpdateLayerPropertiesHistoryItem(
                    Icons.LayerProperties,
                    "Layer Properties",
                    document.Layers.CurrentUserLayerIndex,
                    initial,
                    updated));
            }

            RefreshLayersPad();
            return;
        }

        //Cancelled - restore.
        layer.Name = initial.Name;
        layer.Hidden = initial.Hidden;
        layer.Opacity = initial.Opacity;
        layer.BlendMode = initial.BlendMode;
        document.Workspace.Invalidate();
    }

    private async Task ImportLayerFromFileAsync()
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.PicturesLibrary };

        //The registry owns which extensions an open dialog accepts.
        foreach (string extension in PintaCore.ImageFormats.GetImportExtensions())
        {
            picker.FileTypeFilter.Add(extension);
        }

        StorageFile file = await picker.PickSingleFileAsync();
        if (file is null) { return; }

        Document document = PintaCore.Workspace.ActiveDocument;

        try
        {
            //Load into a scratch document so the importer's own logic is reused,
            //then lift its flattened pixels into a new layer here.
            var descriptor = PintaCore.ImageFormats.GetFormatByFile(file.Path);

            if (descriptor is null || !descriptor.IsImportAvailable())
            {
                await PintaCore.Chrome.ShowErrorDialog(
                    "Cannot import layer", $"No importer is available for '{file.Path}'.", string.Empty);
                return;
            }

            PintaCore.Tools.Commit();

            ImageSurface imported = LoadImageSurface(file.Path);

            if (imported is null) { return; }

            UserLayer layer = document.Layers.AddNewLayer(System.IO.Path.GetFileName(file.Path));

            using (Context g = new(layer.Surface))
            {
                g.SetSourceSurface(imported, 0, 0);
                g.Paint();
            }

            document.History.PushNewItem(new AddLayerHistoryItem(
                Icons.LayerImport, "Import From File", document.Layers.CurrentUserLayerIndex));

            document.Workspace.Invalidate();
        }
        catch (Exception ex)
        {
            await PintaCore.Chrome.ShowErrorDialog("Cannot import layer", ex.Message, ex.ToString());
        }
    }

    private static ImageSurface LoadImageSurface(string path)
    {
        using SkiaSharp.SKBitmap decoded = SkiaSharp.SKBitmap.Decode(path);

        if (decoded is null) { return null; }

        ImageSurface surface = new(Format.Argb32, decoded.Width, decoded.Height);

        using (SkiaSharp.SKCanvas canvas = new(surface.Bitmap))
        {
            canvas.DrawBitmap(decoded, 0, 0, SkiaSharp.SKSamplingOptions.Default, null);
            canvas.Flush();
        }

        surface.MarkDirty();
        return surface;
    }

    // ---- View dialogs ------------------------------------------------------

    private async Task ShowCanvasGridDialogAsync()
    {
        NumberBox widthBox = new() { Header = "Cell width:", Value = PintaCore.CanvasGrid.CellWidth, Minimum = 1, Maximum = 1000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox heightBox = new() { Header = "Cell height:", Value = PintaCore.CanvasGrid.CellHeight, Minimum = 1, Maximum = 1000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        CheckBox showBox = new() { Content = "Show grid", IsChecked = PintaCore.CanvasGrid.ShowGrid };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(showBox);
        panel.Children.Add(widthBox);
        panel.Children.Add(heightBox);

        ContentDialog dialog = new()
        {
            Title = "Canvas Grid",
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return; }

        PintaCore.CanvasGrid.CellWidth = (int)widthBox.Value;
        PintaCore.CanvasGrid.CellHeight = (int)heightBox.Value;
        PintaCore.CanvasGrid.ShowGrid = showBox.IsChecked == true;

        if (PintaCore.Workspace.HasOpenDocuments)
        {
            PintaCore.Workspace.ActiveWorkspace.Invalidate();
        }
    }

    private void ToggleFullscreen()
    {
        //DEFERRED. Microsoft.UI.Windowing.AppWindow - the route to a full-screen
        //presenter - is internal in CodeBrix.Platform unless HAS_CODEBRIX_WINUI
        //is defined, so an application cannot reach it. Per the chrome decision
        //this must be a NO-OP rather than an error: the Frame Buffer head is
        //already full-screen, so there is nothing to toggle there anyway.
        //Chase the presenter seam when the rest of the View menu lands.
        isFullscreen = !isFullscreen;

        PintaCore.Chrome.SetStatusBarText(isFullscreen
            ? "Fullscreen is not available on this head yet."
            : string.Empty);
    }

    private bool isFullscreen;

    // ---- Help dialogs ------------------------------------------------------

    private async Task ShowAboutAsync()
    {
        StackPanel panel = new() { Spacing = 6, MinWidth = 360 };
        panel.Children.Add(new TextBlock { Text = "Pinta.Brix", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"A port of Pinta {PintaCore.ApplicationVersion} to CodeBrix.Platform.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Pinta is Copyright (c) Jonathan Pobst and contributors, MIT licensed.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "See THIRD-PARTY-NOTICES.txt for the full attribution list.", TextWrapping = TextWrapping.Wrap });

        ContentDialog dialog = new()
        {
            Title = "About",
            Content = panel,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private async Task ShowKeyboardShortcutsAsync()
    {
        StackPanel panel = new() { Spacing = 2 };

        foreach (Command command in PintaCore.Actions.AllCommands())
        {
            string shortcut = command.Shortcuts.FirstOrDefault();

            if (string.IsNullOrEmpty(shortcut)) { continue; }

            Grid row = new();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = new() { Text = command.Label.TrimEnd('.') };
            TextBlock keys = new() { Text = FormatShortcut(shortcut), FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("monospace") };
            Grid.SetColumn(keys, 1);

            row.Children.Add(label);
            row.Children.Add(keys);
            panel.Children.Add(row);
        }

        ContentDialog dialog = new()
        {
            Title = "Keyboard Shortcuts",
            Content = new ScrollViewer { Content = panel, MaxHeight = 420, MinWidth = 380 },
            CloseButtonText = "Close",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }
}
