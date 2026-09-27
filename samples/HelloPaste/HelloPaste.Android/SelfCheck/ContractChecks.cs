using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Profile;
using Windows.System.UserProfile;
using static HelloPaste.SelfCheck.Runner;
using Colors = Microsoft.UI.Colors;
using ToolkitElements = CodeBrix.Platform.UI.Toolkit.UIElementExtensions;

namespace HelloPaste.SelfCheck;

/// <summary>
/// The platform-contract checks (the AP1-B device checks, carried over): dispatcher, WinRT
/// data / globalization / imaging / analytics / display, theme, inert composition, Fluent
/// default styles, elevation, focus, geometry, text measure and fonts - all through public
/// CodeBrix.Platform APIs, on the pasted page that is on screen.
/// </summary>
internal static class ContractChecks
{
    internal static async Task RunAsync()
    {
        var queue = Queue;
        Report("dispatcher.timer", true, "DispatcherQueueTimer tick arrived");
        Report("dispatcher.thread-access", queue.HasThreadAccess, "HasThreadAccess on the UI thread");

        var order = new List<string>();
        var done = new TaskCompletionSource<bool>();
        queue.TryEnqueue(DispatcherQueuePriority.Low, () => order.Add("Low"));
        queue.TryEnqueue(DispatcherQueuePriority.Normal, () => order.Add("Normal"));
        queue.TryEnqueue(DispatcherQueuePriority.High, () => order.Add("High"));
        queue.TryEnqueue(DispatcherQueuePriority.Low, () => done.TrySetResult(true));
        await done.Task;
        Report("dispatcher.priorities", string.Join(",", order) == "High,Normal,Low", string.Join(",", order));

        var offThread = await Task.Run(() => queue.HasThreadAccess);
        Report("dispatcher.off-thread", !offThread, "HasThreadAccess from a pool thread = " + offThread);

        var data = ApplicationData.Current;
        var local = data.LocalFolder.Path;
        Report("applicationdata.local", local.EndsWith("/files", StringComparison.Ordinal), local);
        Report("applicationdata.temporary", data.TemporaryFolder.Path.EndsWith("/cache/Temp", StringComparison.Ordinal), data.TemporaryFolder.Path);
        Report("applicationdata.localcache", data.LocalCacheFolder.Path.EndsWith("/cache", StringComparison.Ordinal), data.LocalCacheFolder.Path);
        Report("applicationdata.roaming", data.RoamingFolder.Path.EndsWith("/files/Roaming", StringComparison.Ordinal), data.RoamingFolder.Path);
        data.LocalSettings.Values["selfcheck"] = 42;
        Report("applicationdata.settings", data.LocalSettings.Values["selfcheck"] is int v && v == 42, "LocalSettings round trip");

        var languages = GlobalizationPreferences.Languages;
        Report("globalization.languages", languages.Count > 0 && !languages[0].Contains('_'), string.Join(";", languages));

        await CheckImagingAsync();

        // AP1.9: the Android IDeviceFamilyPlatform (WPE1-5 B3); decision D2: "Android.<form>", the form = the window size class.
        Report("analyticsinfo.devicefamily", AnalyticsInfo.VersionInfo.DeviceFamily?.StartsWith("Android.", StringComparison.Ordinal) == true, AnalyticsInfo.VersionInfo.DeviceFamily);

        var display = DisplayInformation.GetForCurrentView();
        Report("displayinformation.scale", display.RawPixelsPerViewPixel > 0.5, "RawPixelsPerViewPixel=" + display.RawPixelsPerViewPixel + " LogicalDpi=" + display.LogicalDpi);

        Report("theme.requested", true, "Application.RequestedTheme=" + Application.Current.RequestedTheme);

        var page = PageChecks.FindPage();
        Report("window.page", page != null, page?.GetType().FullName ?? "no page");
        if (page != null)
        {
            CheckOnPage(page);
        }

        CheckGeometry();
        CheckFonts();
    }

    private static async Task CheckImagingAsync()
    {
        var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 4, 3, BitmapAlphaMode.Premultiplied);
        Report("graphicsimaging.create", bitmap.PixelWidth == 4 && bitmap.PixelHeight == 3 && bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8, bitmap.PixelWidth + "x" + bitmap.PixelHeight + " " + bitmap.BitmapPixelFormat);

