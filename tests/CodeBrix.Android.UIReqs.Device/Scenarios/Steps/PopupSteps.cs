using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hooks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The Popups group's steps: the things an application shows OVER itself rather than inside
/// itself - a flyout, a menu, a bare popup, a modal dialog, a teaching tip - and the bar it
/// shows inside itself to say something happened. Every pattern here names the control it
/// belongs to, so that no other coverage group's sentence can match one of them.
/// <para>
/// A popup is hosted beside the application's root, so its content is not below the root and
/// the harness's usual tree walk does not reach it; these steps reach a dialog's buttons
/// through the dialog itself and a tip's content through the open popup. Taking a popup down
/// afterwards is not this class's job: the harness closes every open popup as the first act of
/// the scenario reset.
/// </para>
/// </summary>
[Binding]
public sealed class PopupSteps
{
	/// <summary>
	/// How long a flyout is given to finish opening. Opening one is animated, so a frame taken
	/// the instant the flyout is shown would catch it on its way in.
	/// </summary>
	public static readonly TimeSpan FlyoutSettleDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>
	/// How long a dialog is given to finish opening. A dialog scales and fades in over the
	/// smoke layer it paints, and the requirement is about what it looks like when it has
	/// arrived.
	/// </summary>
	public static readonly TimeSpan DialogSettleDelay = TimeSpan.FromMilliseconds(600);

	/// <summary>
	/// How long the panel is left alone before a tap that targets a part of a template. A real
	/// finger never arrives in the same instant as the control it touches.
	/// </summary>
	public static readonly TimeSpan BeforeTapDelay = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// How long a teaching tip is given to arrive or to leave. A tip does not appear the
	/// instant it is opened: it builds its own popup and animates into place, so a scenario
	/// that looked immediately would find nothing over the panel yet.
	/// </summary>
	public static readonly TimeSpan TipSettleDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>How long a dialog is given to hand back its result once a button has been tapped.</summary>
	public static readonly TimeSpan DialogResultTimeout = TimeSpan.FromSeconds(5);

	/// <summary>The template part a ContentDialog draws its own surface as.</summary>
	public const string DialogSurfacePart = "BackgroundElement";

	/// <summary>The template part a ContentDialog's primary button is.</summary>
	public const string DialogPrimaryButtonPart = "PrimaryButton";

	/// <summary>The template part a ContentDialog's secondary button is.</summary>
	public const string DialogSecondaryButtonPart = "SecondaryButton";

	/// <summary>The template part a ContentDialog's close button is.</summary>
	public const string DialogCloseButtonPart = "CloseButton";

	/// <summary>The template part an InfoBar's close button is.</summary>
	public const string InfoBarCloseButtonPart = "CloseButton";

	private const string FlyoutKeyPrefix = "uireqs.flyout.";
	private const string DialogResultKeyPrefix = "uireqs.dialog.";

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public PopupSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the Popups group's controls and colour names, before the
	/// first scenario.
	/// </summary>
	/// <returns>A task that completes once a feature file may name those controls and colours.</returns>
	[BeforeTestRun(Order = 13)]
	public static async Task Register_the_popup_controls()
	{
		PopupElements.Register();
		await PopupElements.RegisterColorsAsync().ConfigureAwait(false);
	}

	// -------------------------------------------------------------- flyouts

