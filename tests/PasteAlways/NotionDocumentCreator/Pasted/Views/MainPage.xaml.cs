using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using NotionDocumentCreator.Helpers;
using NotionDocumentCreator.ViewModels;
using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the FileSavePicker) lives here
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace NotionDocumentCreator.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            //Give the view model a native "Save PDF as…" file dialog (CodeBrix.Platform's
            //  FileSavePicker). Heads with no windowing system throw NotSupportedException
            //  from the picker; the view model handles that.
            if (DataContext is IFileSaveBridge fileSave)
            {
                fileSave.PickSavePdfPathAsync = PickSavePdfPathAsync;
            }
        };

        this.InitializeComponent(); //Leave this line last
    }

    private static async Task<string> PickSavePdfPathAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".pdf"
        };
        picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });

        var file = await picker.PickSaveFileAsync();
        if (file == null) { return null; }

        //Some heads percent-encode the path they return, which would save "My Book.pdf" as
        //  "My%20Book.pdf"; decode it before anything touches the disk. That is the only thing
        //  done to it here: what the chosen path then means is the view model's business.
        return FileDialogHelper.ToFileSystemPath(file.Path);
    }
}
