#nullable disable

using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.CommandBar;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// [AP10-G] The steps of the Android-only AndroidCommandBar group: a ToolBar on the second page of a Frame, its overflow
/// items clicked by real fingers (RealInputSteps), the Android back button pressed (OverlaySteps). The claim: a click on an
/// overflow item closes the overflow flyout, so the next tap and the next back act on the page.
/// </summary>
[Binding]
public sealed class CommandBarAndroidSteps
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A Frame that went from a label page to a page holding a ToolBar (text-only tool buttons, chevron overflow) at the top
    /// left; every button is registered under its text and counts its Clicks.
    /// </summary>
    [Given("the application shows a Frame that went from page {string} to a page holding a ToolBar named {string} {int} wide with the tool buttons {string}")]
    public async Task Given_a_frame_with_a_tool_bar_page(string first, string barName, int width, string buttons)
    {
        ElementRegistry.Clear();
        EventRecorder.Clear();
        var frame = new Frame { Name = "frame" };
        await TestTargetFixture.RunOnUIThreadAsync(() => ElementRegistry.Register("frame", frame)).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(frame).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => frame.Navigate(typeof(OverlaySteps.LabelPage), first)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var bar = new ToolBar
            {
                Name = barName,
                Width = width,
                OverflowMode = OverflowMode.Chevron,
                LabelMode = LabelMode.TextOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            foreach (var text in buttons.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var button = new ToolButton { Name = text, Text = text };
                button.Click += (_, _) => EventRecorder.Record(text, "Click");
                ElementRegistry.Register(text, button);
                bar.Items.Add(button);
            }

            ElementRegistry.Register(barName, bar);
            frame.Navigate(typeof(ToolBarHostPage), bar);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        var overflows = await OnUIThreadAsync(() => ((ToolBar)ElementRegistry.Resolve(barName)).HasOverflowItems).ConfigureAwait(false);
        overflows.Should().BeTrue("the bar \"{0}\" must be too narrow for all its buttons, so it shows a chevron", barName);
    }

    /// <summary>Registers the bar's overflow chevron under a name (the copied CommandBar group's steps bind its own features only).</summary>
    [Given("the chevron of the ToolBar {string} is named {string}")]
    public async Task Given_the_chevron_is_named(string barName, string chevronName)
    {
        var found = await OnUIThreadAsync(() =>
        {
            var chevron = VisualTreeSearch.FindDescendant<ToolBarOverflowButton>((ToolBar)ElementRegistry.Resolve(barName));
            if (chevron != null)
            {
                chevron.Name = chevronName;
                ElementRegistry.Register(chevronName, chevron);
            }

            return chevron != null;
        }).ConfigureAwait(false);
        found.Should().BeTrue("the bar \"{0}\" shows a chevron", barName);
    }

    /// <summary>Asserts that the bar's overflow flyout is open (a Core popup over the page), within five seconds.</summary>
    [Then("the overflow flyout of the ToolBar {string} is open")]
    public async Task Then_the_overflow_flyout_is_open(string barName)
    {
        var open = 0;
        for (var attempt = 0; attempt < 50 && open == 0; attempt++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);
            open = await OnUIThreadAsync(OpenPopupCount).ConfigureAwait(false);
        }

        open.Should().BeGreaterThan(0, "the chevron of \"{0}\" opens its overflow flyout", barName);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts how many times a tool button raised Click.</summary>
    [Then("the tool button {string} was clicked {int} time")]
    [Then("the tool button {string} was clicked {int} times")]
    public async Task Then_the_tool_button_was_clicked(string name, int times)
    {
        await SettleAsync().ConfigureAwait(false);
        EventRecorder.Count(name, "Click").Should().Be(times, "the Clicks of \"{0}\"; the scenario recorded [{1}]", name, string.Join(", ", EventRecorder.Recorded));
    }

    /// <summary>Asserts that no Core popup (flyout) is open over the page.</summary>
    [Then("no flyout is open over the page")]
    public async Task Then_no_flyout_is_open()
    {
        await SettleAsync().ConfigureAwait(false);
        var open = await OnUIThreadAsync(OpenPopupCount).ConfigureAwait(false);
        open.Should().Be(0, "a click on an overflow item closes the overflow flyout");
    }

    private static int OpenPopupCount()
    {
        var root = ((FrameworkElement)ElementRegistry.Resolve("frame")).XamlRoot;
        return root == null ? 0 : VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Count;
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

    /// <summary>A white page that shows the ToolBar passed as the navigation parameter at its top left.</summary>
    public sealed class ToolBarHostPage : Page
    {
        /// <summary>Creates the page.</summary>
        public ToolBarHostPage() => Background = new SolidColorBrush(Microsoft.UI.Colors.White);

        /// <inheritdoc />
        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is ToolBar bar)
            {
                Content = new Grid { Children = { bar } };
            }
        }
    }
}
