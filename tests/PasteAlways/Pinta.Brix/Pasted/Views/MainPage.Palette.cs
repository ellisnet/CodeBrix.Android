// MainPage.Palette.cs
//
// The colour and palette UI: the status bar's palette widget, the colour
// picker it opens, and the Edit > Palette commands. Before this, the primary
// colour was whatever the engine defaulted to and could not be changed, which
// made the application unusable as a paint program.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Pinta.Brix.Controls;
using Pinta.Brix.Engine;
using Windows.Storage;
using Windows.Storage.Pickers;
using Drawing = Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Views;

public sealed partial class MainPage
{
    private PaletteWidget paletteWidget;

    private void BuildPaletteWidget()
    {
        paletteWidget = new PaletteWidget();
        paletteWidget.ColorEditRequested += async (_, args) => await EditColorAsync(args);
        PaletteWidgetHost.Content = paletteWidget;
    }

    private async Task EditColorAsync(PaletteColorEditEventArgs args)
    {
        (string title, Drawing.Color initial) = args.Target switch
        {
            PaletteColorTarget.Primary => ("Primary Color", PintaCore.Palette.PrimaryColor),
            PaletteColorTarget.Secondary => ("Secondary Color", PintaCore.Palette.SecondaryColor),
            _ => ("Palette Color", PintaCore.Palette.CurrentPalette.Colors[args.PaletteIndex]),
        };

        Drawing.Color? chosen = await ColorPickerDialog.ShowAsync(title, initial, XamlRoot);

        if (chosen is null) { return; }

        switch (args.Target)
        {
            case PaletteColorTarget.Primary:
                PintaCore.Palette.SetColor(setPrimary: true, chosen.Value);
                break;
            case PaletteColorTarget.Secondary:
                PintaCore.Palette.SetColor(setPrimary: false, chosen.Value);
                break;
            default:
                PintaCore.Palette.CurrentPalette.SetColor(args.PaletteIndex, chosen.Value);
                break;
        }
    }

    /// <summary>
    /// Upstream handles the X key globally to swap the primary and secondary
    /// colours; it is not a menu command, so it is not in the action model.
    /// </summary>
    private bool TryHandlePaletteKey(Windows.System.VirtualKey key)
    {
        if (key != Windows.System.VirtualKey.X) { return false; }

        //Only when no modifier is held - Ctrl+X is Cut.
        if (acceleratorTable.CurrentModifiers != Windows.System.VirtualKeyModifiers.None) { return false; }

        PintaCore.Palette.SwapColors();
        return true;
    }

    // ---- Edit > Palette ----------------------------------------------------

    private void WirePaletteActions()
    {
        EditActions edit = PintaCore.Actions.Edit;

        OnActivated(edit.LoadPalette, LoadPaletteAsync);
        OnActivated(edit.SavePalette, SavePaletteAsync);
        edit.ResetPalette.Activated += (_, _) => ResetPalette();
        OnActivated(edit.ResizePalette, ResizePaletteAsync);
    }

    private async Task LoadPaletteAsync()
    {
        FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };

        //The palette registry owns which extensions a load dialog accepts.
        foreach (string extension in PintaCore.PaletteFormats.GetLoadExtensions())
        {
            picker.FileTypeFilter.Add(extension);
        }

        StorageFile file = await picker.PickSingleFileAsync();
        if (file is null) { return; }

        try
        {
            PaletteDescriptor format = PintaCore.PaletteFormats.GetFormatByFilename(file.Path);

            if (format?.Loader is null)
            {
                await PintaCore.Chrome.ShowErrorDialog(
                    "Unsupported palette format",
                    $"Pinta.Brix cannot read '{System.IO.Path.GetFileName(file.Path)}'.",
                    string.Empty);
                return;
            }

            PintaCore.Palette.CurrentPalette.Load(format.Loader.Load(file.Path));
        }
        catch (Exception ex)
        {
            await PintaCore.Chrome.ShowErrorDialog("Cannot open palette", ex.Message, ex.ToString());
        }
    }

    private async Task SavePaletteAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "palette",
        };

        //The palette registry owns which formats a save dialog offers.
        foreach (FileDialogFilter filter in PintaCore.PaletteFormats.GetSaveFilters())
        {
            picker.FileTypeChoices.Add(filter.Name, [.. filter.Extensions]);
        }

        StorageFile file = await picker.PickSaveFileAsync();
        if (file is null) { return; }

        try
        {
            PaletteDescriptor format = PintaCore.PaletteFormats.GetFormatByFilename(file.Path);

            if (format?.Saver is null)
            {
                await PintaCore.Chrome.ShowErrorDialog(
                    "Unsupported palette format",
                    $"Pinta.Brix cannot write '{System.IO.Path.GetFileName(file.Path)}'.",
                    string.Empty);
                return;
            }

            PintaCore.Palette.CurrentPalette.Save(file.Path, format.Saver);
        }
        catch (Exception ex)
        {
            await PintaCore.Chrome.ShowErrorDialog("Cannot save palette", ex.Message, ex.ToString());
        }
    }

    private static void ResetPalette() =>
        PintaCore.Palette.CurrentPalette.Load(PaletteHelper.CreateDefault().Colors);

    private async Task ResizePaletteAsync()
    {
        NumberBox countBox = new()
        {
            Header = "Number of colors:",
            Value = PintaCore.Palette.CurrentPalette.Colors.Count,
            Minimum = 1,
            Maximum = 512,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };

        ContentDialog dialog = new()
        {
            Title = "Palette Size",
            Content = countBox,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) { return; }
        if (double.IsNaN(countBox.Value)) { return; }

        PintaCore.Palette.CurrentPalette.Resize((int)countBox.Value);
    }
}
