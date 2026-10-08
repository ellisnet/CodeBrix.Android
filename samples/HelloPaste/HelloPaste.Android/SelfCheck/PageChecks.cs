using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using static HelloPaste.SelfCheck.Runner;
using ATextView = Android.Widget.TextView;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using JbuViewModel = JustBetweenUs.ViewModels.MainViewModel;
using PdfViewModel = PdfSideBySide.ViewModels.MainViewModel;

namespace HelloPaste.SelfCheck;

/// <summary>
/// The checks of the pasted page on screen: App.xaml resources, Frame navigation (a round
/// trip to the other pasted page and back), the startup dialog, bindings and commands
/// through the pasted view models, and the element handlers' native views (texts, glyphs,
/// Core-rect vs view-rect agreement for every TextBlock, native measure vs native draw).
/// </summary>
internal static class PageChecks
{
    private const string PlainText = "Hello from a pasted page on Android";
    private const string StartupDialogText = "This application is adapted from a sample provided by Paul Ainsworth.";

    internal static Frame FindFrame() => Activity?.XamlWindow?.Content as Frame;

    internal static Page FindPage() => FindFrame()?.Content as Page;

    internal static async Task RunAsync()
    {
        var isJbu = StartPages.Resolve(StartPages.Requested) == typeof(JustBetweenUs.Views.MainPage);
        var frame = FindFrame();
        var expected = StartPages.Resolve(StartPages.Requested);
        Report("frame.navigate", frame?.Content?.GetType() == expected, $"Frame.Content = {frame?.Content?.GetType().FullName} (requested '{StartPages.Requested}')");
        if (frame == null)
        {
            return;
        }

        CheckResources(FindPage(), isJbu);

        if (isJbu)
        {
            await CheckStartupDialogAsync("dialog.startup");
        }
        else
        {
            await Task.Delay(1000);
            Report("dialog.none", OpenDialogs().Count == 0, "no dialog at start (PdfSideBySide shows none)");
        }

        await CheckFrameRoundTripAsync(frame, expected);

        var page = FindPage();
        if (isJbu)
        {
            await CheckJustBetweenUsAsync(page);
            await CheckStarAnimationAsync(page);
        }
        else
        {
            CheckPdfSideBySide(page);
        }

        await SettleAsync(400);
        CheckNativeViews(page, isJbu);
    }

    private static void CheckResources(Page page, bool isJbu)
    {
        var resources = Application.Current.Resources;
        var openSans = resources.TryGetValue("OpenSansFont", out var o) ? o as FontFamily : null;
        var roboto = resources.TryGetValue("RobotoFont", out var r) ? r as FontFamily : null;
        Report("resources.app-fonts", openSans?.Source?.Contains("OpenSans.ttf") == true && roboto?.Source?.Contains("Roboto.ttf") == true,
            $"App.xaml OpenSansFont={openSans?.Source} RobotoFont={roboto?.Source}");

        var pageFont = page?.FontFamily?.Source;
        var expectedFont = isJbu ? "OpenSans.ttf" : "Roboto.ttf";
        Report("resources.static", pageFont?.Contains(expectedFont) == true, $"Page FontFamily (StaticResource) = {pageFont}");

        var background = page?.Background as SolidColorBrush;
        Report("resources.theme", background != null, $"Page Background (ThemeResource ApplicationPageBackgroundThemeBrush) = {background?.Color}");
    }

    private static async Task CheckStartupDialogAsync(string name)
    {
        var shown = await WaitForAsync(() => OpenDialogs().Count > 0, 6000);
        var dialog = OpenDialogs().FirstOrDefault();
        var text = dialog == null ? null : Descendants(dialog).OfType<TextBlock>().Select(t => t.Text)
            .Append((dialog.Content as TextBlock)?.Text)
            .FirstOrDefault(t => t == StartupDialogText);
        Report(name, shown && text != null, dialog == null ? "no ContentDialog opened" : $"ContentDialog \"{dialog.Title}\": {text}");
        if (dialog != null)
        {
            dialog.Hide();
            var hidden = await WaitForAsync(() => OpenDialogs().Count == 0, 3000);
            Report(name + ".hide", hidden, "ContentDialog.Hide() closed it");
        }
    }

