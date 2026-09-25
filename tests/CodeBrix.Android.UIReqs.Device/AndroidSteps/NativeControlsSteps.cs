using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reqnroll;
using SilverAssertions;
using ABitmapDrawable = Android.Graphics.Drawables.BitmapDrawable;
using AEditText = Android.Widget.EditText;
using AImageView = Android.Widget.ImageView;
using AImeAction = Android.Views.InputMethods.ImeAction;
using AInputSourceType = Android.Views.InputSourceType;
using AMaterialAutoCompleteTextView = Google.Android.Material.TextField.MaterialAutoCompleteTextView;
using AMaterialCheckBox = Google.Android.Material.CheckBox.MaterialCheckBox;
using AMaterialSlider = Google.Android.Material.Slider.Slider;
using AMaterialSwitch = Google.Android.Material.MaterialSwitch.MaterialSwitch;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AProgressIndicator = Google.Android.Material.ProgressIndicator.BaseProgressIndicator;
using ASystemClock = Android.OS.SystemClock;
using ATextView = Android.Widget.TextView;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP3a: the steps of the Android-only "Native controls" feature (AndroidFeatures/AndroidControls) and the
/// test-run hook that turns on the native template-part stand-ins the copied scenarios address by name
/// (CodeBrix.Android.UI NativeTemplateParts; off in apps).
/// </summary>
[Binding]
public sealed class NativeControlsSteps
{
    private static readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal);

    /// <summary>Turns the part stand-ins on before any scenario builds its controls.</summary>
    [BeforeTestRun(Order = -100)]
    public static void Publish_native_template_parts() => NativeTemplateParts.Enabled = true;

    /// <summary>Puts the native ComboBox switch back after every scenario.</summary>
    [AfterScenario]
    public static void Reset_native_ComboBoxes()
    {
        ComboBoxHandler.Enabled = false;
        Counts.Clear();
        Texts.Clear();
    }

    /// <summary>Uses the native exposed drop-down for the ComboBoxes this scenario builds.</summary>
    [Given("native ComboBoxes are used")]
    public static void Given_native_ComboBoxes_are_used() => ComboBoxHandler.Enabled = true;

    /// <summary>Shows a Button whose content is a StackPanel of two TextBlocks ("First", "Second").</summary>
    [Given("the application shows a Button named {string} with a StackPanel of two TextBlocks as its content")]
    public async Task Given_a_Button_with_element_content(string name)
    {
        var element = await CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory.CreateAsync("Button", name, new[]
        {
            new KeyValuePair<string, string>("Width", "240"),
            new KeyValuePair<string, string>("Height", "120"),
        }).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "First" });
            panel.Children.Add(new TextBlock { Text = "Second" });
            ((Button)element).Content = panel;
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts which native widget shows an element (its view or one inside it).</summary>
    [Then("{string} is shown by a native {word}")]
    public async Task Then_is_shown_by_a_native(string name, string widget)
    {
        var found = new List<string>();
        var ok = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            foreach (var view in Views(ElementRegistry.Resolve(name)))
            {
                var names = TypeNames(view).ToList();
                found.Add(names[0]);
                if (names.Contains(widget, StringComparer.Ordinal))
                {
                    ok = true;
                }
            }
        }).ConfigureAwait(false);

        ok.Should().BeTrue("\"{0}\" must be shown by a native {1}; its views are [{2}]", name, widget, string.Join(", ", found));
    }

    /// <summary>Asserts that a native TextView inside an element shows a text.</summary>
    [Then("{string} shows the native text {string}")]
    public async Task Then_shows_the_native_text(string name, string text)
    {
        var texts = new List<string>();
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            texts.AddRange(Views(ElementRegistry.Resolve(name)).OfType<ATextView>().Select(t => t.Text ?? string.Empty))).ConfigureAwait(false);
        texts.Should().Contain(text);
    }

    /// <summary>Asserts the native check box's state.</summary>
    [Then("the native check box {string} is checked")]
    public async Task Then_the_native_check_box_is_checked(string name) =>
        (await Native<AMaterialCheckBox, bool>(name, v => v.Checked).ConfigureAwait(false)).Should().BeTrue();

    /// <summary>Asserts the native switch's state.</summary>
    [Then("the native switch {string} is on")]
    public async Task Then_the_native_switch_is_on(string name) =>
        (await Native<AMaterialSwitch, bool>(name, v => v.Checked).ConfigureAwait(false)).Should().BeTrue();

    /// <summary>Asserts the native slider's value.</summary>
    [Then("the native slider {string} shows {int}")]
    public async Task Then_the_native_slider_shows(string name, int value) =>
        (await Native<AMaterialSlider, float>(name, v => v.Value).ConfigureAwait(false)).Should().BeApproximately(value, 0.01f);

    /// <summary>Asserts the native progress indicator's fraction.</summary>
    [Then("the native progress of {string} is {int} percent")]
    public async Task Then_the_native_progress_is(string name, int percent) =>
        (await Native<AProgressIndicator, double>(name, v => 100.0 * v.Progress / Math.Max(1, v.Max)).ConfigureAwait(false)).Should().BeApproximately(percent, 0.5);

    /// <summary>Asserts the native editor's text (the value, before any masking).</summary>
    [Then("the native editor of {string} shows {string}")]
    public async Task Then_the_native_editor_shows(string name, string text) =>
        (await Native<ATextView, string>(name, v => v.Text ?? string.Empty, editorsOnly: true).ConfigureAwait(false)).Should().Be(text);

    /// <summary>Asserts what the native editor DISPLAYS (its layout's text: masked for a password).</summary>
    [Then("the native editor of {string} displays {string}")]
    public async Task Then_the_native_editor_displays(string name, string text)
    {
        var (shown, method) = await Native<ATextView, (string, string)>(name, v => (v.Layout?.Text ?? string.Empty, v.TransformationMethod?.GetType().FullName ?? "none"), editorsOnly: true).ConfigureAwait(false);
        shown.Should().Be(text, "the editor's transformation method is {0}", method);
    }

    /// <summary>Asserts that a password editor displays only the PasswordBox's masking character.</summary>
    [Then("the native editor of {string} shows its PasswordChar {int} times")]
    public async Task Then_the_native_editor_shows_its_PasswordChar(string name, int times)
    {
        var mask = string.Empty;
        await TestTargetFixture.RunOnUIThreadAsync(() => mask = ((PasswordBox)ElementRegistry.Resolve(name)).PasswordChar).ConfigureAwait(false);
        await Then_the_native_editor_displays(name, new string(string.IsNullOrEmpty(mask) ? '\u25CF' : mask[0], times)).ConfigureAwait(false);
    }

    /// <summary>Asserts the size of the bitmap an image view shows.</summary>
    [Then("the native image of {string} is a {int} by {int} bitmap")]
    public async Task Then_the_native_image_is_a_bitmap(string name, int width, int height)
    {
        var size = await Native<AImageView, (int, int)>(name, v => v.Drawable is ABitmapDrawable { Bitmap: { } b } ? (b.Width, b.Height) : (0, 0)).ConfigureAwait(false);
        size.Should().Be((width, height));
    }

    /// <summary>Asserts the rows of a native drop-down.</summary>
    [Then("the native drop-down of {string} lists {string}")]
    public async Task Then_the_native_drop_down_lists(string name, string rows)
    {
        var expected = rows.Split(',').Select(r => r.Trim()).ToArray();
        var actual = await Native<AMaterialAutoCompleteTextView, string[]>(name, v =>
            Enumerable.Range(0, v.Adapter?.Count ?? 0).Select(i => v.Adapter!.GetItem(i)?.ToString() ?? string.Empty).ToArray()).ConfigureAwait(false);
        actual.Should().Equal(expected);
    }

    /// <summary>Chooses a row of a native drop-down (the row's click, as a finger on the open list).</summary>
    [When("row {int} of the native drop-down of {string} is chosen")]
    public async Task When_row_of_the_native_drop_down_is_chosen(int row, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var editor = Views(ElementRegistry.Resolve(name)).OfType<AMaterialAutoCompleteTextView>().First();
            editor.OnItemClickListener!.OnItemClick(null, null, row - 1, row - 1);
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A real finger taps the Material switch of a ToggleSwitch.</summary>
    [When("a real finger taps the switch of {string}")]
    public async Task When_a_real_finger_taps_the_switch_of(string name)
    {
        var (x, y) = await CenterOfNativeAsync<AMaterialSwitch>(name).ConfigureAwait(false);
        await TapAsync(x, y).ConfigureAwait(false);
    }

    /// <summary>A real finger taps an element a share of the way across its width.</summary>
    [When("a real finger taps {string} at {int} percent of its width")]
    public async Task When_a_real_finger_taps_at_percent_of_its_width(string name, int percent)
    {
        var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), 0).ConfigureAwait(false);
        await TapAsync(bounds.X + (int)Math.Round(bounds.Width * percent / 100.0), bounds.Center.Y).ConfigureAwait(false);
    }

    /// <summary>Starts counting a TextBox's TextChanged events.</summary>
    [Given("the TextChanged of {string} is counted")]
    public async Task Given_the_TextChanged_is_counted(string name)
    {
        Counts[name] = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            ((TextBox)ElementRegistry.Resolve(name)).TextChanged += (_, _) => Counts[name]++).ConfigureAwait(false);
    }

    /// <summary>Asserts how many TextChanged events were counted (Core raises them on the dispatcher).</summary>
    [Then("the TextChanged of {string} was counted {int} times")]
    public async Task Then_the_TextChanged_was_counted(string name, int times)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name).Should().Be(times, "TextChanged of \"{0}\"", name);
    }

    /// <summary>Shows a NumberBox with inline spin buttons holding a value.</summary>
    [Given("the application shows a NumberBox named {string} holding {int}")]
    public async Task Given_a_NumberBox(string name, int value)
    {
        NumberBox box = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            box = new NumberBox { Name = name, Width = 300, Value = value, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
            ElementRegistry.Register(name, box);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(box).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts a NumberBox's Value.</summary>
    [Then("the NumberBox {string} holds {int}")]
    public async Task Then_the_NumberBox_holds(string name, int value)
    {
        var actual = double.NaN;
        await TestTargetFixture.RunOnUIThreadAsync(() => actual = ((NumberBox)ElementRegistry.Resolve(name)).Value).ConfigureAwait(false);
        actual.Should().Be(value);
    }

    /// <summary>A real finger taps the end icon of the Material text field of an element.</summary>
    [When("a real finger taps the end icon of {string}")]
    public async Task When_a_real_finger_taps_the_end_icon_of(string name)
    {
        var center = (0, 0);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var field = Views(ElementRegistry.Resolve(name)).OfType<Google.Android.Material.TextField.TextInputLayout>().First();
            var id = field.Context!.Resources!.GetIdentifier("text_input_end_icon", "id", field.Context.PackageName);
            var icon = field.FindViewById(id) ?? throw new InvalidOperationException("the text field has no end icon view");
            var location = new int[2];
            icon.GetLocationInWindow(location);
            center = (location[0] + (icon.Width / 2), location[1] + (icon.Height / 2));
        }).ConfigureAwait(false);
        await TapAsync(center.Item1, center.Item2).ConfigureAwait(false);
    }

    /// <summary>Shows an AutoSuggestBox that records its TextChanged (user input) and QuerySubmitted texts.</summary>
    [Given("the application shows an AutoSuggestBox named {string} that records its events")]
    public async Task Given_an_AutoSuggestBox(string name)
    {
        AutoSuggestBox box = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            box = new AutoSuggestBox { Name = name, Width = 400 };
            box.TextChanged += (sender, e) =>
            {
                Texts[name + ".last"] = sender.Text + " (" + e.Reason + ")";
                if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    Texts[name + ".user"] = sender.Text;
                }
            };
            box.QuerySubmitted += (_, e) => Texts[name + ".query"] = e.QueryText;
            ElementRegistry.Register(name, box);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(box).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the last user-input text an AutoSuggestBox reported.</summary>
    [Then("the AutoSuggestBox {string} reported the user text {string}")]
    public async Task Then_the_AutoSuggestBox_reported(string name, string text)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Texts.GetValueOrDefault(name + ".user").Should().Be(text, "the last TextChanged was {0}; the native editor shows \"{1}\"",
            Texts.GetValueOrDefault(name + ".last") ?? "never raised",
            await Native<ATextView, string>(name, v => v.Text ?? string.Empty, editorsOnly: true).ConfigureAwait(false));
    }

    /// <summary>The soft keyboard's action key (the IME action the editor declares) on an element's native editor.</summary>
    [When("the soft keyboard's action key is used on {string}")]
    public async Task When_the_soft_keyboard_action_key_is_used(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var editor = Views(ElementRegistry.Resolve(name)).OfType<AEditText>().First();
            editor.OnEditorAction(editor.ImeOptions & AImeAction.ImeMaskAction);
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the query an AutoSuggestBox submitted.</summary>
    [Then("the AutoSuggestBox {string} submitted {string}")]
    public async Task Then_the_AutoSuggestBox_submitted(string name, string text)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        Texts.GetValueOrDefault(name + ".query").Should().Be(text);
    }

    private static async Task<TResult> Native<TView, TResult>(string name, Func<TView, TResult> read, bool editorsOnly = false)
        where TView : AView
    {
        TResult result = default!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var views = Views(ElementRegistry.Resolve(name)).OfType<TView>();
            if (editorsOnly)
            {
                views = views.Where(v => v is AEditText);
            }

            var view = views.FirstOrDefault()
                ?? throw new InvalidOperationException($"\"{name}\" has no native {typeof(TView).Name}.");
            result = read(view);
        }).ConfigureAwait(false);
        return result;
    }

    private static async Task<(int X, int Y)> CenterOfNativeAsync<TView>(string name)
        where TView : AView
    {
        var center = (0, 0);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var view = Views(ElementRegistry.Resolve(name)).OfType<TView>().First();
            var location = new int[2];
            view.GetLocationInWindow(location);
            center = (location[0] + (view.Width / 2), location[1] + (view.Height / 2));
        }).ConfigureAwait(false);
        return center;
    }

    private static IEnumerable<AView> Views(FrameworkElement element)
    {
        var root = NativeViewLocator.ViewOf(element);
        if (root == null)
        {
            yield break;
        }

        var pending = new Stack<AView>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var view = pending.Pop();
            yield return view;
            if (view is AViewGroup group)
            {
                for (var i = group.ChildCount - 1; i >= 0; i--)
                {
                    if (group.GetChildAt(i) is { } child)
                    {
                        pending.Push(child);
                    }
                }
            }
        }
    }

    private static IEnumerable<string> TypeNames(AView view)
    {
        for (var type = view.GetType(); type != null && type != typeof(Java.Lang.Object); type = type.BaseType)
        {
            yield return type.Name;
        }
    }

    private static async Task TapAsync(int x, int y)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Dispatch(down, down, AMotionEventActions.Down, x, y);
            Dispatch(down, down + 50, AMotionEventActions.Up, x, y);
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static void Dispatch(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var e = AMotionEvent.Obtain(downTime, eventTime, action, x, y, 0)!;
        e.SetSource(AInputSourceType.Touchscreen);
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }
}
