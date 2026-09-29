#nullable disable

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.AdvancedTextEdit.Rendering;
using CodeBrix.Platform.UI.TerminalView;
using CodeBrix.Platform.UI.TerminalView.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using AView = Android.Views.View;
using AWindowInsets = Android.Views.WindowInsets;
using Editor = CodeBrix.Platform.UI.AdvancedTextEdit.AdvancedTextEdit;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// [AP8-S item K] The steps of the Android-only "The soft keyboard on Android" feature (AndroidFeatures/AndroidSoftInput):
/// the MAUI-style soft-input adjust modes of CodeBrixApplication.SoftInputAdjust. Pan (the default): the window pans so the
/// focused field is above the keyboard and the page keeps its size. Resize: Core lays the page out above the keyboard (the
/// root's content bottom occlusion inset). Positions are measured on the SCREEN (native views' GetLocationOnScreen, which
/// includes the window's pan; the keyboard's top edge from the window's IME inset), never from the captured pixels.
/// </summary>
[Binding]
public sealed class SoftInputSteps
{
    private const string TerminalFont = "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf";
    private const float TerminalFontSize = 24f;
    private static int _rootTopBefore;
    private static double _contentHeightBefore;
    private static int _rowsBefore;
    private static int _linesFed;
    private static bool _modeChanged;

