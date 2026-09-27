using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the FileSavePicker) lives here
using System.Collections.Generic;
using System.Threading.Tasks;
using WebcamPainter.Bridges;
using WebcamPainter.Helpers;
using WebcamPainter.ViewModels;
using WebcamPainter.Webcam;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace WebcamPainter.Views;

public sealed partial class MainPage : Page
{
    private MainViewModel ViewModel => DataContext as MainViewModel;

    //One frame renderer per canvas that shows live video (each caches its own buffers); the
    //  main canvas's renderer is the view model's, because the view model is what decides
    //  whether that canvas is showing video or the painting
    private readonly WebcamFrameRenderer _selfViewRenderer = new WebcamFrameRenderer();

    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is IFileSaveBridge fileSave)
            {
                fileSave.PickSaveJpegPathAsync = PickSaveJpegPathAsync;
            }

            if (DataContext is ICanvasBridge canvasBridge)
            {
                //Frames and tracking results arrive on capture/worker threads - marshal
                //  the repaints onto the UI thread
                canvasBridge.InvalidateMainCanvas = () => DispatcherQueue?.TryEnqueue(() => MainCanvas?.Invalidate());
                canvasBridge.InvalidateSelfView = () => DispatcherQueue?.TryEnqueue(() => SelfViewCanvas?.Invalidate());
            }
        };

        //Nothing else owns the view model - the XAML declares it - so the page is what runs its
        //  teardown: the camera stopped, the tracking thread joined, the bridge delegates dropped
        Unloaded += (_, _) => (DataContext as IDisposable)?.Dispose();

        InitializeComponent();

        //Which of the two things the main canvas shows is application state, so the handler
        //  forwards the surface and lets the view model draw (see ICanvasBridge)
        MainCanvas.PaintSurface += (_, e) =>
            (DataContext as ICanvasBridge)?.RenderMainCanvas(e.Surface, e.Info);

        SelfViewCanvas.PaintSurface += (_, e) =>
            _selfViewRenderer.Render(e.Surface, e.Info, ViewModel?.CaptureService, mirror: true);

        MainCanvas.SizeChanged += (_, _) => MainCanvas.Invalidate();
        SelfViewCanvas.SizeChanged += (_, _) => SelfViewCanvas.Invalidate();
    }

    private static async Task<string> PickSaveJpegPathAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".jpg"
        };
        picker.FileTypeChoices.Add("JPEG image", new List<string> { ".jpg" });

        var file = await picker.PickSaveFileAsync();
        if (file == null) { return null; }

        FileDialogHelper.RemoveEmptyPlaceholder(file.Path);
        return file.Path;
    }
}
