using CodeBrix.Platform.Simple;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleCbxVideoPlayer.SkiaVideo.Playback;
using SimpleCbxVideoPlayer.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Storage;
using Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SimpleCbxVideoPlayer.Views;

public sealed partial class MainPage : Page
{
    private SkiaGLCanvasElement gpuCanvas;
    private bool hasSettledVideoSurface;

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is ICanvasBridge canvasBridge)
            {
                //Raised on the decoding thread: hop to the user-interface thread and mark the canvas dirty
                canvasBridge.InvalidateVideoCanvas = () => DispatcherQueue?.TryEnqueue(InvalidateVideoCanvas);
            }

            if (DataContext is IFileSaveBridge fileSave)
            {
                //The bake's destination: this head has a picker, and a head without one wires nothing
                fileSave.PickSaveCubePathAsync = PickSaveCubePathAsync;
            }
        };

        Loaded += OnLoaded;
        Unloaded += (_, _) => ViewModel?.Shutdown();

        this.InitializeComponent(); //Leave this line last
    }

    private MainViewModel ViewModel => DataContext as MainViewModel;

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (gpuCanvas != null) { return; }

        try
        {
            //The GPU canvas is built here rather than in XAML because constructing it is what starts the
            //  graphics API, and that must not happen inside InitializeComponent().
            gpuCanvas = new SkiaGLCanvasElement();
            gpuCanvas.PaintSurface += OnGpuPaintSurface;
            VideoHost.Children.Insert(0, gpuCanvas);

            CpuCanvas.SizeChanged += (_, _) => CpuCanvas.Invalidate();

            //IsGpuInitialized reads null until the element has loaded and tried to start OpenGL.
            await Task.Delay(600);
        }
        catch (Exception exception)
        {
            //Nothing may escape an async void handler. A graphics API that refused to start is not a
            //  crash here either: the settle below collapses the canvas that did not start, and the
            //  view model is told it is on the processor.
            Debug.WriteLine($"SimpleCbxVideoPlayer: the GPU canvas could not be started - {exception.Message}");
        }

        DispatcherQueue?.TryEnqueue(SettleVideoSurface);
    }

    private void SettleVideoSurface()
    {
        var hasGpuCanvas = gpuCanvas?.IsGpuInitialized == true;

        if (gpuCanvas != null)
        {
            gpuCanvas.Visibility = hasGpuCanvas ? Visibility.Visible : Visibility.Collapsed;
        }

        CpuCanvas.Visibility = hasGpuCanvas ? Visibility.Collapsed : Visibility.Visible;

        if (!hasSettledVideoSurface)
        {
            hasSettledVideoSurface = true;
            ViewModel?.OnVideoSurfaceReady(hasGpuCanvas);
        }

        InvalidateVideoCanvas();
    }

    private void InvalidateVideoCanvas()
    {
        if (gpuCanvas is { Visibility: Visibility.Visible })
        {
            gpuCanvas.Invalidate();
            return;
        }

        CpuCanvas?.Invalidate();
    }

    /// <summary>Asks where to write a baked lookup table, and returns null when the person cancels.</summary>
    /// <remarks>
    /// No SuggestedStartLocation: the dialog opens where this person last was, and the application never
    /// proposes a folder of its own. The frame-buffer head has no dialog to show, so a bake there simply
    /// says so rather than writing somewhere nobody chose.
    /// </remarks>
    private Task<string> PickSaveCubePathAsync(string suggestedFileName)
    {
        //A SimpleCommand does not promise to run its handler on the user-interface thread, and a picker
        //  belongs to the window it is shown over - so the thread is made certain rather than assumed.
        TaskCompletionSource<string> chosen =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var enqueued = DispatcherQueue?.TryEnqueue(async () =>
        {
            try { chosen.TrySetResult(await ShowSavePickerAsync(suggestedFileName)); }
            catch (Exception exception) { chosen.TrySetException(exception); }
        });

        //No dispatcher means no window to show it over, which reads the same as declining to choose.
        if (enqueued != true) { chosen.TrySetResult(null); }

        return chosen.Task;
    }

    private static async Task<string> ShowSavePickerAsync(string suggestedFileName)
    {
        FileSavePicker picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = BakeLocations.LutFileExtension,
        };
        picker.FileTypeChoices.Add("Cube lookup table", new List<string> { BakeLocations.LutFileExtension });

        StorageFile file = await picker.PickSaveFileAsync();

        if (file == null) { return null; }

        //The picker creates an empty placeholder at a brand-new name. The bake is about to write over it
        //  anyway; clearing it means a bake that fails leaves nothing behind that looks like a result.
        RemoveEmptyPlaceholder(file.Path);

        return file.Path;
    }

    private static void RemoveEmptyPlaceholder(string path)
    {
        try
        {
            FileInfo info = new FileInfo(path);

            if (info.Exists && info.Length == 0) { info.Delete(); }
        }
        catch (Exception exception)
        {
            //Leaving it is harmless - the bake overwrites it either way.
            Debug.WriteLine($"SimpleCbxVideoPlayer: could not clear the placeholder - {exception.Message}");
        }
    }

    private void OnGpuPaintSurface(object sender, SkiaGLPaintSurfaceEventArgs args)
    {
        //The context is current for the length of this call, which is where the presenter wants it.
        ViewModel?.SetGraphicsContext(args.Context);
        args.Surface.Canvas.Clear(SKColors.Black);
        ViewModel?.DrawVideo(args.Surface.Canvas, new SKRect(0f, 0f, args.Info.Width, args.Info.Height));
    }

    private void OnCpuPaintSurface(object sender, SKPaintSurfaceEventArgs args)
    {
        args.Surface.Canvas.Clear(SKColors.Black);
        ViewModel?.DrawVideo(args.Surface.Canvas, new SKRect(0f, 0f, args.Info.Width, args.Info.Height));
    }
}
