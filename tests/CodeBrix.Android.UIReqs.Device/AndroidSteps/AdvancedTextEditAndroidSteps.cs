#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.AdvancedTextEdit.Android;
using CodeBrix.Android.UI.AdvancedTextEdit.Editing;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;
using CodeBrix.Platform.UI.AdvancedTextEdit.Rendering;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AEditorInfo = Android.Views.InputMethods.EditorInfo;
using AGetTextFlags = Android.Views.InputMethods.GetTextFlags;
using AImeFlags = Android.Views.InputMethods.ImeFlags;
using AInputSourceType = Android.Views.InputSourceType;
using AInputTypes = Android.Text.InputTypes;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AMotionEventToolType = Android.Views.MotionEventToolType;
using ASystemClock = Android.OS.SystemClock;
using Editor = CodeBrix.Platform.UI.AdvancedTextEdit.AdvancedTextEdit;
using TextPosition = CodeBrix.Platform.UI.AdvancedTextEdit.TextViewPosition;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "The AdvancedTextEdit on Android" feature (AndroidFeatures/AndroidAdvancedTextEdit): the
/// add-in's Android canvas supply, and the editor's soft-keyboard session - the "editor" profile and the TEXT TARGET of
/// CodeBrix.Android.UI's input connection (the input method sees the document around the caret, composes in place
/// under an underline, edits the document, moves the selection). The input connection is driven as an input method
/// drives it (the connection steps are the AndroidTerminal group's: "the soft keyboard commits ..." and the rest);
/// fingers are REAL MotionEvents dispatched to the activity.
/// </summary>
[Binding]
public sealed class AdvancedTextEditAndroidSteps
{
    private const string EditorFont = "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf";
    private static readonly Dictionary<string, List<string>> Entered = new();
    private static int _updatesBefore;

    /// <summary>Shows a 600 x 240 editor (Roboto Mono 20, line numbers) at the top left, holding a text (\n = line break).</summary>
    [Given("the application shows an AdvancedTextEdit named {string} holding {string}")]
    public Task Given_an_editor(string name, string text) => ShowAsync(name, text, readOnly: false, button: null);

    /// <summary>The same editor, read-only.</summary>
    [Given("the application shows a read-only AdvancedTextEdit named {string} holding {string}")]
    public Task Given_a_read_only_editor(string name, string text) => ShowAsync(name, text, readOnly: true, button: null);

    /// <summary>The same editor with a Button above it.</summary>
    [Given("the application shows an AdvancedTextEdit named {string} holding {string}, below a Button named {string}")]
    public Task Given_an_editor_and_a_button(string name, string text, string button) => ShowAsync(name, text, readOnly: false, button);

