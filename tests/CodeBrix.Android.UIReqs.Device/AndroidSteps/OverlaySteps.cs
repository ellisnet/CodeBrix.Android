#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Reqnroll;
using SilverAssertions;
using Windows.UI.Core;
using ABackEventCompat = global::AndroidX.Activity.BackEventCompat;
using ACheckedTextView = global::Android.Widget.CheckedTextView;
using ADialogButtonType = global::Android.Content.DialogButtonType;
using AView = global::Android.Views.View;
using Xunit;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP4: the Material forms of overlays (platform dialogs, menus, bottom sheets) are switched ON only for the
/// Android-only scenarios tagged <c>@native-overlays</c> (AndroidFeatures/AndroidOverlays). Every other scenario
/// - the copied Popups group among them, which looks for Core's popups and template parts - runs with Core's
/// own presentation (tier 1), the one every Skia head uses. Between scenarios every platform overlay still open
/// is taken down and the back-navigation state the scenarios touch is reset.
/// </summary>
[Binding]
public sealed class OverlayHooks
{
    /// <summary>The tag of the scenarios that run with the Material overlay forms.</summary>
    public const string NativeOverlaysTag = "native-overlays";

    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the hooks.</summary>
    public OverlayHooks(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    /// <summary>Chooses the overlay presentation of the scenario and starts it with no platform overlay open.</summary>
    [BeforeScenario(Order = -50)]
    public async Task Choose_overlay_presentation()
    {
        var native = _scenarioContext.ScenarioInfo.CombinedTags.Contains(NativeOverlaysTag)
            || _scenarioContext.ScenarioInfo.Tags.Contains(NativeOverlaysTag);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            NativeOverlays.CloseAll();
            OverlayPresentation.Reset();
            OverlayPresentation.NativeContentDialogs = native;
            OverlayPresentation.NativeMenuFlyouts = native;
        }).ConfigureAwait(false);
    }

    /// <summary>Takes every platform overlay down and removes a BackRequested handler a scenario added.</summary>
    [AfterScenario(Order = -50)]
    public async Task Close_platform_overlays()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            NativeOverlays.CloseAll();
            OverlaySteps.RemoveBackRequestedHandler();
            OverlayPresentation.Reset();
            OverlayPresentation.NativeContentDialogs = false;
            OverlayPresentation.NativeMenuFlyouts = false;
        }).ConfigureAwait(false);
        await Task.Delay(300, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }
}

