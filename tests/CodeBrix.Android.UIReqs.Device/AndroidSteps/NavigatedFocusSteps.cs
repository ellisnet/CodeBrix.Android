#nullable disable

using System;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Xunit;
using AView = Android.Views.View;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// [AP10-G] A TextBox that receives a page's FIRST focus from Frame.Navigate (Core's first-focusable rule, focus state
/// Programmatic) must hold the Android focus, so that a hardware keyboard's keys go into its editor as text - and, by the
/// AP9-4 rule, no soft keyboard comes up for it. Keys are REAL KeyEvents dispatched to the activity (RealInputSteps).
/// </summary>
[Binding]
public sealed class NavigatedFocusSteps
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>A Frame on a first page whose TextBox has the (programmatic) focus, as a launch page's box has.</summary>
    [Given("the application shows a Frame on a page whose TextBox {string} has the focus")]
    public async Task Given_a_frame_with_a_focused_text_box(string name)
    {
        ElementRegistry.Clear();
        EventRecorder.Clear();
        var frame = new Frame { Name = "frame" };
        await TestTargetFixture.RunOnUIThreadAsync(() => ElementRegistry.Register("frame", frame)).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(frame).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => frame.Navigate(typeof(TextBoxPage), name)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        var focused = await OnUIThreadAsync(() =>
        {
            var box = ((TextBoxPage)frame.Content).Box;
            ElementRegistry.Register(name, box);
            return box.Focus(FocusState.Programmatic);
        }).ConfigureAwait(false);
        focused.Should().BeTrue("the first page's TextBox \"{0}\" must take the focus", name);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>The Frame navigates to a page whose first focusable element is a TextBox (Core focuses it on load).</summary>
    [When("the Frame navigates to a page whose first focusable element is a TextBox named {string}")]
    public async Task When_the_frame_navigates(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var frame = (Frame)ElementRegistry.Resolve("frame");
            frame.Navigate(typeof(TextBoxPage), name);
            ElementRegistry.Register(name, ((TextBoxPage)frame.Content).Box);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts a TextBox's text.</summary>
    [Then("the TextBox {string} holds {string}")]
    public async Task Then_the_text_box_holds(string name, string text)
    {
        await SettleAsync().ConfigureAwait(false);
        var (shown, state) = await OnUIThreadAsync(() => (((TextBox)ElementRegistry.Resolve(name)).Text, Describe(name))).ConfigureAwait(false);
        shown.Should().Be(text, "the keys typed while \"{0}\" had the focus go into it ({1})", name, state);
    }

    /// <summary>Asserts that the TextBox's native editor is the activity's focused view.</summary>
    [Then("the TextBox {string} holds the Android focus")]
    public async Task Then_the_text_box_holds_the_android_focus(string name)
    {
        await SettleAsync().ConfigureAwait(false);
        var (holds, state) = await OnUIThreadAsync(() =>
        {
            var editor = FindEditText(NativeView(name));
            return (editor != null && editor.HasFocus && ReferenceEquals(AppHost.Activity.CurrentFocus, editor), Describe(name));
        }).ConfigureAwait(false);
        holds.Should().BeTrue("the editor of \"{0}\" must hold the Android focus ({1})", name, state);
    }

    private static string Describe(string name)
    {
        var box = (TextBox)ElementRegistry.Resolve(name);
        var editor = FindEditText(NativeView(name));
        var current = AppHost.Activity?.CurrentFocus;
        return $"Core FocusState={box.FocusState}, editor attached={editor?.IsAttachedToWindow} hasFocus={editor?.HasFocus}, "
            + $"activity focus={current?.GetType().Name ?? "none"}";
    }

    private static AView NativeView(string name) =>
        (PolicyDiagnostics.HandlerOf(ElementRegistry.Resolve(name)) as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView;

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

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(SettleDelay, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A white page: a TextBox (the first focusable element) above a Button, as a typical form page.</summary>
    public sealed class TextBoxPage : Page
    {
        /// <summary>Creates the page.</summary>
        public TextBoxPage()
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.White);
            Box = new TextBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
            var panel = new StackPanel { Spacing = 16, Margin = new Thickness(16) };
            panel.Children.Add(Box);
            panel.Children.Add(new Button { Content = "Next", Width = 200, Height = 64 });
            Content = panel;
        }

        /// <summary>The page's TextBox.</summary>
        public TextBox Box { get; }

        /// <inheritdoc />
        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Box.Name = e.Parameter as string ?? string.Empty;
        }
    }
}