        var copy = SoftwareBitmap.Copy(bitmap);
        Report("graphicsimaging.copy", copy.PixelWidth == 4 && copy.BitmapAlphaMode == BitmapAlphaMode.Premultiplied, copy.BitmapAlphaMode.ToString());

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        var pixels = Enumerable.Range(0, 4 * 3 * 4).Select(i => (byte)(i * 5)).ToArray();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, 4, 3, 96, 96, pixels);
        await encoder.FlushAsync();
        stream.Seek(0);
        var header = new byte[4];
        var read = stream.AsStreamForRead().Read(header, 0, 4);
        Report("graphicsimaging.encode-png", read == 4 && header[0] == 0x89 && header[1] == (byte)'P' && header[2] == (byte)'N' && header[3] == (byte)'G', "size=" + stream.Size);
    }

    private static void CheckOnPage(Page page)
    {
        // This self-check proves the inert composition platform on purpose, so the CBAND0003 warning (composition is
        // accepted and ignored on Android) is expected here and silenced for these lines only.
#pragma warning disable CBAND0003
        var visual = ElementCompositionPreview.GetElementVisual(page);
        var sprite = visual.Compositor.CreateSpriteVisual();
        sprite.Size = new System.Numerics.Vector2(10, 10);
        sprite.Brush = visual.Compositor.CreateColorBrush(Colors.Red);
#pragma warning restore CBAND0003
        Report("composition.inert", visual != null, "page visual " + visual.GetType().Name + ", sprite + color brush created");

        var measured = Descendants(page).OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text) && t.Visibility == Visibility.Visible).ToList();
        Report("text.measure", measured.Count > 0 && measured.All(t => t.ActualWidth > 0 && t.ActualHeight > 0),
            measured.Count + " TextBlocks: " + string.Join(" | ", measured.Take(6).Select(t => $"\"{t.Text}\" {t.ActualWidth:0.#}x{t.ActualHeight:0.#}")));

        var button = Descendants(page).OfType<Button>().FirstOrDefault(b => b.ActualWidth > 0);
        Report("xaml.button", button != null, button == null ? "no button" : $"{button.GetType().Name} {button.ActualWidth:0.#}x{button.ActualHeight:0.#}");
        // The Fluent default style resolved: it sets the Button's Template. A Button shown by its native widget (AP3a)
        // keeps that Template without expanding it (no visual children); a templated Button expands it.
        Report("resources.default-style", button != null && (button.Template != null || VisualTreeHelper.GetChildrenCount(button) > 0),
            "Button Template from the Fluent default style: " + (button?.Template != null ? "set" : "null")
            + ", children=" + (button == null ? 0 : VisualTreeHelper.GetChildrenCount(button)));

        if (button != null)
        {
            ToolkitElements.SetElevation(button, 4);
            Report("toolkit.elevation", true, "SetElevation(button, 4) accepted (stub)");
        }

        var focused = page.Focus(FocusState.Programmatic);
        Report("focus.programmatic", true, "Page.Focus returned " + focused);

        Report("xamlroot.size", page.XamlRoot != null && page.XamlRoot.Size.Width > 0,
            $"XamlRoot {page.XamlRoot?.Size.Width:0.#}x{page.XamlRoot?.Size.Height:0.#} scale={page.XamlRoot?.RasterizationScale}");
    }

    private static void CheckGeometry()
    {
        var rect = new RectangleGeometry { Rect = new Rect(1, 2, 30, 40) };
        Report("geometry.rectangle-bounds", rect.Bounds == new Rect(1, 2, 30, 40), rect.Bounds.ToString());

        var figure = new PathFigure { StartPoint = new Point(0, 50), IsClosed = false };
        figure.Segments.Add(new ArcSegment { Point = new Point(100, 50), Size = new Size(50, 50), SweepDirection = SweepDirection.Clockwise });
        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(figure);
        var path = new Microsoft.UI.Xaml.Shapes.Path { Data = pathGeometry, Stroke = new SolidColorBrush(Colors.Black), StrokeThickness = 0 };
        path.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = path.DesiredSize;
        Report("geometry.path-arc-measure", Math.Abs(size.Width - 100) < 1.5 && Math.Abs(size.Height - 50) < 1.5, $"Path(arc) desired {size.Width:0.##}x{size.Height:0.##} (expect 100x50)");

        var ellipse = new Ellipse { Width = 20, Height = 10 };
        ellipse.Measure(new Size(100, 100));
        Report("geometry.ellipse-measure", ellipse.DesiredSize.Width == 20 && ellipse.DesiredSize.Height == 10, ellipse.DesiredSize.ToString());
    }

    private static void CheckFonts()
    {
        const string sample = "Wide text 0123";
        var defaultFamily = FeatureConfiguration.Font.DefaultTextFontFamily;
        // Weight variants come from the font package's .ttf.manifest (Open Sans: its Bold face is clearly wider).
        const string openSansFile = "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf";
        var plain = Measure(new TextBlock { Text = sample, FontSize = 20, FontFamily = new FontFamily(openSansFile) });
        var bold = Measure(new TextBlock { Text = sample, FontSize = 20, FontFamily = new FontFamily(openSansFile), FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        Report("font.manifest-weights", plain.Width > 0 && bold.Width > plain.Width,
            $"Open Sans regular {plain.Width:0.##} bold {bold.Width:0.##} (the manifest picks OpenSans-Bold.ttf)");

        // The CodeBrix font rule on Android: a family NAME resolves to the app's default font file, never to a system font.
        var roboto = Measure(new TextBlock { Text = sample, FontSize = 20, FontFamily = new FontFamily(defaultFamily) });
        var named = Measure(new TextBlock { Text = sample, FontSize = 20, FontFamily = new FontFamily("Segoe UI") });
        var openSans = plain;
        Report("font.named-family-uses-app-default", Math.Abs(named.Width - roboto.Width) < 0.01 && Math.Abs(openSans.Width - roboto.Width) > 0.5,
            $"'Segoe UI' {named.Width:0.##} = app default file {roboto.Width:0.##}; Open Sans file {openSans.Width:0.##} (a different face)");

        var wrapped = new TextBlock { Text = "one two three four five six seven eight nine ten", FontSize = 16, TextWrapping = TextWrapping.Wrap };
        wrapped.Measure(new Size(80, double.PositiveInfinity));
        Report("text.wrap", wrapped.DesiredSize.Width <= 80 && wrapped.DesiredSize.Height > 40, $"wrapped {wrapped.DesiredSize.Width:0.##}x{wrapped.DesiredSize.Height:0.##}");

        var symbols = Measure(new TextBlock { FontFamily = new FontFamily(FeatureConfiguration.Font.SymbolsFont), Text = "", FontSize = 20 });
        Report("font.symbols-asset", symbols.Width > 0, $"symbols glyph {symbols.Width:0.##}x{symbols.Height:0.##}");
    }

    private static Size Measure(TextBlock textBlock)
    {
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return textBlock.DesiredSize;
    }
}
