using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using WebcamViewer.Video;
using WebcamViewer.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the FolderPicker) lives here
using System.Threading.Tasks;

namespace WebcamViewer.Views;

public sealed partial class MainPage : Page
{
    //I tend to like to declare/define private methods above the constructor, in C# classes
    //The paint handler reaches the view model through the interface it implements, never the
    //  concrete type - the renderer only ever needs the newest frame
    private IVideoFrameSource FrameSource => DataContext as IVideoFrameSource;

    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is IFolderPickBridge folderPick)
            {
                folderPick.PickFolderPathAsync = PickFolderPathAsync;
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                //Frames arrive on a capture thread - marshal the repaint onto the UI thread
                invalidator.InvalidateCanvas = () => DispatcherQueue?.TryEnqueue(() => VideoView?.Invalidate());
            }
        };

        InitializeComponent();

        VideoView.PaintSurface += (_, e) => VideoCanvasHelper.RenderFrame(e.Surface, e.Info, FrameSource);
        VideoView.SizeChanged += (_, _) => VideoView.Invalidate();
    }

    private static async Task<string> PickFolderPathAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add("*");

        StorageFolder folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