	/// <summary>
	/// Attaches a Flyout, whose whole content is one panel of a known colour, to an element
	/// that is already showing - so that "the flyout is showing" is something a person can see.
	/// </summary>
	/// <param name="flyoutName">The name the scenario refers to the flyout by.</param>
	/// <param name="targetName">The Gherkin name of the element the flyout belongs to.</param>
	/// <param name="panelName">The name the scenario refers to the flyout's panel by.</param>
	/// <param name="width">The panel's width in logical pixels.</param>
	/// <param name="height">The panel's height in logical pixels.</param>
	/// <param name="color">The colour the panel is painted.</param>
	/// <returns>A task that completes once the flyout is attached.</returns>
	[Given("a Flyout named {string} is attached to {string} with a panel named {string} {int} by {int} painted {string}")]
	public async Task Given_a_Flyout_is_attached(string flyoutName, string targetName, string panelName,
		int width, int height, Color color)
	{
		FlyoutBase flyout = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var panel = new Border
			{
				Name = panelName,
				Width = width,
				Height = height,
				Background = new SolidColorBrush(color),
			};
			ElementRegistry.Register(panelName, panel);

			flyout = new Flyout { Content = panel };
			FlyoutBase.SetAttachedFlyout(ElementRegistry.Resolve(targetName), flyout);
		}).ConfigureAwait(false);

		_scenarioContext[FlyoutKeyPrefix + flyoutName] = flyout;
	}

	/// <summary>
	/// Attaches a MenuFlyout, whose items are the commands a menu offers, to an element that is
	/// already showing. Each item is registered under its own text, so a scenario taps it and
	/// asks about its Click with the sentences every coverage group shares.
	/// </summary>
	/// <param name="flyoutName">The name the scenario refers to the menu by.</param>
	/// <param name="targetName">The Gherkin name of the element the menu belongs to.</param>
	/// <param name="items">An Item table, one row per menu item.</param>
	/// <returns>A task that completes once the menu is attached.</returns>
	[Given("a MenuFlyout named {string} is attached to {string} with the items:")]
	public async Task Given_a_MenuFlyout_is_attached(string flyoutName, string targetName, DataTable items)
	{
		ArgumentNullException.ThrowIfNull(items);

		var texts = items.Rows.Select(row => row["Item"]).ToArray();

		FlyoutBase flyout = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var menu = new MenuFlyout();
			foreach (var text in texts)
			{
				var item = new MenuFlyoutItem { Name = text, Text = text };
				item.Click += (_, _) => EventRecorder.Record(text, "Click");
				ElementRegistry.Register(text, item);
				menu.Items.Add(item);
			}

			flyout = menu;
			FlyoutBase.SetAttachedFlyout(ElementRegistry.Resolve(targetName), menu);
		}).ConfigureAwait(false);

		_scenarioContext[FlyoutKeyPrefix + flyoutName] = flyout;
	}

	/// <summary>Shows an attached flyout at the element it belongs to, and lets it settle.</summary>
	/// <param name="flyoutName">The name the scenario gave the flyout.</param>
	/// <param name="targetName">The Gherkin name of the element to show it at.</param>
	/// <returns>A task that completes once the flyout is open and still.</returns>
	[When("the flyout {string} is shown at {string}")]
	public async Task When_the_flyout_is_shown_at(string flyoutName, string targetName)
	{
		var flyout = Flyout(flyoutName);
		await TestTargetFixture.RunOnUIThreadAsync(() => flyout.ShowAt(ElementRegistry.Resolve(targetName)))
			.ConfigureAwait(false);
		await SettleAsync(FlyoutSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts that a flyout the scenario named is showing.</summary>
	/// <param name="flyoutName">The name the scenario gave the flyout.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the flyout {string} is open")]
	public async Task Then_the_flyout_is_open(string flyoutName) =>
		(await IsFlyoutOpenAsync(flyoutName).ConfigureAwait(false)).Should()
			.BeTrue("the flyout \"{0}\" must be open", flyoutName);

	/// <summary>Asserts that a flyout the scenario named is not showing.</summary>
	/// <param name="flyoutName">The name the scenario gave the flyout.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the flyout {string} is closed")]
	public async Task Then_the_flyout_is_closed(string flyoutName) =>
		(await IsFlyoutOpenAsync(flyoutName).ConfigureAwait(false)).Should()
			.BeFalse("the flyout \"{0}\" must be closed", flyoutName);

	/// <summary>
	/// Fills the panel with a backdrop that is white down one half and black down the other, and
	/// attaches a Flyout with an empty presenter to it. A flyout shown over this straddles the
	/// two halves, so anything it lets through from behind it shows up as two colours inside one
	/// presenter rather than one.
	/// </summary>
	/// <param name="backdropName">The name the scenario refers to the backdrop by.</param>
	/// <param name="flyoutName">The name the scenario refers to the flyout by.</param>
	/// <param name="width">The flyout content's width in logical pixels.</param>
	/// <param name="height">The flyout content's height in logical pixels.</param>
	/// <returns>A task that completes once the backdrop is showing and the flyout is attached.</returns>
	[Given("a Flyout named {string} with an empty panel {int} by {int} is attached to a light and dark backdrop named {string}")]
	public async Task Given_a_Flyout_with_an_empty_panel_over_a_split_backdrop(string flyoutName,
		int width, int height, string backdropName)
	{
		Grid backdrop = null!;
		FlyoutBase flyout = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			backdrop = new Grid { Name = backdropName };
			backdrop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			backdrop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			var light = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.White) };
			var dark = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.Black) };
			Grid.SetColumn(dark, 1);
			backdrop.Children.Add(light);
			backdrop.Children.Add(dark);
			ElementRegistry.Register(backdropName, backdrop);

			// The content carries no background of its own, so what is seen inside the presenter
			// is the presenter's own surface and nothing else.
			var panel = new Border { Width = width, Height = height };
			flyout = new Flyout { Content = panel };
			FlyoutBase.SetAttachedFlyout(backdrop, flyout);
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(backdrop).ConfigureAwait(false);
		_scenarioContext[FlyoutKeyPrefix + flyoutName] = flyout;
	}

	/// <summary>
	/// Asserts that the flyout that is open covers what is behind it: its surface is one colour
	/// all the way across, whatever it was shown over.
	/// </summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the open flyout hides what is behind it")]
	public async Task Then_the_open_flyout_hides_what_is_behind_it() =>
		(await OpenFlyoutInteriorAsync().ConfigureAwait(false)).IsOneOpaqueSurface();

	/// <summary>
	/// Attaches a MenuFlyout holding a given number of equally tall items, named "Item 1" to
	/// "Item N", to an element that is already showing. A menu built this way is how a scenario
	/// asks for one TALLER than the panel it has to fit on, without depending on how tall the
	/// theme happens to draw a menu item.
	/// </summary>
	/// <param name="flyoutName">The name the scenario refers to the menu by.</param>
	/// <param name="targetName">The Gherkin name of the element the menu belongs to.</param>
	/// <param name="count">How many items the menu holds.</param>
	/// <param name="itemHeight">How tall each item is, in logical pixels.</param>
	/// <returns>A task that completes once the menu is attached.</returns>
	[Given("a MenuFlyout named {string} is attached to {string} with {int} items {int} tall")]
	public async Task Given_a_MenuFlyout_is_attached_with_sized_items(string flyoutName, string targetName,
		int count, int itemHeight)
	{
		var texts = Enumerable.Range(1, count)
			.Select(number => string.Create(CultureInfo.InvariantCulture, $"Item {number}"))
			.ToArray();

		FlyoutBase flyout = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var menu = new MenuFlyout();
			foreach (var text in texts)
			{
				var item = new MenuFlyoutItem { Name = text, Text = text, Height = itemHeight };
				item.Click += (_, _) => EventRecorder.Record(text, "Click");
				ElementRegistry.Register(text, item);
				menu.Items.Add(item);
			}

			flyout = menu;
			FlyoutBase.SetAttachedFlyout(ElementRegistry.Resolve(targetName), menu);
		}).ConfigureAwait(false);

		_scenarioContext[FlyoutKeyPrefix + flyoutName] = flyout;
	}

	/// <summary>
	/// Asserts that the flyout that is open lies inside the panel. A flyout the application
	/// cannot fit is not allowed to run off the panel's edge, because everything past that edge
	/// is unreachable: it has to be made small enough to fit and let the person scroll it.
	/// </summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the open flyout fits within the panel")]
	public static async Task Then_the_open_flyout_fits_within_the_panel()
	{
		var presenter = await OpenFlyoutPresenterAsync().ConfigureAwait(false);
		var bounds = await PresenterBoundsAsync(presenter).ConfigureAwait(false);
		var (width, height) = TestTargetFixture.PanelSize;

		bounds.Height.Should().BeLessThanOrEqualTo(height,
			"the open flyout must not be taller than the {0} by {1} panel", width, height);
		bounds.Width.Should().BeLessThanOrEqualTo(width,
			"the open flyout must not be wider than the {0} by {1} panel", width, height);
		bounds.Bottom.Should().BeLessThanOrEqualTo(height,
			"the bottom of the open flyout must be on the panel, not past it");
		bounds.Top.Should().BeGreaterThanOrEqualTo(0.0,
			"the top of the open flyout must be on the panel, not above it");
	}

	/// <summary>Asserts that the flyout that is open offers more content than it can show at once.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the open flyout can be scrolled")]
	public static async Task Then_the_open_flyout_can_be_scrolled() =>
		(await OpenFlyoutScrollableHeightAsync().ConfigureAwait(false)).Should().BeGreaterThan(0.0,
			"a flyout too tall for the panel must offer the rest of its items by scrolling");

	/// <summary>Asserts that the flyout that is open shows everything it holds.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the open flyout cannot be scrolled")]
	public static async Task Then_the_open_flyout_cannot_be_scrolled() =>
		(await OpenFlyoutScrollableHeightAsync().ConfigureAwait(false)).Should().Be(0.0,
			"a flyout that fits on the panel must show all of itself at once");

	/// <summary>Scrolls the flyout that is open to the bottom of its items, and lets it settle.</summary>
	/// <returns>A task that completes once the last item is in view and the frame is still.</returns>
	[When("the open flyout is scrolled to its end")]
	public static async Task When_the_open_flyout_is_scrolled_to_its_end()
	{
		var presenter = await OpenFlyoutPresenterAsync().ConfigureAwait(false);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var scroller = FlyoutScroller(presenter);
			scroller.ChangeView(null, scroller.ScrollableHeight, null, true);
		}).ConfigureAwait(false);

		await SettleAsync(FlyoutSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Hides a flyout the way an application does, without a finger.</summary>
	/// <param name="flyoutName">The name the scenario gave the flyout.</param>
	/// <returns>A task that completes once the flyout has gone.</returns>
	[When("the flyout {string} is hidden")]
	public async Task When_the_flyout_is_hidden(string flyoutName)
	{
		var flyout = Flyout(flyoutName);
		await TestTargetFixture.RunOnUIThreadAsync(flyout.Hide).ConfigureAwait(false);
		await SettleAsync(FlyoutSettleDelay).ConfigureAwait(false);
	}

	// --------------------------------------------------------------- popups

	/// <summary>
	/// Shows a bare Popup whose child is one panel of a known colour. The Popup itself takes no
	/// room on the panel: nothing is drawn until a scenario opens it.
	/// </summary>
	/// <param name="name">The name the scenario refers to the Popup by.</param>
	/// <param name="panelName">The name the scenario refers to the Popup's child by.</param>
	/// <param name="width">The child's width in logical pixels.</param>
	/// <param name="height">The child's height in logical pixels.</param>
	/// <param name="color">The colour the child is painted.</param>
	/// <returns>A task that completes once the Popup is in the tree.</returns>
	[Given("the application shows a Popup named {string} with a panel named {string} {int} by {int} painted {string}")]
	public async Task Given_the_application_shows_a_Popup(string name, string panelName,
		int width, int height, Color color)
	{
		Popup popup = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var panel = new Border
			{
				Name = panelName,
				Width = width,
				Height = height,
				Background = new SolidColorBrush(color),
			};
			ElementRegistry.Register(panelName, panel);

			popup = new Popup
			{
				Name = name,
				Child = panel,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			ElementRegistry.Register(name, popup);
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(popup).ConfigureAwait(false);
	}

	/// <summary>Asserts that a bare Popup reports itself open.</summary>
	/// <param name="name">The Gherkin name of the Popup.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Popup {string} is open")]
	public async Task Then_the_Popup_is_open(string name) =>
		(await IsPopupOpenAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the Popup \"{0}\" must be open", name);

	/// <summary>Asserts that a bare Popup reports itself closed.</summary>
	/// <param name="name">The Gherkin name of the Popup.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Popup {string} is closed")]
	public async Task Then_the_Popup_is_closed(string name) =>
		(await IsPopupOpenAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the Popup \"{0}\" must be closed", name);

	/// <summary>Asserts that whatever popup is open was actually drawn on the panel.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the open popup has ink")]
	public async Task Then_the_open_popup_has_ink() =>
		(await OpenPopupRegionAsync().ConfigureAwait(false)).HasInk();

	// ------------------------------------------------------------- dialogs

	/// <summary>
	/// Builds a ContentDialog the scenario can show. A dialog is not part of the page: it
	/// hosts itself over the whole application when it is shown, so the scenario names one
	/// rather than putting it on the panel.
	/// </summary>
	/// <param name="name">The name the scenario refers to the dialog by.</param>
	/// <param name="properties">A Property/Value table: Title, Content and the button texts.</param>
	/// <returns>A task that completes once the dialog exists.</returns>
	[Given("the scenario has a ContentDialog named {string} with:")]
	public async Task Given_the_scenario_has_a_ContentDialog(string name, DataTable properties)
	{
		ArgumentNullException.ThrowIfNull(properties);

		var settings = properties.Rows
			.Select(row => (Property: row[properties.Header.First()], Value: row[properties.Header.Last()]))
			.ToArray();

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			// A dialog is not part of the page, so nothing else can tell it which window it
			// belongs to: an application gives it the XamlRoot of the page it is showing over,
			// and without one it has nowhere to put itself.
			var dialog = new ContentDialog
			{
				Name = name,
				XamlRoot = VirtualApplication.Instance.Root.XamlRoot,
			};
			foreach (var setting in settings)
			{
				ApplyDialogProperty(dialog, setting.Property, setting.Value);
			}

			dialog.Closed += (_, _) => EventRecorder.Record(name, "Closed");
			ElementRegistry.Register(name, dialog);
		}).ConfigureAwait(false);
	}

	/// <summary>
	/// Shows a ContentDialog. The call that shows one hands back the result it will finish
	/// with, so the scenario keeps that promise and asks about it after a button has been
	/// tapped on the panel.
	/// </summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <returns>A task that completes once the dialog is up and still.</returns>
	[When("the ContentDialog {string} is shown")]
	public async Task When_the_ContentDialog_is_shown(string name)
	{
		Task<ContentDialogResult> result = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var dialog = Dialog(name);
			result = dialog.ShowAsync().AsTask();
		}).ConfigureAwait(false);

		_scenarioContext[DialogResultKeyPrefix + name] = result;
		await SettleAsync(DialogSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Taps the primary button of a ContentDialog that is showing.</summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the primary button of the ContentDialog {string} is tapped")]
	public async Task When_the_primary_button_is_tapped(string name) =>
		await TapDialogPartAsync(name, DialogPrimaryButtonPart).ConfigureAwait(false);

	/// <summary>Taps the secondary button of a ContentDialog that is showing.</summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the secondary button of the ContentDialog {string} is tapped")]
	public async Task When_the_secondary_button_is_tapped(string name) =>
		await TapDialogPartAsync(name, DialogSecondaryButtonPart).ConfigureAwait(false);

	/// <summary>Taps the close button of a ContentDialog that is showing.</summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the close button of the ContentDialog {string} is tapped")]
	public async Task When_the_dialog_close_button_is_tapped(string name) =>
		await TapDialogPartAsync(name, DialogCloseButtonPart).ConfigureAwait(false);

	/// <summary>Asserts that a ContentDialog is on the panel.</summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ContentDialog {string} is showing")]
	public async Task Then_the_ContentDialog_is_showing(string name)
	{
		(await ScenarioHooks.OpenPopupCountAsync().ConfigureAwait(false)).Should().BeGreaterThan(0,
			"the ContentDialog \"{0}\" hosts itself in a popup, which must be open while it shows", name);
		(await DialogSurfaceAsync(name).ConfigureAwait(false)).Should().NotBeNull(
			"the ContentDialog \"{0}\" must have built its own surface", name);
	}

	/// <summary>Asserts that a ContentDialog has gone from the panel.</summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ContentDialog {string} is closed")]
	public async Task Then_the_ContentDialog_is_closed(string name)
	{
		var count = await ScenarioHooks.OpenPopupCountAsync().ConfigureAwait(false);
		count.Should().Be(0, "the ContentDialog \"{0}\" must have taken its popup down", name);
	}

	/// <summary>Asserts what a ContentDialog finished with. The result is what ShowAsync promised.</summary>
	/// <param name="name">The Gherkin name of the dialog.</param>
	/// <param name="expected">The result, as ContentDialogResult spells it.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ContentDialog {string} returned {string}")]
	public async Task Then_the_ContentDialog_returned(string name, string expected)
	{
		var promise = DialogResult(name);
		var finished = await Task.WhenAny(promise, Task.Delay(DialogResultTimeout,
			TestContext.Current.CancellationToken)).ConfigureAwait(false);

		ReferenceEquals(finished, promise).Should().BeTrue(
			"the ContentDialog \"{0}\" must hand back its result once a button has been tapped, "
			+ "and it had not done so after {1}", name, DialogResultTimeout);

		var result = await promise.ConfigureAwait(false);
		result.Should().Be(GherkinValue.ToEnum<ContentDialogResult>(expected),
			"the result of the ContentDialog \"{0}\" was asserted", name);
	}

	/// <summary>
	/// Asserts that the panel behind a modal dialog is dimmed: the same corner of the panel,
	/// far from the dialog itself, is darker than it was before the dialog was shown.
	/// </summary>
	/// <param name="frameName">The frame taken while the dialog is showing.</param>
	/// <param name="otherFrameName">The frame taken before it was shown.</param>
	[Then("the panel behind the dialog is dimmed in frame {string} compared to frame {string}")]
	public void Then_the_panel_behind_the_dialog_is_dimmed(string frameName, string otherFrameName)
	{
		var corner = CanvasAssert.PanelCorner();
		var now = new Region(ScenarioFrames.Get(_scenarioContext, frameName), corner,
			$"the corner of the panel in frame \"{frameName}\"");
		var before = new Region(ScenarioFrames.Get(_scenarioContext, otherFrameName), corner,
			$"the corner of the panel in frame \"{otherFrameName}\"");

		now.IsDimmedComparedTo(before);
	}

	// --------------------------------------------------------- teaching tips

	/// <summary>Opens a TeachingTip and lets it arrive.</summary>
	/// <param name="name">The Gherkin name of the tip.</param>
	/// <returns>A task that completes once the tip is over the panel and still.</returns>
	[When("the TeachingTip {string} is opened")]
	public async Task When_the_TeachingTip_is_opened(string name) =>
		await SetTipOpenAsync(name, open: true).ConfigureAwait(false);

	/// <summary>Closes a TeachingTip and lets it leave.</summary>
	/// <param name="name">The Gherkin name of the tip.</param>
	/// <returns>A task that completes once the tip has gone and the panel is still.</returns>
	[When("the TeachingTip {string} is dismissed")]
	public async Task When_the_TeachingTip_is_dismissed(string name) =>
		await SetTipOpenAsync(name, open: false).ConfigureAwait(false);

	/// <summary>Asserts that a TeachingTip reports itself open.</summary>
	/// <param name="name">The Gherkin name of the tip.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the TeachingTip {string} is open")]
	public async Task Then_the_TeachingTip_is_open(string name) =>
		(await IsTipOpenAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the TeachingTip \"{0}\" must be open", name);

	/// <summary>Asserts that a TeachingTip reports itself closed.</summary>
	/// <param name="name">The Gherkin name of the tip.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the TeachingTip {string} is closed")]
	public async Task Then_the_TeachingTip_is_closed(string name) =>
		(await IsTipOpenAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the TeachingTip \"{0}\" must be closed", name);

	/// <summary>Asserts the title a TeachingTip carries. Text content is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the tip.</param>
	/// <param name="title">The title it must carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the TeachingTip {string} carries the title {string}")]
	public async Task Then_the_TeachingTip_carries_the_title(string name, string title)
	{
		var actual = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			actual = Tip(name).Title ?? string.Empty).ConfigureAwait(false);

		actual.Should().Be(title, "the title of the TeachingTip \"{0}\" was asserted", name);
	}

	// --------------------------------------------------------------- infobars

	/// <summary>Asserts that an InfoBar reports itself open.</summary>
	/// <param name="name">The Gherkin name of the bar.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the InfoBar {string} is open")]
	public async Task Then_the_InfoBar_is_open(string name) =>
		(await IsBarOpenAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the InfoBar \"{0}\" must be open", name);

	/// <summary>Asserts that an InfoBar reports itself closed.</summary>
	/// <param name="name">The Gherkin name of the bar.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the InfoBar {string} is closed")]
	public async Task Then_the_InfoBar_is_closed(string name) =>
		(await IsBarOpenAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the InfoBar \"{0}\" must be closed", name);

	/// <summary>Taps the close button an InfoBar offers.</summary>
	/// <param name="name">The Gherkin name of the bar.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the close button of the InfoBar {string} is tapped")]
	public async Task When_the_InfoBar_close_button_is_tapped(string name)
	{
		FrameworkElement button = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var bar = ElementRegistry.Resolve(name);
			button = VisualTreeSearch.FindDescendantNamed(bar, InfoBarCloseButtonPart)
				?? throw new InvalidOperationException(
					$"The InfoBar \"{name}\" carries no \"{InfoBarCloseButtonPart}\" part.");
		}).ConfigureAwait(false);

		await TapAsync(button).ConfigureAwait(false);
	}

	/// <summary>Asserts how often an element raised an event a Popups requirement names.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the CloseButtonClick of the InfoBar {string} was raised {int} times")]
	public void Then_the_CloseButtonClick_was_raised(string name, int times) =>
		EventRecorder.Count(name, "CloseButtonClick").Should().Be(times,
			"the CloseButtonClick of \"{0}\" was asserted; the scenario recorded [{1}]",
			name, string.Join(", ", EventRecorder.Recorded));

	// ---------------------------------------------------------------- inner

	private static void ApplyDialogProperty(ContentDialog dialog, string property, string value)
	{
		var text = GherkinValue.Unquote(value);
		switch (property)
		{
			case "Title":
				dialog.Title = text;
				break;
			case "Content":
				dialog.Content = text;
				break;
			case "PrimaryButtonText":
				dialog.PrimaryButtonText = text;
				break;
			case "SecondaryButtonText":
				dialog.SecondaryButtonText = text;
				break;
			case "CloseButtonText":
				dialog.CloseButtonText = text;
				break;
			default:
				throw new NotSupportedException(
					$"A ContentDialog has no \"{property}\" the harness can set. It knows Title, Content, "
					+ "PrimaryButtonText, SecondaryButtonText and CloseButtonText.");
		}
	}

	private FlyoutBase Flyout(string flyoutName)
	{
		if (_scenarioContext.TryGetValue(FlyoutKeyPrefix + flyoutName, out var stored)
			&& stored is FlyoutBase flyout)
		{
			return flyout;
		}

		throw new InvalidOperationException(
			$"The scenario has no flyout named \"{flyoutName}\". A scenario attaches one with "
			+ "\"Given a Flyout named ...\" or \"Given a MenuFlyout named ...\".");
	}

	private Task<ContentDialogResult> DialogResult(string name)
	{
		if (_scenarioContext.TryGetValue(DialogResultKeyPrefix + name, out var stored)
			&& stored is Task<ContentDialogResult> result)
		{
			return result;
		}

		throw new InvalidOperationException(
			$"The ContentDialog \"{name}\" has not been shown, so it has promised no result.");
	}

	private static ContentDialog Dialog(string name) =>
		ElementRegistry.Resolve(name) as ContentDialog
			?? throw new NotSupportedException($"\"{name}\" is not a ContentDialog.");

	private static TeachingTip Tip(string name) =>
		ElementRegistry.Resolve(name) as TeachingTip
			?? throw new NotSupportedException($"\"{name}\" is not a TeachingTip.");

	private async Task<bool> IsFlyoutOpenAsync(string flyoutName)
	{
		var open = false;
		var flyout = Flyout(flyoutName);
		await TestTargetFixture.RunOnUIThreadAsync(() => open = flyout.IsOpen).ConfigureAwait(false);
		return open;
	}

	private static async Task<bool> IsPopupOpenAsync(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var popup = ElementRegistry.Resolve(name) as Popup
				?? throw new NotSupportedException($"\"{name}\" is not a Popup.");
			open = popup.IsOpen;
		}).ConfigureAwait(false);

		return open;
	}

	private static async Task SetTipOpenAsync(string name, bool open)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => Tip(name).IsOpen = open).ConfigureAwait(false);
		await SettleAsync(TipSettleDelay).ConfigureAwait(false);
	}

	private static async Task<bool> IsTipOpenAsync(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => open = Tip(name).IsOpen).ConfigureAwait(false);
		return open;
	}

	private static async Task<bool> IsBarOpenAsync(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var bar = ElementRegistry.Resolve(name) as InfoBar
				?? throw new NotSupportedException($"\"{name}\" is not an InfoBar.");
			open = bar.IsOpen;
		}).ConfigureAwait(false);

		return open;
	}

	private static async Task<FrameworkElement?> DialogSurfaceAsync(string name)
	{
		FrameworkElement? surface = null;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			surface = VisualTreeSearch.FindDescendantNamed(Dialog(name), DialogSurfacePart)).ConfigureAwait(false);
		return surface;
	}

	private async Task<Region> OpenPopupRegionAsync()
	{
		FrameworkElement? content = null;
		await TestTargetFixture.RunOnUIThreadAsync(() => content = PopupElements.OpenPopupContent())
			.ConfigureAwait(false);

		var element = content ?? throw new InvalidOperationException(
			"No popup is open, so there is nothing over the panel to look at.");
		var bounds = await DeviceRect.OfAsync(element).ConfigureAwait(false);
		return new Region(ScenarioFrames.Current(_scenarioContext), bounds, "the open popup");
	}

	private static async Task TapDialogPartAsync(string name, string partName)
	{
		FrameworkElement button = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			button = VisualTreeSearch.FindDescendantNamed(Dialog(name), partName)
				?? throw new InvalidOperationException(
					$"The ContentDialog \"{name}\" carries no \"{partName}\" part. A button a dialog was "
					+ "given no text for is not in its template.");
		}).ConfigureAwait(false);

		await TapAsync(button).ConfigureAwait(false);
	}

	private static async Task<FrameworkElement> OpenFlyoutPresenterAsync()
	{
		FrameworkElement? presenter = null;
		await TestTargetFixture.RunOnUIThreadAsync(() => presenter = PopupElements.OpenPopupContent())
			.ConfigureAwait(false);

		return presenter ?? throw new InvalidOperationException(
			"No flyout is open, so there is nothing over the panel to look at.");
	}

	private static async Task<(double Top, double Bottom, double Width, double Height)> PresenterBoundsAsync(
		FrameworkElement presenter)
	{
		var bounds = default(Windows.Foundation.Rect);
		await TestTargetFixture.RunOnUIThreadAsync(() => bounds = presenter
			.TransformToVisual(null)
			.TransformBounds(new Windows.Foundation.Rect(0, 0, presenter.ActualWidth, presenter.ActualHeight)))
			.ConfigureAwait(false);

		return (bounds.Top, bounds.Bottom, bounds.Width, bounds.Height);
	}

	private static async Task<double> OpenFlyoutScrollableHeightAsync()
	{
		var presenter = await OpenFlyoutPresenterAsync().ConfigureAwait(false);
		var scrollable = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() => scrollable = FlyoutScroller(presenter).ScrollableHeight)
			.ConfigureAwait(false);

		return scrollable;
	}

	private async Task<Region> OpenFlyoutInteriorAsync()
	{
		var presenter = await OpenFlyoutPresenterAsync().ConfigureAwait(false);
		var bounds = await DeviceRect.OfAsync(presenter).ConfigureAwait(false);

		// The presenter draws a border and rounds its corners, and a shadow may fall just inside
		// its rectangle, so the surface a requirement is about starts a little way in.
		var inset = Math.Max(CanvasAssert.SurfaceInset,
			Math.Min(bounds.Width, bounds.Height) / CanvasAssert.SurfaceInsetDivisor);
		return new Region(ScenarioFrames.Current(_scenarioContext), bounds.Inset(inset),
			"the interior of the open flyout");
	}

	private static ScrollViewer FlyoutScroller(FrameworkElement presenter) =>
		VisualTreeSearch.FindDescendant<ScrollViewer>(presenter)
			?? throw new InvalidOperationException(
				"The open flyout's presenter holds no ScrollViewer, so its items cannot be scrolled.");

	private static async Task TapAsync(FrameworkElement element)
	{
		var bounds = await DeviceRect.OfAsync(element).ConfigureAwait(false);
		var (x, y) = bounds.Center;

		await SettleAsync(BeforeTapDelay).ConfigureAwait(false);
		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task SettleAsync(TimeSpan delay)
	{
		await Task.Delay(delay, TestContext.Current.CancellationToken).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}
}