    /// <summary>Sets the app-level soft-input adjust mode (as an application does, in the running app).</summary>
    [Given("the application's soft-input mode is {string}")]
    [When("the application switches its soft-input mode to {string}")]
    public async Task Given_the_mode(string mode)
    {
        _modeChanged = true;
        var adjust = Enum.Parse<SoftInputAdjust>(mode);
        await TestTargetFixture.RunOnUIThreadAsync(() => App.SoftInputAdjust = adjust).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>A TextBox at the bottom edge of the panel, below a Border that fills the rest.</summary>
    [Given("the application shows a TextBox named {string} at the bottom of the panel, below a Border named {string} painted {string}")]
    public async Task Given_a_low_textbox(string box, string ground, string color)
    {
        ElementRegistry.Clear();
        Grid grid = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var border = new Border { Name = ground, Background = new SolidColorBrush(CodeBrix.Platform.UI.Core.UIReqs.Support.Colors.Parse(color)) };
            var entry = new TextBox { Name = box, Width = 300, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(16, 8, 16, 16) };
            Grid.SetRow(entry, 1);
            grid.Children.Add(border);
            grid.Children.Add(entry);
            ElementRegistry.Register(box, entry);
            ElementRegistry.Register(ground, border);
            ElementRegistry.Register("page content", grid);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(grid).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
        await RememberAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// [AP8-S batch 2] A Button that collapses itself when clicked (a viewer's Back button) above a TextBox: Core then
    /// moves the focus on to the TextBox with the Button's pointer focus state (WinUI's rule).
    /// </summary>
    [Given("the application shows a Button named {string} that collapses itself when clicked, above a TextBox named {string}")]
    public async Task Given_a_collapsing_button(string button, string box)
    {
        ElementRegistry.Clear();
        StackPanel panel = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            panel = new StackPanel { Spacing = 16, Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Left };
            var back = new Button { Name = button, Content = "Back", Width = 200, Height = 64 };
            back.Click += (_, _) => back.Visibility = Visibility.Collapsed;
            var entry = new TextBox { Name = box, Width = 300 };
            panel.Children.Add(back);
            panel.Children.Add(entry);
            ElementRegistry.Register(button, back);
            ElementRegistry.Register(box, entry);
            ElementRegistry.Register("page content", panel);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(panel).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
        await RememberAsync().ConfigureAwait(false);
    }

    /// <summary>[AP8-S batch 2] Core's focused element is the named one (within two seconds).</summary>
    [Then("Core's focus is on {string}")]
    public async Task Then_core_focus_on(string name)
    {
        var focused = false;
        for (var attempt = 0; attempt < 20 && !focused; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            await TestTargetFixture.RunOnUIThreadAsync(() =>
            {
                var element = (UIElement)ElementRegistry.Resolve(name);
                focused = element.XamlRoot is { } root
                    && ReferenceEquals(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root), element);
            }).ConfigureAwait(false);
        }

        focused.Should().BeTrue("Core must have moved the focus on to \"{0}\"", name);
    }

    /// <summary>A TextBox at the top edge of the panel, above a Border that fills the rest of it.</summary>
    [Given("the application shows a TextBox named {string} at the top of the panel, above a Border named {string} painted {string}")]
    public async Task Given_a_high_textbox(string box, string ground, string color)
    {
        ElementRegistry.Clear();
        Grid grid = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var entry = new TextBox { Name = box, Width = 300, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(16, 16, 16, 8) };
            var border = new Border { Name = ground, Background = new SolidColorBrush(CodeBrix.Platform.UI.Core.UIReqs.Support.Colors.Parse(color)) };
            Grid.SetRow(border, 1);
            grid.Children.Add(entry);
            grid.Children.Add(border);
            ElementRegistry.Register(box, entry);
            ElementRegistry.Register(ground, border);
            ElementRegistry.Register("page content", grid);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(grid).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
        await RememberAsync().ConfigureAwait(false);
    }

    /// <summary>A terminal (Navy ground, Yellow text, 24 DIPs) that fills the whole panel, fed numbered lines to its bottom row, its cursor hidden.</summary>
    [Given("the application shows a TerminalView named {string} that fills the panel, fed {int} numbered lines")]
    public Task Given_a_full_terminal(string name, int lines) => ShowFullTerminalAsync(name, lines, showCursor: false);

    /// <summary>
    /// [AP8-S batch 4] The same terminal with its cursor SHOWN (after "line n" on the bottom row): the soft keyboard's focus
    /// view is laid on the cursor cell (the TerminalView caret seam). No frame of it is captured: the cursor blinks.
    /// </summary>
    [Given("the application shows a TerminalView named {string} that fills the panel, fed {int} numbered lines, its cursor shown")]
    public Task Given_a_full_terminal_with_cursor(string name, int lines) => ShowFullTerminalAsync(name, lines, showCursor: true);

    private static async Task ShowFullTerminalAsync(string name, int lines, bool showCursor)
    {
        ElementRegistry.Clear();
        TerminalControl terminal = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            CellMetrics.Measure(TerminalFont, TerminalFontSize);
            terminal = new TerminalControl
            {
                Name = name,
                BackgroundColor = SKColors.Navy,
                ForegroundColor = SKColors.Yellow,
                TerminalFontSize = TerminalFontSize,
            };
            ElementRegistry.Register(name, terminal);
            ElementRegistry.Register("page content", terminal);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(terminal).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
        var text = new StringBuilder();
        for (var i = 1; i <= lines; i++)
        {
            text.Append(i == 1 ? string.Empty : "\r\n").Append("line ").Append(i);
        }

        // The cursor blinks twice a second while the terminal has the keyboard (the copied TerminalView group hides it for the
        // same reason): hidden (DECTCEM off) so a captured frame never depends on the blink phase.
        if (!showCursor)
        {
            text.Append("\u001b[?25l");
        }

        await TestTargetFixture.RunOnUIThreadAsync(() => terminal.Feed(text.ToString())).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
        _rowsBefore = await OnUIThreadAsync(() => terminal.Rows).ConfigureAwait(false);
        _linesFed = lines;
        await RememberAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// [AP8-S item L] An AdvancedTextEdit (Roboto Mono 20, line numbers) that fills the whole panel, holding "line 1" ..
    /// "line n": its lower lines are where the soft keyboard comes up.
    /// </summary>
    [Given("the application shows an AdvancedTextEdit named {string} that fills the panel, holding {int} numbered lines")]
    public async Task Given_a_full_editor(string name, int lines)
    {
        ElementRegistry.Clear();
        Editor editor = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            editor = new Editor
            {
                Name = name,
                FontFamily = new FontFamily(TerminalFont),
                FontSize = 20,
                ShowLineNumbers = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            editor.Text = string.Join("\n", Enumerable.Range(1, lines).Select(i => "line " + i));
            ElementRegistry.Register(name, editor);
            ElementRegistry.Register("page content", editor);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(editor).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
        await RememberAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// [AP8-S item L] The page's content is replaced by an empty panel (the focused control leaves the tree: its soft-keyboard
    /// session closes and the keyboard goes); waits until the keyboard is hidden and the window is back where it was.
    /// </summary>
    [When("the page is emptied")]
    public async Task When_page_emptied()
    {
        Grid empty = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => empty = new Grid()).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(empty).ConfigureAwait(false);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var (open, keyboardTop) = await OnUIThreadAsync(() => (CoreTextInputController.Current?.View != null, KeyboardTopOnScreen())).ConfigureAwait(false);
            if (!open && keyboardTop == 0)
            {
                break;
            }
        }

        await Settle().ConfigureAwait(false);
    }

    /// <summary>
    /// [AP8-S batch 4] Core's focus report for the editor's text area, delivered to the controller as Core delivers it
    /// (ITextInputFocusNotificationsSingleton.OnTextControlFocused) - here for a text area that has left the page.
    /// </summary>
    [When("Core reports that the text area of the AdvancedTextEdit {string} has the focus")]
    public async Task When_core_reports_text_area_focus(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            CoreTextInputController.Current.OnTextControlFocused(((Editor)ElementRegistry.Resolve(name)).TextArea)).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>[AP8-S item L] A real finger taps (column 3 of) the last line the editor shows whole - near its bottom edge.</summary>
    [When("a real finger taps the last line the AdvancedTextEdit {string} shows")]
    public async Task When_finger_taps_last_line(string name)
    {
        var line = await OnUIThreadAsync(() =>
        {
            var view = ((Editor)ElementRegistry.Resolve(name)).TextArea.TextView;
            view.EnsureVisualLines();
            var bottom = view.VerticalOffset + view.ActualHeight;
            return view.VisualLines.Where(v => v.VisualTop + v.Height <= bottom).Max(v => v.FirstDocumentLine.LineNumber);
        }).ConfigureAwait(false);
        await new AdvancedTextEditAndroidSteps().When_finger_taps_at(name, line, 3).ConfigureAwait(false);
    }

    /// <summary>
    /// [AP8-S item L] The soft-keyboard session's focus view (the view that holds the Android focus, whose rectangle
    /// Android's pan brings into view) is focused and lies on the editor's caret on the screen (1 px).
    /// </summary>
    [Then("the soft keyboard's focus view is on the caret of the AdvancedTextEdit {string}")]
    public async Task Then_focus_view_on_caret(string name)
    {
        await Settle().ConfigureAwait(false);
        var (focused, viewTop, viewHeight, viewLeft, caret) = await OnUIThreadAsync(() =>
        {
            var view = CoreTextInputController.Current?.View ?? throw new InvalidOperationException("No soft-keyboard session is open.");
            var location = new int[2];
            view.GetLocationOnScreen(location);
            return (view.IsFocused, location[1], view.Height, location[0], CaretOnScreen(name));
        }).ConfigureAwait(false);
        focused.Should().BeTrue("the session's focus view must hold the Android focus");
        Math.Abs(viewLeft - caret.Left).Should().BeLessThanOrEqualTo(1, "the focus view (x {0}) must be on the caret (x {1})", viewLeft, caret.Left);
        Math.Abs(viewTop - caret.Top).Should().BeLessThanOrEqualTo(1, "the focus view (y {0}) must be on the caret (y {1})", viewTop, caret.Top);
        Math.Abs(viewHeight - (caret.Bottom - caret.Top)).Should().BeLessThanOrEqualTo(1, "the focus view must be as tall as the caret");
    }

    /// <summary>[AP8-S item L] The editor's caret ends at or above the soft keyboard's top edge on the screen (polled for 3 s).</summary>
    [Then("the caret of the AdvancedTextEdit {string} is above the soft keyboard")]
    public async Task Then_editor_caret_above(string name)
    {
        var (bottom, keyboardTop) = (0, 0);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            (bottom, keyboardTop) = await OnUIThreadAsync(() => (CaretOnScreen(name).Bottom, KeyboardTopOnScreen())).ConfigureAwait(false);
            if (keyboardTop > 0 && bottom <= keyboardTop)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        keyboardTop.Should().BeGreaterThan(0, "the soft keyboard must be up");
        bottom.Should().BeLessThanOrEqualTo(keyboardTop, "the caret of \"{0}\" (bottom {1} px on screen) must be above the keyboard (top {2} px)", name, bottom, keyboardTop);
    }

    /// <summary>
    /// [AP8-S batch 4] The soft-keyboard session's focus view holds the Android focus and lies on the terminal's cursor cell
    /// on the screen (2 px): the cell after the last line's text ("line n": column = its length) on the bottom row.
    /// </summary>
    [Then("the soft keyboard's focus view is on the cursor cell of the TerminalView {string}")]
    public async Task Then_focus_view_on_cursor_cell(string name)
    {
        await Settle().ConfigureAwait(false);
        var (focused, viewLeft, viewTop, cellLeft, cellTop) = await OnUIThreadAsync(() =>
        {
            var view = CoreTextInputController.Current?.View ?? throw new InvalidOperationException("No soft-keyboard session is open.");
            var location = new int[2];
            view.GetLocationOnScreen(location);
            var terminal = (TerminalControl)ElementRegistry.Resolve(name);
            var cell = CellMetrics.Measure(TerminalFont, TerminalFontSize);
            var lastLine = terminal.Rows < 1 ? 0 : terminal.Rows - 1;
            var column = ("line " + _linesFed).Length;
            var cellOnPage = terminal.TransformToVisual(null).TransformPoint(new Point(column * cell.Width, lastLine * cell.Height));
            var density = AppHost.Activity.Resources.DisplayMetrics.Density;
            var root = new int[2];
            ((CodeBrixActivity)AppHost.Activity).RootLayout.GetLocationOnScreen(root);
            return (view.IsFocused, location[0], location[1], root[0] + (int)Math.Round(cellOnPage.X * density), root[1] + (int)Math.Round(cellOnPage.Y * density));
        }).ConfigureAwait(false);
        focused.Should().BeTrue("the session's focus view must hold the Android focus");
        Math.Abs(viewLeft - cellLeft).Should().BeLessThanOrEqualTo(2, "the focus view (x {0}) must be on the cursor cell (x {1})", viewLeft, cellLeft);
        Math.Abs(viewTop - cellTop).Should().BeLessThanOrEqualTo(2, "the focus view (y {0}) must be on the cursor cell (y {1})", viewTop, cellTop);
    }

    /// <summary>
    /// [AP8-S item L] The terminal's cursor row - its last row, after more lines than it has rows - ends at or above the soft
    /// keyboard's top edge on the screen (polled for 3 s).
    /// </summary>
    [Then("the cursor row of the TerminalView {string} is above the soft keyboard")]
    public async Task Then_cursor_row_above(string name)
    {
        var (bottom, keyboardTop) = (0, 0);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            (bottom, keyboardTop) = await OnUIThreadAsync(() =>
            {
                var terminal = (TerminalControl)ElementRegistry.Resolve(name);
                var cell = CellMetrics.Measure(TerminalFont, TerminalFontSize);
                var rowBottom = terminal.TransformToVisual(null).TransformPoint(new Point(0, terminal.Rows * cell.Height)).Y;
                var density = AppHost.Activity.Resources.DisplayMetrics.Density;
                return (RootTopOnScreen() + (int)Math.Ceiling(rowBottom * density), KeyboardTopOnScreen());
            }).ConfigureAwait(false);
            if (keyboardTop > 0 && bottom <= keyboardTop)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        keyboardTop.Should().BeGreaterThan(0, "the soft keyboard must be up");
        bottom.Should().BeLessThanOrEqualTo(keyboardTop, "the cursor row of \"{0}\" (bottom {1} px on screen) must be above the keyboard (top {2} px)", name, bottom, keyboardTop);
    }

    // The editor's caret on the SCREEN, in pixels (left, top, bottom): the Core's caret rectangle on the text view, through
    // Core's layout, plus the root layout's place on the screen (which includes the window's pan).
    private static (int Left, int Top, int Bottom) CaretOnScreen(string name)
    {
        var area = ((Editor)ElementRegistry.Resolve(name)).TextArea;
        var view = area.TextView;
        var caret = area.Caret.CalculateCaretRectangle();
        var local = new Rect(caret.X - view.HorizontalOffset, caret.Y - view.VerticalOffset, Math.Max(caret.Width, 0), caret.Height);
        var box = view.TransformToVisual(null).TransformBounds(local);
        var density = view.XamlRoot?.RasterizationScale ?? AppHost.Activity.Resources.DisplayMetrics.Density;
        var root = new int[2];
        ((CodeBrixActivity)AppHost.Activity).RootLayout.GetLocationOnScreen(root);
        return (root[0] + (int)Math.Floor(box.X * density), root[1] + (int)Math.Floor(box.Y * density), root[1] + (int)Math.Ceiling(box.Bottom * density));
    }

    /// <summary>The window's adjust bits (SOFT_INPUT_MASK_ADJUST) are the named mode's.</summary>
    [Then("the window's soft-input adjust mode is {string}")]
    public async Task Then_the_adjust_mode_is(string mode)
    {
        var bits = await OnUIThreadAsync(() => (int)AppHost.Activity.Window.Attributes.SoftInputMode & 0xF0).ConfigureAwait(false);
        var expected = mode switch
        {
            "Pan" => 0x20,
            "Resize" => 0x10,
            "Unspecified" => 0x00,
            _ => throw new ArgumentException($"Unknown mode \"{mode}\"."),
        };
        bits.Should().Be(expected, "the window's adjust bits must be {0}'s", mode);
    }

    /// <summary>The window is panned up (the content is drawn higher on the screen than before the keyboard).</summary>
    [Then("the window is panned up")]
    public async Task Then_panned_up()
    {
        var top = await OnUIThreadAsync(RootTopOnScreen).ConfigureAwait(false);
        top.Should().BeLessThan(_rootTopBefore, "the window must pan up for the keyboard (content top {0} px, was {1} px)", top, _rootTopBefore);
    }

    /// <summary>The window is not panned (the content is where it was before the keyboard).</summary>
    [Then("the window is not panned")]
    public async Task Then_not_panned()
    {
        var top = await OnUIThreadAsync(RootTopOnScreen).ConfigureAwait(false);
        top.Should().Be(_rootTopBefore, "the window must not pan in this mode");
    }

    /// <summary>
    /// The text field's caret line (the focused EditText's focused rectangle - what Android's pan brings into view) ends at
    /// or above the soft keyboard's top edge on the screen; polled for up to three seconds (the pan follows the keyboard).
    /// </summary>
    [Then("the caret line of {string} is above the soft keyboard")]
    public async Task Then_caret_above_the_keyboard(string name)
    {
        var (bottom, keyboardTop) = (0, 0);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            (bottom, keyboardTop) = await OnUIThreadAsync(() =>
            {
                var edit = FindEditText(NativeView(name)) ?? throw new InvalidOperationException($"\"{name}\" has no EditText.");
                var rect = new global::Android.Graphics.Rect();
                edit.GetFocusedRect(rect);
                var location = new int[2];
                edit.GetLocationOnScreen(location);
                return (location[1] + rect.Bottom - edit.ScrollY, KeyboardTopOnScreen());
            }).ConfigureAwait(false);
            if (keyboardTop > 0 && bottom <= keyboardTop)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        keyboardTop.Should().BeGreaterThan(0, "the soft keyboard must be up");
        bottom.Should().BeLessThanOrEqualTo(keyboardTop, "the caret line of \"{0}\" (bottom {1} px on screen) must be above the keyboard (top {2} px)", name, bottom, keyboardTop);
    }

    /// <summary>The element's native view ends at or above the soft keyboard's top edge on the screen.</summary>
    [Then("{string} is above the soft keyboard")]
    public async Task Then_above_the_keyboard(string name)
    {
        var (bottom, keyboardTop) = await OnUIThreadAsync(() =>
        {
            var view = NativeView(name);
            var location = new int[2];
            view.GetLocationOnScreen(location);
            return (location[1] + view.Height, KeyboardTopOnScreen());
        }).ConfigureAwait(false);
        keyboardTop.Should().BeGreaterThan(0, "the soft keyboard must be up");
        bottom.Should().BeLessThanOrEqualTo(keyboardTop, "\"{0}\" (bottom {1} px on screen) must be above the keyboard (top {2} px)", name, bottom, keyboardTop);
    }

    /// <summary>
    /// The layout-replay audit (every named element's native view where Core laid it out, 1 px) holds - with a panned window
    /// the audit takes the pan out (GeometryAudit), so this fences that too.
    /// </summary>
    [Then("the native views are where Core laid them out")]
    public Task Then_layout_replay_holds() => GeometryAudit.VerifyAsync();

    /// <summary>The page keeps its full height: Core withholds nothing, the page content's height is unchanged.</summary>
    [Then("the page keeps its full height")]
    public async Task Then_full_height()
    {
        var (inset, height) = await OnUIThreadAsync(() => (Wrapper.KeyboardOcclusionInsetDips, ((FrameworkElement)ElementRegistry.Resolve("page content")).ActualHeight)).ConfigureAwait(false);
        inset.Should().Be(0, "nothing is withheld from the page in this mode");
        height.Should().Be(_contentHeightBefore, "the page content must keep its height");
    }

    /// <summary>The element's bottom edge (Core's layout, on the screen) lies on the soft keyboard's top edge (1 px).</summary>
    [Then("the bottom edge of {string} is on the soft keyboard's top edge")]
    public async Task Then_bottom_on_keyboard(string name)
    {
        var (bottom, keyboardTop, inset, keyboard) = await OnUIThreadAsync(() =>
        {
            var element = (FrameworkElement)ElementRegistry.Resolve(name);
            var density = AppHost.Activity.Resources.DisplayMetrics.Density;
            var corner = element.TransformToVisual(null).TransformPoint(new Point(0, element.ActualHeight));
            var bottomPx = RootTopOnScreen() + (int)Math.Round(corner.Y * density);
            return (bottomPx, KeyboardTopOnScreen(), Wrapper.KeyboardOcclusionInsetDips, Wrapper.KeyboardDips);
        }).ConfigureAwait(false);
        keyboardTop.Should().BeGreaterThan(0, "the soft keyboard must be up");
        inset.Should().Be(keyboard, "Core must withhold the keyboard's height ({0} DIPs) from the page", keyboard);
        Math.Abs(bottom - keyboardTop).Should().BeLessThanOrEqualTo(1, "the bottom of \"{0}\" ({1} px) must be on the keyboard's top edge ({2} px)", name, bottom, keyboardTop);
    }

    /// <summary>A terminal laid out above the keyboard has fewer rows, its last row (the cursor's) still on screen.</summary>
    [Then("the TerminalView {string} has fewer rows than before the keyboard")]
    public async Task Then_fewer_rows(string name)
    {
        var rows = await OnUIThreadAsync(() => ((TerminalControl)ElementRegistry.Resolve(name)).Rows).ConfigureAwait(false);
        rows.Should().BeLessThan(_rowsBefore, "the terminal must be re-fitted to the space above the keyboard");
        rows.Should().BeGreaterThan(0);
    }

    /// <summary>Core withholds nothing and the page content is back to its full height once the keyboard is gone.</summary>
    [Then("the page is back to its full height")]
    public async Task Then_back_to_full_height()
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var height = await OnUIThreadAsync(() => ((FrameworkElement)ElementRegistry.Resolve("page content")).ActualHeight).ConfigureAwait(false);
            if (height == _contentHeightBefore)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        await Then_full_height().ConfigureAwait(false);
    }

    /// <summary>InputPane reports the keyboard (its occluded rectangle is the keyboard's height at the window's bottom).</summary>
    [Then("the input pane reports the soft keyboard")]
    public async Task Then_input_pane_reports()
    {
        var (rect, keyboard) = await OnUIThreadAsync(() => (InputPane.GetForCurrentView().OccludedRect, Wrapper.KeyboardDips)).ConfigureAwait(false);
        keyboard.Should().BeGreaterThan(0, "the soft keyboard must be up");
        rect.Height.Should().Be(keyboard, "InputPane.OccludedRect must report the keyboard in every mode");
    }

    /// <summary>Puts the app-level mode back to the default (unset: Pan, declared modes kept) after a scenario that changed it.</summary>
    [AfterScenario(Order = -20)]
    public async Task Back_to_the_default_mode()
    {
        if (!_modeChanged)
        {
            return;
        }

        _modeChanged = false;
        await TestTargetFixture.RunOnUIThreadAsync(() => App.ResetSoftInputAdjust()).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static CodeBrixApplication App => (CodeBrixApplication)AppHost.Activity.Application;

    private static AndroidNativeWindowWrapper Wrapper => ((CodeBrixActivity)AppHost.Activity).WindowWrapper;

    private static async Task RememberAsync()
    {
        (_rootTopBefore, _contentHeightBefore) = await OnUIThreadAsync(() => (RootTopOnScreen(), ((FrameworkElement)ElementRegistry.Resolve("page content")).ActualHeight)).ConfigureAwait(false);
    }

    private static int RootTopOnScreen()
    {
        var location = new int[2];
        ((CodeBrixActivity)AppHost.Activity).RootLayout.GetLocationOnScreen(location);
        return location[1];
    }

    // The keyboard's top edge in SCREEN pixels: the window's bottom edge on the screen (its metrics, which a pan does not
    // move - unlike a view's GetLocationOnScreen) minus the IME's bottom inset.
    private static int KeyboardTopOnScreen()
    {
        var activity = AppHost.Activity;
        var insets = activity?.Window?.DecorView?.RootWindowInsets;
        if (insets == null || !insets.IsVisible(AWindowInsets.Type.Ime()))
        {
            return 0;
        }

        return activity.WindowManager.CurrentWindowMetrics.Bounds.Bottom - insets.GetInsets(AWindowInsets.Type.Ime()).Bottom;
    }

    private static global::Android.Widget.EditText FindEditText(AView view)
    {
        if (view is global::Android.Widget.EditText edit)
        {
            return edit;
        }

        if (view is global::Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (FindEditText(group.GetChildAt(i)) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static AView NativeView(string name) =>
        (PolicyDiagnostics.HandlerOf(ElementRegistry.Resolve(name)) as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView
        ?? throw new InvalidOperationException($"\"{name}\" has no native view.");

    private static async Task Settle()
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await Task.Delay(150).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }
}
