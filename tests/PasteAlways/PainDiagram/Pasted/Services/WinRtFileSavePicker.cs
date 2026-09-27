using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the FileSavePicker) lives here
using System.Collections.Generic;
using System.Threading.Tasks;
using PainDiagram.Helpers;
using Windows.Storage;
using Windows.Storage.Pickers;

// ReSharper disable once CheckNamespace
namespace PainDiagram.Services;

/// <summary>
/// The WinRT <see cref="FileSavePicker"/>, which is the save dialog the CodeBrix.Platform Skia
/// heads have. It creates an empty placeholder file at a brand-new path, so the chosen path
/// goes through <see cref="FileDialogHelper.RemoveEmptyPlaceholder"/> before it is returned and
/// the view model's own "replace existing file?" question stays the only one the user sees.
/// </summary>
public class WinRtFileSavePicker : IFileSavePicker
{
    /// <inheritdoc />
    public async Task<string> PickSavePngPathAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".png"
        };
        picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });

        StorageFile file = await picker.PickSaveFileAsync();
        if (file == null) { return null; }

        FileDialogHelper.RemoveEmptyPlaceholder(file.Path);
        return file.Path;
    }
}