    private static async Task CheckFrameRoundTripAsync(Frame frame, Type startType)
    {
        var otherType = StartPages.Resolve(StartPages.Other(StartPages.Requested));
        var navigated = frame.Navigate(otherType);
        await SettleAsync();
        Report("frame.navigate-other", navigated && frame.Content?.GetType() == otherType && frame.CanGoBack,
            $"Navigate({otherType.Name} of {otherType.Namespace}) -> {frame.Content?.GetType().FullName}, CanGoBack={frame.CanGoBack}");

        frame.GoBack();
        await SettleAsync();
        Report("frame.go-back", frame.Content?.GetType() == startType, $"GoBack -> {frame.Content?.GetType().FullName}, BackStackDepth={frame.BackStackDepth}");

        // A JustBetweenUs page shows its startup dialog every time it loads; close whatever is open.
        await Task.Delay(1500);
        foreach (var dialog in OpenDialogs())
        {
            dialog.Hide();
        }

        await WaitForAsync(() => OpenDialogs().Count == 0, 3000);
        await SettleAsync();
    }

    /// <summary>
    /// The star of the JustBetweenUs page: an AnimatedVisualPlayer (AutoPlay) whose LottieVisualSource names an
    /// embedded:// Bodymovin document of JustBetweenUs.Core; the Lottie add-in loads it and plays it.
    /// </summary>
    private static async Task CheckStarAnimationAsync(Page page)
    {
        var player = Descendants(page).OfType<AnimatedVisualPlayer>().FirstOrDefault();
        var loaded = player != null && await WaitForAsync(() => player.IsAnimatedVisualLoaded, 10000);
        Report("lottie.star", loaded && player.Duration > TimeSpan.Zero && player.IsPlaying,
            player == null
                ? "no AnimatedVisualPlayer on the page"
                : $"{player.Source?.GetType().Name} loaded={player.IsAnimatedVisualLoaded} duration={player.Duration.TotalMilliseconds:0} ms playing={player.IsPlaying}");
    }

    private static async Task CheckJustBetweenUsAsync(Page page)
    {
        var vm = page?.DataContext as JbuViewModel;
        Report("binding.datacontext", vm != null, $"Page.DataContext = {page?.DataContext?.GetType().FullName} (created by <Page.DataContext><vm:MainViewModel/>)");
        if (vm == null)
        {
            return;
        }

        var combo = Descendants(page).OfType<ComboBox>().FirstOrDefault();
        var selected = combo?.SelectedItem as JustBetweenUs.ViewModels.EncryptionMode;
        var shownText = combo == null ? null : Descendants(combo).OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrEmpty(t) && t == selected?.Description);
        Report("binding.combobox", combo != null && combo.Items.Count == 3 && shownText != null,
            $"ComboBox items={combo?.Items.Count} SelectedItem.Description (DisplayMemberPath) = \"{shownText}\"");

        await WaitForAsync(() => !string.IsNullOrEmpty(vm.EncryptionKey), 3000);
        var textBoxes = Descendants(page).OfType<TextBox>().Where(t => t.TemplatedParent == null).ToList();
        var keyBox = textBoxes.FirstOrDefault(t => t.Width == 180);
        Report("binding.key", keyBox != null && !string.IsNullOrEmpty(vm.EncryptionKey) && keyBox.Text == vm.EncryptionKey,
            $"Encryption key TextBox.Text = \"{keyBox?.Text}\" (view model: \"{vm.EncryptionKey}\", read from the Encryption library's embedded resource)");

        var label = Descendants(page).OfType<TextBlock>().FirstOrDefault(t => t.Text == "Encryption Key:");
        Report("xaml.inline-text", label != null, "TextBlock inline content \"Encryption Key:\"");

        var entered = textBoxes.FirstOrDefault(t => t.AcceptsReturn && !t.IsReadOnly);
        var processed = textBoxes.FirstOrDefault(t => t.AcceptsReturn && t.IsReadOnly);
        vm.EnteredText = PlainText;
        await SettleAsync();
        Report("binding.twoway", entered?.Text == PlainText, $"view model EnteredText -> TextBox.Text = \"{entered?.Text}\"");

        var canEncrypt = vm.EncryptCommand.CanExecute(null);
        var encryptButton = Descendants(page).OfType<Button>().FirstOrDefault(b => b.Command == vm.EncryptCommand);
        await SettleAsync();
        Report("command.canexecute", canEncrypt && encryptButton?.IsEnabled == true, $"EncryptCommand.CanExecute={canEncrypt}, Encrypt button IsEnabled={encryptButton?.IsEnabled}");

        vm.EncryptCommand.Execute(null);
        var encrypted = await WaitForAsync(() => !string.IsNullOrEmpty(vm.ProcessedText), 8000);
        var cipher = vm.ProcessedText;
        await SettleAsync();
        Report("command.encrypt", encrypted && processed?.Text == cipher && cipher != PlainText,
            $"AES (CodeBrix.Cryptography) -> ProcessedText ({cipher?.Length} chars) shown in the read-only TextBox: {processed?.Text == cipher}");

