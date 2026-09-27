using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using PolyHavenBrowser.ViewModels;
using System;
using Windows.Foundation;

namespace PolyHavenBrowser.Views;

public sealed partial class MainPage : Page
{
    private MainViewModel ViewModel => DataContext as MainViewModel;

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ICanvasInvalidator invalidator)
            {
                invalidator.InvalidateCanvas = () => DisplayCanvas?.Invalidate();
            }

            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            //(e.g. the "Vulkan rendering is not available on this platform." alert).
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
        };

        //The XAML creates the view model and nothing else owns it, so the page is what
        //  releases its painters, rendering engines and commands.
        Unloaded += (_, _) => (DataContext as IDisposable)?.Dispose();

        InitializeComponent();

        #region | Wire the display canvas to the view model |

        DisplayCanvas.PaintSurface += (_, e) => ViewModel?.PaintCanvas(e.Surface, e.Info);

        DisplayCanvas.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(DisplayCanvas);
            if (!point.Properties.IsLeftButtonPressed) { return; }

            var (x, y) = ToCanvasPixels(point.Position);
            if (ViewModel?.PointerPressed(x, y, point.Timestamp) != true) { return; }

            DisplayCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        };

        DisplayCanvas.PointerMoved += (_, e) =>
        {
            var point = e.GetCurrentPoint(DisplayCanvas);
            var (x, y) = ToCanvasPixels(point.Position);
            if (ViewModel?.PointerMoved(x, y, point.Timestamp) != true) { return; }

            //Handle the move so it doesn't bubble to the window manager (which would
            //  otherwise drag/manipulate the window instead of orbiting the scene).
            e.Handled = true;
        };

        DisplayCanvas.PointerReleased += (_, e) =>
        {
            ViewModel?.PointerReleased();
            DisplayCanvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        };

        DisplayCanvas.PointerCaptureLost += (_, _) => ViewModel?.PointerReleased();

        DisplayCanvas.PointerWheelChanged += (_, e) =>
        {
            var delta = e.GetCurrentPoint(DisplayCanvas).Properties.MouseWheelDelta;
            if (ViewModel?.PointerWheelChanged(delta) != true) { return; }

            e.Handled = true;
        };

        DisplayCanvas.SizeChanged += (_, _) => ViewModel?.RequestRender();

        #endregion
    }

    // Maps a pointer position (in view/DIP units) to the canvas's pixel space, so pointer
    // input stays aligned with the rendered pixels at any DPI and after any window resize -
    // the coordinate robustness the PainDiagram sample demonstrates.
    private (double X, double Y) ToCanvasPixels(Point position)
    {
        var canvasSize = DisplayCanvas.CanvasSize;
        var scaleX = DisplayCanvas.ActualWidth > 0 && canvasSize.Width > 0
            ? canvasSize.Width / DisplayCanvas.ActualWidth : 1.0;
        var scaleY = DisplayCanvas.ActualHeight > 0 && canvasSize.Height > 0
            ? canvasSize.Height / DisplayCanvas.ActualHeight : 1.0;
        return (position.X * scaleX, position.Y * scaleY);
    }
}