    /// <summary>Every drawing surface of the editor (the text view's and each margin's) is the add-in's Canvas, shown by its
    /// handler as a native Skia view, and each has painted.</summary>
    [Then("every drawing surface of the AdvancedTextEdit {string} is an Android editor canvas that has painted")]
    public async Task Then_the_surfaces_are_Android_canvases(string name)
    {
        var rows = new List<string>();
        for (var attempt = 0; attempt < 30; attempt++)
        {
            rows = await OnUIThreadAsync(() => Find<FrameworkElement>(EditorOf(name), e => e is EditorCanvasElement || e.GetType().Name == "RenderCanvas")
                .Select(e =>
                {
                    var handler = PolicyDiagnostics.HandlerOf(e);
                    var view = (handler as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView?.GetType().Name;
                    var painted = e is EditorCanvasElement canvas && canvas.PaintCount > 0;
                    return $"{e.GetType().Name}/{handler?.GetType().Name}/{view}/painted={painted}";
                }).ToList()).ConfigureAwait(false);
            if (rows.Count >= 2 && rows.All(r => r.EndsWith("painted=True", StringComparison.Ordinal)))
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        rows.Count.Should().BeGreaterThanOrEqualTo(2, "the text view and the line-number margin each draw on a surface ({0})", string.Join("; ", rows));
        rows.Should().OnlyContain(r => r == $"{nameof(EditorCanvasElement)}/{nameof(EditorCanvasHandler)}/SkiaCanvasView/painted=True");
        var created = await OnUIThreadAsync(() => AndroidPlatformBootstrap.CanvasPlatform?.Created ?? 0).ConfigureAwait(false);
        created.Should().BeGreaterThanOrEqualTo(rows.Count, "the add-in's registered canvas supply made the surfaces");
    }

    /// <summary>
    /// The scenario drives the input connection itself, as the input method would: the device's real input method stops
    /// hearing the editor's selection reports for the scenario (it would end a composition it did not make); the reports
    /// are still made and counted. Put back after the scenario.
    /// </summary>
    [Given("the input method is driven by the scenario alone")]
    public void Given_the_input_method_is_driven_by_the_scenario() => CoreTextInputView.ReportsReachInputMethod = false;

    /// <summary>Lets the real input method hear the selection reports again.</summary>
    [AfterScenario]
    public void Let_the_input_method_hear_again() => CoreTextInputView.ReportsReachInputMethod = true;

    /// <summary>The input method commits a line break (a Gherkin value cannot carry one).</summary>
    [When("the soft keyboard commits a line break")]
    public Task When_commits_line_break() => WithConnectionAsync(c => c.CommitText(new Java.Lang.String("\n"), 1));

    /// <summary>Gives the editor's text area the focus (as a tap on the text does).</summary>
    [When("the text area of the AdvancedTextEdit {string} takes the focus")]
    public async Task When_text_area_takes_focus(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).TextArea.Focus(FocusState.Programmatic)).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>Puts the caret at an offset (clears the selection).</summary>
    [When("the caret of the AdvancedTextEdit {string} is put at offset {int}")]
    public async Task When_caret_at(string name, int offset)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).CaretOffset = offset).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>Hides the blinking caret before a frame (the copied group's reason: its timer raises no event).</summary>
    [When("the caret of the AdvancedTextEdit {string} is hidden")]
    public async Task When_caret_hidden(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).TextArea.Caret.Hide()).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>The controller's session is open for the editor's text area, with the profile and a text target.</summary>
    [Then("the soft-keyboard session is open for the AdvancedTextEdit {string} with the {string} profile and its text")]
    public async Task Then_session_open_with_text(string name, string profile)
    {
        var wanted = $"focused=True profile={profile} target=TextAreaInputTarget editor=True androidFocus=True";
        var state = string.Empty;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            state = await OnUIThreadAsync(() =>
            {
                var controller = CoreTextInputController.Current;
                var view = controller?.View;
                return $"focused={ReferenceEquals(controller?.FocusedControl, EditorOf(name).TextArea)} profile={view?.Profile} "
                    + $"target={view?.TargetEditor?.Target?.GetType().Name ?? "none"} editor={view?.OnCheckIsTextEditor()} androidFocus={view?.IsFocused}";
            }).ConfigureAwait(false);
            if (state == wanted)
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        state.Should().Be(wanted);
    }

    /// <summary>No soft-keyboard session was opened (a read-only editor is not text entry).</summary>
    [Then("no soft-keyboard session is open")]
    public async Task Then_no_session()
    {
        await Task.Delay(500).ConfigureAwait(false);
        var state = await OnUIThreadAsync(() =>
        {
            var controller = CoreTextInputController.Current;
            return $"focused={controller?.FocusedControl?.GetType().Name ?? "none"} profile={controller?.View?.Profile?.ToString() ?? "none"}";
        }).ConfigureAwait(false);
        state.Should().Be("focused=none profile=none");
    }

    /// <summary>The editor the text-input view describes to an input method: a multi-line text editor with suggestions.</summary>
    [Then("the soft keyboard is asked for suggestions in a multi-line editor")]
    public async Task Then_editor_info_for_an_editor()
    {
        var (type, options) = await OnUIThreadAsync(() =>
        {
            var info = new AEditorInfo();
            using var connection = CoreTextInputController.Current.View.OnCreateInputConnection(info);
            return (info.InputType, info.ImeOptions);
        }).ConfigureAwait(false);
        (type & AInputTypes.ClassText).Should().Be(AInputTypes.ClassText);
        (type & AInputTypes.TextFlagMultiLine).Should().Be(AInputTypes.TextFlagMultiLine);
        (type & AInputTypes.TextFlagNoSuggestions).Should().Be((AInputTypes)0);
        (type & AInputTypes.TextVariationVisiblePassword).Should().Be((AInputTypes)0);
        (options & AImeFlags.NoFullscreen).Should().Be(AImeFlags.NoFullscreen);
        (options & AImeFlags.NoEnterAction).Should().Be(AImeFlags.NoEnterAction);
    }

    /// <summary>What the input method is handed when it connects: the editor's selection and the text around it.</summary>
    [Then("the soft keyboard is handed the selection {int} to {int} with {string} before it and {string} after it")]
    public async Task Then_editor_info_selection(int start, int end, string before, string after)
    {
        var (selStart, selEnd, textBefore, textAfter) = await OnUIThreadAsync(() =>
        {
            var info = new AEditorInfo();
            using var connection = CoreTextInputController.Current.View.OnCreateInputConnection(info);
            return (info.InitialSelStart, info.InitialSelEnd,
                info.GetInitialTextBeforeCursor(100, 0)?.ToString(), info.GetInitialTextAfterCursor(100, 0)?.ToString());
        }).ConfigureAwait(false);
        selStart.Should().Be(start);
        selEnd.Should().Be(end);
        Escape(textBefore).Should().Be(before);
        Escape(textAfter).Should().Be(after);
    }

    /// <summary>What the input method reads through the connection around the cursor.</summary>
    [Then("the soft keyboard reads {string} before the cursor and {string} after it")]
    public async Task Then_keyboard_reads(string before, string after)
    {
        var (b, a) = await OnUIThreadAsync(() =>
        {
            using var connection = (CoreTextInputConnection)CoreTextInputController.Current.View.OnCreateInputConnection(new AEditorInfo());
            return (connection.GetTextBeforeCursorFormatted(100, 0)?.ToString(), connection.GetTextAfterCursorFormatted(100, 0)?.ToString());
        }).ConfigureAwait(false);
        Escape(b).Should().Be(before);
        Escape(a).Should().Be(after);
    }

    /// <summary>What the input method reads as the selected text.</summary>
    [Then("the soft keyboard reads {string} as the selected text")]
    public async Task Then_keyboard_reads_selection(string selected)
    {
        var text = await OnUIThreadAsync(() =>
        {
            using var connection = (CoreTextInputConnection)CoreTextInputController.Current.View.OnCreateInputConnection(new AEditorInfo());
            return connection.GetSelectedTextFormatted(AGetTextFlags.None)?.ToString();
        }).ConfigureAwait(false);
        Escape(text).Should().Be(selected);
    }

    /// <summary>The input method selects a range.</summary>
    [When("the soft keyboard selects {int} to {int}")]
    public Task When_keyboard_selects(int start, int end) => WithConnectionAsync(c => c.SetSelection(start, end));

    /// <summary>The input method makes a range of the text its composition again.</summary>
    [When("the soft keyboard makes {int} to {int} its composition")]
    public Task When_keyboard_recomposes(int start, int end) => WithConnectionAsync(c => c.SetComposingRegion(start, end));

    /// <summary>Starts counting the selection updates the input method is sent.</summary>
    [When("the selection updates sent to the soft keyboard are counted from now")]
    public async Task When_counting_updates() =>
        _updatesBefore = await OnUIThreadAsync(() => CoreTextInputController.Current.View.SelectionUpdates).ConfigureAwait(false);

    /// <summary>The input method was sent at least one selection update since counting started.</summary>
    [Then("the soft keyboard was told that the selection moved")]
    public async Task Then_updates_sent()
    {
        var updates = await OnUIThreadAsync(() => CoreTextInputController.Current.View.SelectionUpdates).ConfigureAwait(false);
        updates.Should().BeGreaterThan(_updatesBefore, "the input method must hear of every change the editor makes to its text or selection");
    }

    /// <summary>What the editor's document holds (\n = line break).</summary>
    [Then("the AdvancedTextEdit {string} holds {string}")]
    public async Task Then_holds(string name, string text)
    {
        var actual = await OnUIThreadAsync(() => EditorOf(name).Text).ConfigureAwait(false);
        Escape(actual).Should().Be(text);
    }

    /// <summary>Where the caret is.</summary>
    [Then("the caret of the AdvancedTextEdit {string} is at offset {int}")]
    public async Task Then_caret_at(string name, int offset)
    {
        var actual = await OnUIThreadAsync(() => EditorOf(name).CaretOffset).ConfigureAwait(false);
        actual.Should().Be(offset);
    }

    /// <summary>Where the caret is, as a line and a column.</summary>
    [Then("the caret of the AdvancedTextEdit {string} is at line {int}, column {int}")]
    public async Task Then_caret_at_line_column(string name, int line, int column)
    {
        var actual = await OnUIThreadAsync(() => $"{EditorOf(name).TextArea.Caret.Line},{EditorOf(name).TextArea.Caret.Column}").ConfigureAwait(false);
        actual.Should().Be($"{line},{column}");
    }

    /// <summary>What is selected.</summary>
    [Then("the selected text of the AdvancedTextEdit {string} is {string}")]
    public async Task Then_selected_text(string name, string text)
    {
        var actual = await OnUIThreadAsync(() => EditorOf(name).SelectedText).ConfigureAwait(false);
        Escape(actual).Should().Be(text);
    }

    /// <summary>The composition is underlined over a range (the target's underline, drawn by the text view).</summary>
    [Then("the composition of the AdvancedTextEdit {string} is underlined from {int} to {int}")]
    public async Task Then_underlined(string name, int start, int end)
    {
        var state = string.Empty;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            state = await OnUIThreadAsync(() =>
            {
                var underline = (CoreTextInputController.Current?.View?.TargetEditor?.Target as TextAreaInputTarget)?.Underline;
                return $"{underline?.Start},{underline?.End} drawn={underline?.DrawCount > 0}";
            }).ConfigureAwait(false);
            if (state == $"{start},{end} drawn=True")
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        state.Should().Be($"{start},{end} drawn=True");
    }

    /// <summary>No composition is shown.</summary>
    [Then("the AdvancedTextEdit {string} shows no composition")]
    public async Task Then_no_composition(string name)
    {
        var state = await OnUIThreadAsync(() =>
        {
            var editor = CoreTextInputController.Current?.View?.TargetEditor;
            var underline = (editor?.Target as TextAreaInputTarget)?.Underline;
            return $"composing={editor?.IsComposing == true} underline={underline?.Start ?? -1}";
        }).ConfigureAwait(false);
        state.Should().Be("composing=False underline=-1");
    }

    /// <summary>The composition underline left the text view when the session ended.</summary>
    [Then("the text view of the AdvancedTextEdit {string} draws no composition underline")]
    public async Task Then_no_underline_renderer(string name)
    {
        var count = await OnUIThreadAsync(() => EditorOf(name).TextArea.TextView.BackgroundRenderers.Count(r => r is CompositionUnderline)).ConfigureAwait(false);
        count.Should().Be(0);
    }

    /// <summary>What the editor's TextArea raised TextEntered with, in order (the typing path completion listens to).</summary>
    [Then("the AdvancedTextEdit {string} saw {string} entered")]
    public async Task Then_entered(string name, string texts)
    {
        var seen = await OnUIThreadAsync(() => string.Join("|", Entered[name])).ConfigureAwait(false);
        seen.Should().Be(texts);
    }

    /// <summary>A real finger taps the editor where a line and column are drawn.</summary>
    [When("a real finger taps the AdvancedTextEdit {string} at line {int}, column {int}")]
    public async Task When_finger_taps_at(string name, int line, int column)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var (x, y) = EventPoint(name, line, column);
            var down = ASystemClock.UptimeMillis();
            Touch(Obtain(down, down, AMotionEventActions.Down, x, y));
            Touch(Obtain(down, down + 60, AMotionEventActions.Up, x, y));
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    /// <summary>A real finger drags along a line of the editor from one column to another, then lifts.</summary>
    [When("a real finger drags along line {int} of the AdvancedTextEdit {string} from column {int} to column {int}")]
    public async Task When_finger_drags(int line, string name, int from, int to)
    {
        (int X, int Y) start = default, end = default;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            start = EventPoint(name, line, from);
            end = EventPoint(name, line, to);
        }).ConfigureAwait(false);
        var down = ASystemClock.UptimeMillis();
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(Obtain(down, down, AMotionEventActions.Down, start.X, start.Y))).ConfigureAwait(false);
        const int steps = 10;
        for (var i = 1; i <= steps; i++)
        {
            await Task.Delay(16).ConfigureAwait(false);
            var x = start.X + ((end.X - start.X) * i / steps);
            var y = start.Y + ((end.Y - start.Y) * i / steps);
            var at = down + (i * 16);
            await TestTargetFixture.RunOnUIThreadAsync(() => Touch(Obtain(down, at, AMotionEventActions.Move, x, y))).ConfigureAwait(false);
        }

        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(Obtain(down, down + ((steps + 1) * 16), AMotionEventActions.Up, end.X, end.Y))).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static async Task ShowAsync(string name, string text, bool readOnly, string button)
    {
        ElementRegistry.Clear();
        Entered.Clear();
        UIElement content = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var editor = new Editor
            {
                Name = name,
                FontFamily = new FontFamily(EditorFont),
                FontSize = 20,
                Width = 600,
                Height = 240,
                ShowLineNumbers = true,
                IsReadOnly = readOnly,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            editor.Text = Unescape(text);
            var entered = new List<string>();
            Entered[name] = entered;
            editor.TextArea.TextEntered += (_, e) => entered.Add(Escape(e.Text));
            ElementRegistry.Register(name, editor);
            var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            if (button != null)
            {
                var other = new Button { Name = button, Content = "Other" };
                ElementRegistry.Register(button, other);
                panel.Children.Add(other);
            }

            panel.Children.Add(editor);
            content = panel;
        }).ConfigureAwait(false);

        await TestTargetFixture.SetContentAsync(content).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    // The point of the screen where a line and column of the editor are drawn, in the coordinates the activity's
    // DispatchTouchEvent receives (decor-view coordinates): the text view's own layout says where the column is.
    private static (int X, int Y) EventPoint(string name, int line, int column)
    {
        var editor = EditorOf(name);
        var view = editor.TextArea.TextView;
        view.EnsureVisualLines();
        var position = view.GetVisualPosition(new TextPosition(line, column), VisualYPosition.LineMiddle);
        var local = new Windows.Foundation.Point(position.X - view.HorizontalOffset, position.Y - view.VerticalOffset);
        var point = view.TransformToVisual(null).TransformPoint(local);
        var scale = editor.XamlRoot?.RasterizationScale ?? 1d;
        var activity = (CodeBrix.Android.UI.Hosting.CodeBrixActivity)AppHost.Activity;
        var content = new int[2];
        activity.RootLayout.ContentLayer.GetLocationOnScreen(content);
        var decor = new int[2];
        activity.Window.DecorView.GetLocationOnScreen(decor);
        return ((int)Math.Round(point.X * scale) + content[0] - decor[0], (int)Math.Round(point.Y * scale) + content[1] - decor[1]);
    }

    private static async Task WithConnectionAsync(Func<CoreTextInputConnection, bool> call)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var view = CoreTextInputController.Current?.View ?? throw new InvalidOperationException("No soft-keyboard session is open.");
            var connection = (CoreTextInputConnection)view.OnCreateInputConnection(new AEditorInfo());
            call(connection).Should().BeTrue();
        }).ConfigureAwait(false);
        await Settle().ConfigureAwait(false);
    }

    private static void Touch(AMotionEvent e)
    {
        AppHost.Activity.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static AMotionEvent Obtain(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = AMotionEventToolType.Finger };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y, Pressure = action == AMotionEventActions.Up ? 0f : 1f, Size = 1f };
        return AMotionEvent.Obtain(downTime, eventTime, action, 1, new[] { properties }, new[] { coords }, 0, 0, 1f, 1f, 1, 0, AInputSourceType.Touchscreen, 0);
    }

    private static Editor EditorOf(string name) => (Editor)ElementRegistry.Resolve(name);

    private static string Unescape(string text) => (text ?? string.Empty).Replace("\\n", "\n");

    private static string Escape(string text) => text?.Replace("\r\n", "\\n").Replace("\n", "\\n");

    private static List<T> Find<T>(DependencyObject root, Func<T, bool> match)
        where T : class
    {
        var found = new List<T>();
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && match(typed))
            {
                found.Add(typed);
            }

            found.AddRange(Find(child, match));
        }

        return found;
    }

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