        vm.EnteredText = cipher;
        vm.DecryptCommand.Execute(null);
        var decrypted = await WaitForAsync(() => vm.ProcessedText == PlainText, 8000);
        Report("command.decrypt", decrypted, $"decrypt round trip -> \"{vm.ProcessedText}\"");

        // The state the screen captures show: the plain text above, its encryption below.
        vm.EnteredText = PlainText;
        vm.ProcessedText = cipher;
        await SettleAsync();
    }

    private static void CheckPdfSideBySide(Page page)
    {
        var vm = page?.DataContext as PdfViewModel;
        Report("binding.datacontext", vm != null, $"Page.DataContext = {page?.DataContext?.GetType().FullName}");
        if (vm == null)
        {
            return;
        }

        var texts = Descendants(page).OfType<TextBlock>().Where(t => t.Visibility == Visibility.Visible).Select(t => t.Text).ToList();
        var buttons = Descendants(page).OfType<Button>().ToList();
        var browse = buttons.Select(b => b.Content as string).Where(c => c != null && c.StartsWith("Document", StringComparison.Ordinal)).ToList();
        Report("binding.browse-labels", browse.Contains("Document 1…") && browse.Contains("Document 2…"),
            "Button.Content {Binding BrowseLabel} = " + string.Join(", ", browse.Select(b => $"\"{b}\"")));
        Report("binding.placeholder-text", texts.Count(t => t == "No document selected") == 2, "TextBlock {Binding FileName} x2 = \"No document selected\"");
        Report("binding.zoom-label", texts.Contains("100%"), "TextBlock {Binding ZoomLabel} = \"100%\"");

        var images = Descendants(page).OfType<Image>().ToList();
        Report("xaml.images", images.Count == 2 && images.All(i => !string.IsNullOrEmpty(i.Name)), "Image elements: " + string.Join(", ", images.Select(i => i.Name)));

        var icons = Descendants(page).OfType<FontIcon>().Where(i => i.TemplatedParent == null).ToList();
        Report("xaml.fonticons", icons.Count >= 10 && icons.All(i => !string.IsNullOrEmpty(i.Glyph)), $"{icons.Count} FontIcons with glyphs");

        var next = buttons.FirstOrDefault(b => b.Command == vm.NextPageCommand);
        Report("command.canexecute", next != null && !vm.NextPageCommand.CanExecute(null) && !next.IsEnabled,
            $"NextPageCommand.CanExecute={vm.NextPageCommand.CanExecute(null)} (no document), its Button IsEnabled={next?.IsEnabled}");

        Report("bridge.file-picker", ((PdfSideBySide.Services.IPdfFileBridge)vm).PickPdfPathAsync != null, "the page gave the view model its file-picker bridge (DataContextChanged)");
    }

    private static void CheckNativeViews(Page page, bool isJbu)
    {
        var layer = Activity?.RootLayout?.ContentLayer;
        var views = new List<AView>();
        Collect(layer, views);
        var textViews = views.OfType<ATextView>().Where(v => v.Visibility == Android.Views.ViewStates.Visible && IsShown(v)).ToList();
        var texts = textViews.Select(v => v.Text).ToList();
        var elements = Descendants(page).OfType<FrameworkElement>().Where(e => e.Visibility == Visibility.Visible).ToList();
        Info($"native views: {views.Count} leaf views ({textViews.Count} text views) for {elements.Count} visible elements");

        // The element handlers show every TextBlock natively (TextBox values come with the AP3a TextBox handler).
        var expectedTexts = isJbu
            ? new[] { "Encryption Key:", "AES Standard Encryption (Secure)", "Encrypt", "Decrypt", "Copy to Clipboard" }
            : new[] { "Document 1…", "Document 2…", "No document selected", "100%" };
        var missing = expectedTexts.Where(t => !texts.Contains(t)).ToList();
        Report("handlers.texts", missing.Count == 0,
            missing.Count == 0 ? "native TextViews show: " + string.Join(" | ", expectedTexts) : "missing: " + string.Join(" | ", missing));

        if (!isJbu)
        {
            // Fluent icon glyphs (private-use code points): FontIcon's inner TextBlock, drawn by its TextBlock handler.
            var glyphs = texts.Count(t => t.Length == 1 && t[0] >= '\uE000' && t[0] <= '\uF8FF');
            Report("handlers.glyphs", glyphs >= 10, $"{glyphs} FontIcon glyphs shown by native text views");

            var images = Descendants(page).OfType<Image>().ToList();
            Report("handlers.empty-images", images.Count == 2 && images.All(i => i.ActualWidth == 0),
                "the two Image elements are laid out at 0x0 until a document is open");
        }

        // Core rect vs native view rect, for EVERY TextBlock that shows text (layout replay, within 1 px).
        var density = page.XamlRoot.RasterizationScale;
        var compared = 0;
        var off = new List<string>();
        foreach (var block in Descendants(page).OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text) && t.Visibility == Visibility.Visible && t.RenderSize.Width > 0))
        {
            var view = textViews.FirstOrDefault(v => v.Text == block.Text && !Used(v));
            if (view == null)
            {
                continue;
            }

            MarkUsed(view);
            compared++;
            var origin = block.TransformToVisual(null).TransformPoint(new Point(0, 0));
            var core = new Rect(origin.X * density, origin.Y * density, block.RenderSize.Width * density, block.RenderSize.Height * density);
            var (left, top) = WindowPosition(view, layer);
            if (Math.Abs(left - core.X) > 1 || Math.Abs(top - core.Y) > 1 || Math.Abs(view.Width - core.Width) > 1 || Math.Abs(view.Height - core.Height) > 1)
            {
                off.Add($"\"{block.Text}\" Core {core.X:0.#},{core.Y:0.#} {core.Width:0.#}x{core.Height:0.#} vs view {left},{top} {view.Width}x{view.Height}");
            }
        }

        _used.Clear();
        Report("handlers.geometry", compared >= expectedTexts.Length && off.Count == 0,
            off.Count == 0 ? $"{compared} TextBlocks: native view rect = Core rect (within 1 px)" : string.Join(" | ", off.Take(4)));

        // Native measure vs native draw: the text must fit the box Core gave it.
        var overflowing = textViews.Where(v => v.Layout != null && v.Layout.Height > v.Height + 1 && v.MaxLines > 1).Select(v => $"\"{v.Text}\" {v.Layout.Height}>{v.Height}").ToList();
        var singleLineWrapped = textViews.Where(v => v.Layout != null && v.MaxLines == 1 && v.Layout.LineCount > 1).Select(v => v.Text).ToList();
        Report("handlers.measure-agrees", overflowing.Count == 0 && singleLineWrapped.Count == 0,
            overflowing.Count + singleLineWrapped.Count == 0
                ? $"all {textViews.Count} native text layouts fit their Core boxes"
                : "overflow: " + string.Join(", ", overflowing.Concat(singleLineWrapped)));
    }

    private static readonly HashSet<AView> _used = new();

    private static bool Used(AView view) => _used.Contains(view);

    private static void MarkUsed(AView view) => _used.Add(view);

    private static bool IsShown(AView view)
    {
        for (var v = view; v != null; v = v.Parent as AView)
        {
            if (v.Visibility != Android.Views.ViewStates.Visible)
            {
                return false;
            }
        }

        return true;
    }

    private static (int Left, int Top) WindowPosition(AView view, AView root)
    {
        int left = 0, top = 0;
        for (var v = view; v != null && v != root; v = v.Parent as AView)
        {
            left += v.Left;
            top += v.Top;
        }

        return (left, top);
    }

    private static void Collect(AViewGroup group, List<AView> views)
    {
        if (group == null)
        {
            return;
        }

        for (var i = 0; i < group.ChildCount; i++)
        {
            var child = group.GetChildAt(i);
            if (child is AViewGroup nested && child is not ATextView)
            {
                Collect(nested, views);
            }
            else if (child != null)
            {
                views.Add(child);
            }
        }
    }

    private static List<ContentDialog> OpenDialogs()
    {
        var root = FindPage()?.XamlRoot ?? Activity?.XamlWindow?.Content?.XamlRoot;
        if (root == null)
        {
            return new List<ContentDialog>();
        }

        // Core-presented dialogs live in open popups; a text dialog shown as a Material dialog (tier 2) is in the
        // native overlay registry instead.
        return VisualTreeHelper.GetOpenPopupsForXamlRoot(root)
            .SelectMany(p => Descendants(p.Child))
            .OfType<ContentDialog>()
            .Concat(PlatformOverlayContentDialogs())
            .Distinct()
            .ToList();
    }

    // The ContentDialogs CodeBrix.Android shows as Material dialogs. Its overlay registry
    // (CodeBrix.Android.UI.Overlay.PlatformOverlays) is internal, so the self-check reads it by
    // reflection; the DynamicDependency keeps the property in the trimmed Release build.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, "CodeBrix.Android.UI.Overlay.PlatformOverlays", "CodeBrix.Android.UI")]
    private static IEnumerable<ContentDialog> PlatformOverlayContentDialogs()
    {
        var registry = Type.GetType("CodeBrix.Android.UI.Overlay.PlatformOverlays, CodeBrix.Android.UI", throwOnError: true);
        var property = registry.GetProperty("ContentDialogs", BindingFlags.Public | BindingFlags.Static);
        return (IEnumerable<ContentDialog>)property.GetValue(null);
    }
}