/// <summary>The steps of the Android-only AndroidOverlays group (Material dialogs, Material menus, back navigation).</summary>
[Binding]
public sealed class OverlaySteps
{
    private const string FlyoutKeyPrefix = "uireqs.flyout.";
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);
    private static EventHandler<BackRequestedEventArgs> _backRequested;
    private static int _backRequestedCount;

    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the steps.</summary>
    public OverlaySteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    // ------------------------------------------------------------------ dialogs

    /// <summary>Asserts the Material dialog on top shows a title and a message.</summary>
    [Then("a Material dialog is showing with the title {string} and the message {string}")]
    public async Task Then_a_Material_dialog_is_showing(string title, string message)
    {
        var (shownTitle, shownMessage) = await OnUIThreadAsync(() =>
        {
            var dialog = TopDialog() ?? throw new InvalidOperationException("No Material dialog is showing.");
            var titleId = dialog.Native.Context.Resources!.GetIdentifier("alertTitle", "id", dialog.Native.Context.PackageName);
            var titleView = dialog.Native.FindViewById<global::Android.Widget.TextView>(titleId);
            var messageView = dialog.Native.FindViewById<global::Android.Widget.TextView>(global::Android.Resource.Id.Message);
            return (titleView?.Text ?? string.Empty, messageView?.Visibility == global::Android.Views.ViewStates.Visible ? messageView.Text ?? string.Empty : string.Empty);
        }).ConfigureAwait(false);

        shownTitle.Should().Be(title, "the Material dialog shows the ContentDialog's Title");
        shownMessage.Should().Be(message, "the Material dialog shows the ContentDialog's text Content as its message");
    }

    /// <summary>Asserts which text a Material dialog slot shows.</summary>
    [Then("the Material dialog shows {string} as its {word} button")]
    public async Task Then_the_Material_dialog_shows_button(string text, string slot)
    {
        var type = slot switch
        {
            "positive" => ADialogButtonType.Positive,
            "negative" => ADialogButtonType.Negative,
            "neutral" => ADialogButtonType.Neutral,
            _ => throw new ArgumentException($"\"{slot}\" is not a Material dialog button slot (positive, negative, neutral)."),
        };
        var shown = await OnUIThreadAsync(() =>
        {
            var button = TopDialog()?.Native?.GetButton((int)type);
            return button is { Visibility: global::Android.Views.ViewStates.Visible } ? button.Text : null;
        }).ConfigureAwait(false);

        shown.Should().Be(text, "the ContentDialog's buttons take the Material {0} slot as DialogButtonSlots places them", slot);
    }

    /// <summary>Asserts that no Material dialog is showing.</summary>
    [Then("no Material dialog is showing")]
    public async Task Then_no_Material_dialog_is_showing()
    {
        await SettleAsync().ConfigureAwait(false);
        var showing = await OnUIThreadAsync(() => NativeOverlays.Open.OfType<NativeContentDialog>().Any(d => d.IsShowing)).ConfigureAwait(false);
        showing.Should().BeFalse("the Material dialog must be gone");
    }

    /// <summary>Presses a Material dialog button through its native click path.</summary>
    [When("the Material dialog button {string} is pressed")]
    public async Task When_the_Material_dialog_button_is_pressed(string text)
    {
        var pressed = await OnUIThreadAsync(() => FindDialogButton(text)?.PerformClick() == true).ConfigureAwait(false);
        pressed.Should().BeTrue("the Material dialog has an enabled button \"{0}\"", text);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts a Material dialog button's enabled state.</summary>
    [Then("the Material dialog button {string} is {word}")]
    public async Task Then_the_Material_dialog_button_is(string text, string state)
    {
        var enabled = await OnUIThreadAsync(() => FindDialogButton(text)?.Enabled).ConfigureAwait(false);
        enabled.Should().NotBeNull("the Material dialog has a button \"{0}\"", text);
        enabled.Should().Be(state == "enabled", "the Material dialog button \"{0}\" must be {1}", text, state);
    }

    /// <summary>Makes a ContentDialog cancel its first Closing.</summary>
    [Given("the ContentDialog {string} cancels its first Closing")]
    public Task Given_the_ContentDialog_cancels_its_first_Closing(string name) => TestTargetFixture.RunOnUIThreadAsync(() =>
    {
        var dialog = Dialog(name);
        var cancelled = false;
        dialog.Closing += (_, e) =>
        {
            if (!cancelled)
            {
                cancelled = true;
                e.Cancel = true;
            }
        };
    });

    /// <summary>Disables a ContentDialog's primary button before it is shown.</summary>
    [Given("the ContentDialog {string} starts with its primary button disabled")]
    public Task Given_the_primary_button_disabled(string name) => TestTargetFixture.RunOnUIThreadAsync(() => Dialog(name).IsPrimaryButtonEnabled = false);

    /// <summary>Enables a ContentDialog's primary button while it shows.</summary>
    [When("the ContentDialog {string} enables its primary button")]
    public async Task When_the_ContentDialog_enables_its_primary_button(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Dialog(name).IsPrimaryButtonEnabled = true).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Gives a ContentDialog XAML content (a panel of elements), which only Core can present.</summary>
    [Given("the ContentDialog {string} shows a panel of XAML as its content")]
    public Task Given_XAML_content(string name) => TestTargetFixture.RunOnUIThreadAsync(() =>
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Choose one of these:" });
        panel.Children.Add(new CheckBox { Content = "Red" });
        Dialog(name).Content = panel;
    });

    // -------------------------------------------------------------------- menus

    /// <summary>Lays overlays out for a window width class.</summary>
    [Given("overlays are laid out for a {word} window")]
    public Task Given_overlays_are_laid_out_for(string widthClass) =>
        TestTargetFixture.RunOnUIThreadAsync(() => OverlayPresentation.WidthClassOverride = Enum.Parse<WindowWidthClass>(widthClass));

    /// <summary>Attaches a MenuFlyout with one of each item kind.</summary>
    [Given("a MenuFlyout named {string} is attached to {string} with a toggle, a radio group, a separator and a sub-menu")]
    public async Task Given_a_rich_MenuFlyout(string flyoutName, string targetName)
    {
        MenuFlyout menu = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            menu = new MenuFlyout();
            menu.Items.Add(Recorded(new ToggleMenuFlyoutItem { Text = "Bold", IsChecked = true }));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Recorded(new RadioMenuFlyoutItem { Text = "Small", GroupName = "size" }));
            menu.Items.Add(Recorded(new RadioMenuFlyoutItem { Text = "Large", GroupName = "size", IsChecked = true }));
            menu.Items.Add(new MenuFlyoutSeparator());
            var more = new MenuFlyoutSubItem { Text = "More" };
            more.Items.Add(Recorded(new MenuFlyoutItem { Text = "Help" }));
            more.Items.Add(Recorded(new MenuFlyoutItem { Text = "About" }));
            menu.Items.Add(more);
            FlyoutBase.SetAttachedFlyout(ElementRegistry.Resolve(targetName), menu);
        }).ConfigureAwait(false);

        _scenarioContext[FlyoutKeyPrefix + flyoutName] = menu;
    }

    /// <summary>Asserts the popup menu on top and its top-level item texts.</summary>
    [Then("a Material popup menu is showing with the items {string}")]
    public async Task Then_a_Material_popup_menu_is_showing(string items)
    {
        var shown = await OnUIThreadAsync(() =>
        {
            var menu = TopMenu() ?? throw new InvalidOperationException("No Material menu is showing.");
            menu.IsSheet.Should().BeFalse("a wide window shows a popup menu, not a bottom sheet");
            var native = menu.Popup.Menu;
            return Enumerable.Range(0, native.Size()).Select(i => native.GetItem(i)!.TitleFormatted!.ToString()).ToList();
        }).ConfigureAwait(false);

        shown.Should().Equal(Split(items), "the popup menu lists the MenuFlyout's items in order");
    }

    /// <summary>Asserts the bottom sheet on top and its rows.</summary>
    [Then("a Material bottom sheet is showing with the items {string}")]
    public async Task Then_a_Material_bottom_sheet_is_showing(string items)
    {
        await SettleAsync().ConfigureAwait(false);
        var shown = await OnUIThreadAsync(() =>
        {
            var menu = TopMenu() ?? throw new InvalidOperationException("No Material menu is showing.");
            menu.IsSheet.Should().BeTrue("a Compact window shows the menu as a bottom sheet");
            return menu.SheetRows.OfType<ACheckedTextView>().Select(r => r.Text).ToList();
        }).ConfigureAwait(false);

        shown.Should().Equal(Split(items), "the bottom sheet lists the MenuFlyout's items in order");
    }

    /// <summary>Chooses a menu item through the native menu.</summary>
    [When("the Material menu item {string} is chosen")]
    public async Task When_the_Material_menu_item_is_chosen(string text)
    {
        var chosen = await OnUIThreadAsync(() => (TopMenu() ?? throw new InvalidOperationException("No Material menu is showing.")).PerformItem(text)).ConfigureAwait(false);
        chosen.Should().BeTrue("the Material menu has an enabled item \"{0}\"", text);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Dismisses the menu as a tap outside it does.</summary>
    [When("the Material menu is dismissed")]
    public async Task When_the_Material_menu_is_dismissed()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => (TopMenu() ?? throw new InvalidOperationException("No Material menu is showing.")).DismissByUser()).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts that no Material menu is showing.</summary>
    [Then("no Material menu is showing")]
    public async Task Then_no_Material_menu_is_showing()
    {
        await SettleAsync().ConfigureAwait(false);
        var showing = await OnUIThreadAsync(() => NativeOverlays.Open.OfType<NativeMenuFlyout>().Any(m => m.IsShowing)).ConfigureAwait(false);
        showing.Should().BeFalse("the Material menu must be gone");
    }

    /// <summary>Asserts a popup-menu item's check state.</summary>
    [Then("the Material menu item {string} is {word}")]
    public async Task Then_the_Material_menu_item_is(string text, string state)
    {
        var isChecked = await OnUIThreadAsync(() => FindMenuItem(TopMenu()?.Popup?.Menu, text)?.IsChecked).ConfigureAwait(false);
        isChecked.Should().NotBeNull("the Material menu has an item \"{0}\"", text);
        isChecked.Should().Be(state == "checked", "the Material menu item \"{0}\" must be {1}", text, state);
    }

    /// <summary>Asserts a sub-menu and its items.</summary>
    [Then("the Material menu has the sub-menu {string} with the items {string}")]
    public async Task Then_the_Material_menu_has_the_sub_menu(string text, string items)
    {
        var shown = await OnUIThreadAsync(() =>
        {
            var item = FindMenuItem(TopMenu()?.Popup?.Menu, text) ?? throw new InvalidOperationException($"No menu item \"{text}\".");
            item.HasSubMenu.Should().BeTrue("\"{0}\" is a MenuFlyoutSubItem", text);
            var sub = item.SubMenu!;
            return Enumerable.Range(0, sub.Size()).Select(i => sub.GetItem(i)!.TitleFormatted!.ToString()).ToList();
        }).ConfigureAwait(false);

        shown.Should().Equal(Split(items), "the sub-menu lists the MenuFlyoutSubItem's items");
    }

    /// <summary>Asserts that a popup-menu item is not checked.</summary>
    [Then("the Material menu item {string} is not checked")]
    public Task Then_the_Material_menu_item_is_not_checked(string text) => Then_the_Material_menu_item_is(text, "unchecked");

    /// <summary>Asserts a ToggleMenuFlyoutItem's IsChecked in Core ("checked" / "unchecked").</summary>
    [Then("the ToggleMenuFlyoutItem {string} is {word}")]
    public async Task Then_the_ToggleMenuFlyoutItem_is(string text, string state)
    {
        var isChecked = await OnUIThreadAsync(() => ((ToggleMenuFlyoutItem)ElementRegistry.Resolve(text)).IsChecked).ConfigureAwait(false);
        isChecked.Should().Be(state == "checked", "choosing the item runs Core's toggle");
    }

    // --------------------------------------------------------------- navigation

    /// <summary>Shows a Frame that navigated twice (a back stack of one page).</summary>
    [Given("the application shows a Frame that went from page {string} to page {string}")]
    public async Task Given_a_Frame_with_two_pages(string first, string second)
    {
        ElementRegistry.Clear();
        EventRecorder.Clear();
        var frame = new Frame { Name = "frame" };
        await TestTargetFixture.RunOnUIThreadAsync(() => ElementRegistry.Register("frame", frame)).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(frame).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => frame.Navigate(typeof(LabelPage), first)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => frame.Navigate(typeof(LabelPage), second)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            if (frame.Content is LabelPage page)
            {
                ElementRegistry.Register(second, page);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Asserts which page the frame shows.</summary>
    [Then("the Frame shows page {string}")]
    public async Task Then_the_Frame_shows_page(string label)
    {
        await SettleAsync().ConfigureAwait(false);
        var shown = await OnUIThreadAsync(() => (TheFrame().Content as LabelPage)?.Label).ConfigureAwait(false);
        shown.Should().Be(label, "the frame's current page");
    }

    /// <summary>Asserts that the frame cannot go back.</summary>
    [Then("the Frame cannot go back")]
    public async Task Then_the_Frame_cannot_go_back() =>
        (await OnUIThreadAsync(() => TheFrame().CanGoBack).ConfigureAwait(false)).Should().BeFalse("the back stack is empty");

    /// <summary>Asserts that the app takes the back button (its back callback is enabled).</summary>
    [Then("the Android back button is taken by the app")]
    public async Task Then_back_is_taken_by_the_app()
    {
        var target = await OnUIThreadAsync(() => BackNavigation.Target(Activity(), out _)).ConfigureAwait(false);
        target.Should().NotBe(BackNavigation.BackTarget.System, "something in the app can take the back");
        (await OnUIThreadAsync(() => Activity().OnBackPressedDispatcher.HasEnabledCallbacks).ConfigureAwait(false)).Should().BeTrue("the app's back callback is enabled");
    }

    /// <summary>Asserts that nothing in the app takes the back button (the system's back applies).</summary>
    [Then("the Android back button is left to the system")]
    public async Task Then_back_is_left_to_the_system()
    {
        await SettleAsync().ConfigureAwait(false);
        var target = await OnUIThreadAsync(() => BackNavigation.Target(Activity(), out _)).ConfigureAwait(false);
        target.Should().Be(BackNavigation.BackTarget.System, "nothing in the app can take the back any more");
    }

    /// <summary>
    /// Presses the Android back button: the back event goes, as the system sends it, to the OnBackPressedDispatcher
    /// of the window on top - a Material dialog or bottom sheet (ComponentDialogs) when one shows, else the activity.
    /// (A test app may not inject real key events: INJECT_EVENTS is a signature permission.)
    /// </summary>
    [When("the Android back button is pressed")]
    public async Task When_the_Android_back_button_is_pressed()
    {
        await SettleAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            if (TopDialog() is { } dialog)
            {
                dialog.Native.OnBackPressedDispatcher.OnBackPressed();
            }
            else if (TopMenu() is { IsSheet: true } sheet)
            {
                sheet.SheetDialog.OnBackPressedDispatcher.OnBackPressed();
            }
            else if (TopMenu() != null)
            {
                throw new NotSupportedException("A popup menu is a PopupWindow: its back key cannot be dispatched from the app.");
            }
            else
            {
                Activity().OnBackPressedDispatcher.OnBackPressed();
            }
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Starts a predictive back gesture and moves it to a progress.</summary>
    [When("a predictive back gesture from the left edge reaches {int} percent")]
    public async Task When_a_predictive_back_gesture_reaches(int percent)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var dispatcher = Activity().OnBackPressedDispatcher;
            _scenarioContext["uireqs.back.page"] = CurrentPageView();
            dispatcher.DispatchOnBackStarted(new ABackEventCompat(0f, 500f, 0f, ABackEventCompat.EdgeLeft));
            dispatcher.DispatchOnBackProgressed(new ABackEventCompat(percent * 5f, 500f, percent / 100f, ABackEventCompat.EdgeLeft));
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the preview of the page being left.</summary>
    [Then("the page being left is previewed smaller and shifted right")]
    public async Task Then_the_page_is_previewed()
    {
        var (scale, translation) = await OnUIThreadAsync(() =>
        {
            var view = (AView)_scenarioContext["uireqs.back.page"];
            return (view.ScaleX, view.TranslationX);
        }).ConfigureAwait(false);
        scale.Should().BeLessThan(1f, "the page shrinks with the gesture");
        translation.Should().BeGreaterThan(0f, "the page moves toward the swipe");
    }

    /// <summary>Cancels the predictive back gesture.</summary>
    [When("the predictive back gesture is cancelled")]
    public async Task When_the_predictive_back_gesture_is_cancelled()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Activity().OnBackPressedDispatcher.DispatchOnBackCancelled()).ConfigureAwait(false);
        await Task.Delay(400, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Completes the predictive back gesture (the finger is released past the threshold).</summary>
    [When("the predictive back gesture is completed")]
    public async Task When_the_predictive_back_gesture_is_completed()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Activity().OnBackPressedDispatcher.OnBackPressed()).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the page view is at its full size and place again.</summary>
    [Then("the page being left is back at full size")]
    [Then("the page that was left is at full size")]
    public async Task Then_the_page_is_at_full_size()
    {
        var (scaleX, scaleY, translation) = await OnUIThreadAsync(() =>
        {
            var view = (AView)_scenarioContext["uireqs.back.page"];
            return (view.ScaleX, view.ScaleY, view.TranslationX);
        }).ConfigureAwait(false);
        scaleX.Should().Be(1f, "the preview is undone");
        scaleY.Should().Be(1f, "the preview is undone");
        translation.Should().Be(0f, "the preview is undone");
    }

    /// <summary>Makes the application handle SystemNavigationManager.BackRequested (handled = true).</summary>
    [Given("the application handles SystemNavigationManager.BackRequested")]
    public Task Given_the_application_handles_BackRequested() => TestTargetFixture.RunOnUIThreadAsync(() =>
    {
        RemoveBackRequestedHandler();
        _backRequestedCount = 0;
        _backRequested = (_, e) =>
        {
            _backRequestedCount++;
            e.Handled = true;
        };
        SystemNavigationManager.GetForCurrentView().BackRequested += _backRequested;
        BackNavigation.Update(Activity());
    });

    /// <summary>Asserts how often the application's BackRequested handler ran.</summary>
    [Then("the application's BackRequested handler ran once")]
    public void Then_the_BackRequested_handler_ran_once() => _backRequestedCount.Should().Be(1, "the app's own back handling comes before the frame");

    /// <summary>Removes the scenario's BackRequested handler (hooks call it after every scenario).</summary>
    internal static void RemoveBackRequestedHandler()
    {
        if (_backRequested != null)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= _backRequested;
            _backRequested = null;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static CodeBrixActivity Activity() => AppHost.Activity as CodeBrixActivity ?? throw new InvalidOperationException("The scenario activity is not a CodeBrixActivity.");

    private static Frame TheFrame() => (Frame)ElementRegistry.Resolve("frame");

    private static AView CurrentPageView() =>
        CodeBrix.Android.UI.Diagnostics.NativeViewLocator.ViewOf(TheFrame().Content as UIElement) ?? throw new InvalidOperationException("The frame's page has no native view.");

    private static ContentDialog Dialog(string name) => ElementRegistry.Resolve(name) as ContentDialog ?? throw new NotSupportedException($"\"{name}\" is not a ContentDialog.");

    private static NativeContentDialog TopDialog() => NativeOverlays.Open.OfType<NativeContentDialog>().LastOrDefault(d => d.IsShowing);

    private static NativeMenuFlyout TopMenu() => NativeOverlays.Open.OfType<NativeMenuFlyout>().LastOrDefault(m => m.IsShowing);

    private static global::Android.Widget.Button FindDialogButton(string text)
    {
        var dialog = TopDialog()?.Native;
        foreach (var type in new[] { ADialogButtonType.Positive, ADialogButtonType.Negative, ADialogButtonType.Neutral })
        {
            if (dialog?.GetButton((int)type) is { Visibility: global::Android.Views.ViewStates.Visible } button && button.Text == text)
            {
                return button;
            }
        }

        return null;
    }

    private static global::Android.Views.IMenuItem FindMenuItem(global::Android.Views.IMenu menu, string text)
    {
        for (var i = 0; menu != null && i < menu.Size(); i++)
        {
            var item = menu.GetItem(i)!;
            if (item.TitleFormatted?.ToString() == text)
            {
                return item;
            }

            if (item.HasSubMenu && FindMenuItem(item.SubMenu, text) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static T Recorded<T>(T item)
        where T : MenuFlyoutItem
    {
        var text = item.Text;
        item.Name = text;
        item.Click += (_, _) => EventRecorder.Record(text, "Click");
        ElementRegistry.Register(text, item);
        return item;
    }

    private static List<string> Split(string items) => items.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default!;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(SettleDelay, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A page that shows the navigation parameter as its label.</summary>
    public sealed class LabelPage : Page
    {
        private readonly TextBlock _text = new() { FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        /// <summary>Creates the page.</summary>
        public LabelPage()
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            Content = _text;
        }

        /// <summary>The navigation parameter.</summary>
        public string Label => _text.Text;

        /// <inheritdoc />
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _text.Text = e.Parameter as string ?? string.Empty;
            Name = _text.Text;
        }
    }
}
