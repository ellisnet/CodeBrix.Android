using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The Navigation group's steps: the controls that decide WHICH of several things the panel is
/// showing - a Frame and its back stack, a NavigationView and its pane, a SplitView, an
/// Expander, a ScrollViewer under a finger, and a TwoPaneView. Everything these controls do to
/// get from one look to the next is animated, so the waits are named settings on this class
/// rather than numbers in a feature file.
/// </summary>
[Binding]
public sealed class NavigationSteps
{
	/// <summary>
	/// How long an Expander is given to finish opening or closing. The template moves its
	/// content with a spline animation of a third of a second, so a frame taken straight after
	/// IsExpanded changed shows the content halfway there.
	/// </summary>
	public static readonly TimeSpan ExpandAnimationDelay = TimeSpan.FromMilliseconds(600);

	/// <summary>
	/// How long a pane is given to finish sliding open or shut. A SplitView's pane and a
	/// NavigationView's pane are both animated.
	/// </summary>
	public static readonly TimeSpan PaneAnimationDelay = TimeSpan.FromMilliseconds(500);

	/// <summary>How long a Frame is given to finish its navigation transition.</summary>
	public static readonly TimeSpan NavigationDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>
	/// How long a scrolled panel is given to come to rest after the finger has left it. A touch
	/// drag hands the scroll over to inertia, so the offset is still changing at the moment the
	/// finger lifts.
	/// </summary>
	public static readonly TimeSpan ScrollSettleDelay = TimeSpan.FromMilliseconds(900);

	/// <summary>How long a finger rests on the panel before it starts moving.</summary>
	public static readonly TimeSpan DragPressDelay = TimeSpan.FromMilliseconds(80);

	/// <summary>How long the panel is given between the moves of a drag.</summary>
	public static readonly TimeSpan DragStepDelay = TimeSpan.FromMilliseconds(16);

	/// <summary>How long the panel is left alone before a tap that targets a template part.</summary>
	public static readonly TimeSpan BeforePartTapDelay = TimeSpan.FromMilliseconds(250);

	/// <summary>How many moves a drag is delivered in, so that it looks like a finger.</summary>
	public const int DragSteps = 10;

	/// <summary>The template part a NavigationView draws its pane toggle button as.</summary>
	public const string NavigationTogglePart = "TogglePaneButton";

	/// <summary>The template part an Expander draws its header as.</summary>
	public const string ExpanderHeaderPart = "ExpanderHeader";

	/// <summary>The template part an Expander draws its content in.</summary>
	public const string ExpanderContentPart = "ExpanderContent";

	private const int PointerId = 0;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public NavigationSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the Navigation group's controls, before the first scenario.
	/// </summary>
	[BeforeTestRun(Order = 12)]
	public static void Register_the_navigation_controls() => NavigationElements.Register();

	/// <summary>Forgets what the previous scenario said its pages show.</summary>
	[BeforeScenario(Order = 1)]
	public static void Forget_the_previous_scenarios_pages() => NavigationPages.Clear();

	// ---------------------------------------------------------------- Frame

	/// <summary>Says what one of the pages a Frame can navigate to shows.</summary>
	/// <param name="key">The page word - "first", "second" or "third".</param>
	/// <param name="panelName">The name the scenario refers to the page's panel by.</param>
	/// <param name="color">The colour the panel is painted.</param>
	[Given("the page {string} shows a panel named {string} painted {string}")]
	public void Given_the_page_shows_a_panel(string key, string panelName, Color color) =>
		NavigationPages.Describe(key, panelName, color);

	/// <summary>Navigates a Frame to one of the pages the scenario described.</summary>
	/// <param name="name">The Gherkin name of the Frame.</param>
	/// <param name="key">The page word.</param>
	/// <returns>A task that completes once the page is showing.</returns>
	[Given("the Frame {string} navigates to the page {string}")]
	[When("the Frame {string} navigates to the page {string}")]
	public async Task When_the_Frame_navigates_to_the_page(string name, string key)
	{
		var navigated = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var frame = NavigationElements.AsFrame(ElementRegistry.Resolve(name), "Navigate");
			navigated = frame.Navigate(NavigationPages.TypeOf(key));
		}).ConfigureAwait(false);

		navigated.Should().BeTrue("the Frame \"{0}\" must be able to navigate to the page \"{1}\"", name, key);
		await SettleAsync(NavigationDelay).ConfigureAwait(false);
	}

	/// <summary>Sends a Frame back to the page it was showing before.</summary>
	/// <param name="name">The Gherkin name of the Frame.</param>
	/// <returns>A task that completes once the earlier page is showing.</returns>
	[When("the Frame {string} goes back")]
	public async Task When_the_Frame_goes_back(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			NavigationElements.AsFrame(ElementRegistry.Resolve(name), "GoBack").GoBack()).ConfigureAwait(false);

		await SettleAsync(NavigationDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts which page a Frame is showing. The current page is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the Frame.</param>
	/// <param name="key">The page word it must be showing.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Frame {string} is showing the page {string}")]
	public async Task Then_the_Frame_is_showing_the_page(string name, string key)
	{
		var showing = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			showing = NavigationPages.KeyOf(
				NavigationElements.AsFrame(ElementRegistry.Resolve(name), "CurrentSourcePageType")
					.CurrentSourcePageType)).ConfigureAwait(false);

		showing.Should().Be(key, "the Frame \"{0}\" must be showing the page \"{1}\"", name, key);
	}

	/// <summary>Asserts that a Frame has somewhere to go back to.</summary>
	/// <param name="name">The Gherkin name of the Frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Frame {string} can go back")]
	public async Task Then_the_Frame_can_go_back(string name) =>
		(await CanGoBackAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the Frame \"{0}\" must be able to go back", name);

	/// <summary>Asserts that a Frame has nowhere to go back to.</summary>
	/// <param name="name">The Gherkin name of the Frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Frame {string} cannot go back")]
	public async Task Then_the_Frame_cannot_go_back(string name) =>
		(await CanGoBackAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the Frame \"{0}\" must have nowhere to go back to", name);

	/// <summary>Asserts how many pages a Frame's back stack holds.</summary>
	/// <param name="name">The Gherkin name of the Frame.</param>
	/// <param name="depth">How deep the stack must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the back stack of {string} holds {int} pages")]
	public async Task Then_the_back_stack_holds_pages(string name, int depth)
	{
		var actual = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			actual = NavigationElements.AsFrame(ElementRegistry.Resolve(name), "BackStackDepth").BackStackDepth)
			.ConfigureAwait(false);

		actual.Should().Be(depth, "the back stack of the Frame \"{0}\" was asserted", name);
	}

	// ------------------------------------------------------- NavigationView

	/// <summary>
	/// Shows a NavigationView whose menu items each stand for a panel of a known colour, so
	/// that "tapping the item showed its page" is something a person can see on the panel.
	/// </summary>
	/// <param name="name">The name the scenario refers to the NavigationView by.</param>
	/// <param name="width">The control's width in logical pixels.</param>
	/// <param name="height">The control's height in logical pixels.</param>
	/// <param name="items">A Name/Label/Panel/Colour table, one row per menu item.</param>
	/// <returns>A task that completes once the NavigationView is showing.</returns>
	[Given("the application shows a NavigationView named {string} {int} by {int} with items:")]
	public async Task Given_the_application_shows_a_NavigationView_with_items(string name, int width, int height,
		DataTable items)
	{
		ArgumentNullException.ThrowIfNull(items);

		var rows = items.Rows
			.Select(row => (Name: row["Name"], Label: row["Label"], Panel: row["Panel"], Colour: row["Colour"]))
			.ToArray();

		var element = await ElementFactory.CreateAsync("NavigationView", name, new[]
		{
			new KeyValuePair<string, string>("Width", Number(width)),
			new KeyValuePair<string, string>("Height", Number(height)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var view = NavigationElements.AsNavigationView(element, "menu items");
			var panels = new Dictionary<NavigationViewItem, Border>();

			foreach (var row in rows)
			{
				var item = new NavigationViewItem
				{
					Name = row.Name,
					Content = row.Label,
				};

				ElementRegistry.Register(row.Name, item);
				panels[item] = NavigationElements.BuildPanel(row.Panel, Colors.Parse(row.Colour));
				view.MenuItems.Add(item);
			}

			view.SelectionChanged += (_, args) =>
			{
				if (args.SelectedItem is NavigationViewItem selected && panels.TryGetValue(selected, out var panel))
				{
					view.Content = panel;
				}
			};
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>Taps the button a NavigationView draws for opening and closing its pane.</summary>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	/// <returns>A task that completes once the pane has finished moving.</returns>
	[When("the pane toggle of the NavigationView {string} is tapped")]
	public async Task When_the_pane_toggle_is_tapped(string name)
	{
		ElementRegistry.Resolve(name);
		await TapPartAsync(NavigationTogglePart).ConfigureAwait(false);
		await SettleAsync(PaneAnimationDelay).ConfigureAwait(false);
	}

	/// <summary>Taps one of a NavigationView's menu items, as a finger would.</summary>
	/// <param name="itemName">The Gherkin name of the menu item.</param>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	/// <returns>A task that completes once the tap has been delivered.</returns>
	[When("the item {string} of the NavigationView {string} is tapped")]
	public async Task When_the_item_of_the_NavigationView_is_tapped(string itemName, string name)
	{
		ElementRegistry.Resolve(name);
		await TapPartAsync(itemName).ConfigureAwait(false);
		await SettleAsync(NavigationDelay).ConfigureAwait(false);
	}

	/// <summary>Selects one of a NavigationView's menu items without touching the panel.</summary>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	/// <param name="itemName">The Gherkin name of the menu item to select.</param>
	/// <returns>A task that completes once the selection has been applied.</returns>
	[Given("the NavigationView {string} selects {string}")]
	[When("the NavigationView {string} selects {string}")]
	public async Task When_the_NavigationView_selects(string name, string itemName)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var view = NavigationElements.AsNavigationView(ElementRegistry.Resolve(name), "SelectedItem");
			view.SelectedItem = ElementRegistry.Resolve(itemName);
		}).ConfigureAwait(false);

		await SettleAsync(NavigationDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts that a NavigationView told the application its selection had changed.</summary>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	[Then("the NavigationView {string} reported a selection change")]
	public void Then_the_NavigationView_reported_a_selection_change(string name) =>
		EventRecorder.Count(name, NavigationElements.SelectionChangedEvent).Should().BeGreaterThan(0,
			"the NavigationView \"{0}\" must report a selection change; the scenario recorded [{1}]",
			name, string.Join(", ", EventRecorder.Recorded));

	/// <summary>
	/// Captures a frame and remembers where the page a NavigationView is showing was at that
	/// moment: shutting the pane hands the pane's room to the page, so the page is a different
	/// size in every frame and its rectangle has to be taken when the frame is.
	/// </summary>
	/// <param name="pageName">The Gherkin name of the page's panel.</param>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the page {string} of the NavigationView {string} is captured as {string}")]
	public async Task When_the_page_of_the_NavigationView_is_captured_as(string pageName, string name,
		string captureName)
	{
		ElementRegistry.Resolve(name);
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, pageName, name).ConfigureAwait(false);
	}

	/// <summary>Asserts that a NavigationView's page had more room in one capture than in another.</summary>
	/// <param name="pageName">The Gherkin name of the page's panel.</param>
	/// <param name="wideCapture">The capture in which it must be wider.</param>
	/// <param name="narrowCapture">The capture in which it must be narrower.</param>
	[Then("the NavigationView page {string} was wider in {string} than in {string}")]
	public void Then_the_NavigationView_page_was_wider_in(string pageName, string wideCapture, string narrowCapture)
	{
		var wide = CapturedParts.Get(_scenarioContext, wideCapture);
		var narrow = CapturedParts.Get(_scenarioContext, narrowCapture);

		wide.Part.Width.Should().BeGreaterThan(narrow.Part.Width,
			"the page \"{0}\" must have more room in capture \"{1}\" ({2}) than in capture \"{3}\" ({4})",
			pageName, wideCapture, wide.Part, narrowCapture, narrow.Part);
	}

	/// <summary>
	/// Asserts that the room a NavigationView's page fills in one capture was showing something
	/// else in another - which is what "the page took the pane's room" looks like on the panel.
	/// </summary>
	/// <param name="pageName">The Gherkin name of the page's panel.</param>
	/// <param name="captureName">The capture the page's rectangle is taken from.</param>
	/// <param name="otherCapture">The capture that must look different there.</param>
	[Then("where the NavigationView page {string} is in {string} looked different in {string}")]
	public void Then_where_the_NavigationView_page_looked_different(string pageName, string captureName,
		string otherCapture)
	{
		var capture = CapturedParts.Get(_scenarioContext, captureName);
		var other = CapturedParts.Get(_scenarioContext, otherCapture);
		var now = capture.PartRegion($"where the page \"{pageName}\" is in capture \"{captureName}\"");
		var was = new Region(other.Frame, capture.Part, $"that same place in capture \"{otherCapture}\"");
		now.DiffersFrom(was);
	}

	/// <summary>Asserts which menu item a NavigationView has selected.</summary>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	/// <param name="itemName">The Gherkin name of the item that must be selected.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the NavigationView {string} has selected {string}")]
	public async Task Then_the_NavigationView_has_selected(string name, string itemName)
	{
		var selected = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var view = NavigationElements.AsNavigationView(ElementRegistry.Resolve(name), "SelectedItem");
			selected = view.SelectedItem is FrameworkElement item ? item.Name : "nothing";
		}).ConfigureAwait(false);

		selected.Should().Be(itemName, "the selected item of the NavigationView \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that a NavigationView has selected nothing at all.</summary>
	/// <param name="name">The Gherkin name of the NavigationView.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the NavigationView {string} has selected nothing")]
	public async Task Then_the_NavigationView_has_selected_nothing(string name)
	{
		var selected = true;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			selected = NavigationElements.AsNavigationView(ElementRegistry.Resolve(name), "SelectedItem")
				.SelectedItem is not null).ConfigureAwait(false);

		selected.Should().BeFalse("the NavigationView \"{0}\" must have selected nothing", name);
	}

	// ------------------------------------------------------------ SplitView

	/// <summary>
	/// Shows a SplitView whose pane and whose content are each one panel of a known colour.
	/// </summary>
	/// <param name="name">The name the scenario refers to the SplitView by.</param>
	/// <param name="width">The control's width in logical pixels.</param>
	/// <param name="height">The control's height in logical pixels.</param>
	/// <param name="paneName">The name the scenario refers to the pane's panel by.</param>
	/// <param name="paneColor">The colour the pane is painted.</param>
	/// <param name="contentName">The name the scenario refers to the content panel by.</param>
	/// <param name="contentColor">The colour the content is painted.</param>
	/// <returns>A task that completes once the SplitView is showing.</returns>
	[Given("the application shows a SplitView named {string} {int} by {int} with a pane named {string} painted {string} and content named {string} painted {string}")]
	public async Task Given_the_application_shows_a_SplitView(string name, int width, int height,
		string paneName, Color paneColor, string contentName, Color contentColor)
	{
		var element = await ElementFactory.CreateAsync("SplitView", name, new[]
		{
			new KeyValuePair<string, string>("Width", Number(width)),
			new KeyValuePair<string, string>("Height", Number(height)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var split = NavigationElements.AsSplitView(element, "a pane");
			split.Pane = NavigationElements.BuildPanel(paneName, paneColor);
			split.Content = NavigationElements.BuildPanel(contentName, contentColor);
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>Opens the pane of a SplitView or a NavigationView and lets it finish sliding.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the pane has finished moving.</returns>
	[Given("the pane of {string} is opened")]
	[When("the pane of {string} is opened")]
	public async Task When_the_pane_is_opened(string name) => await SetPaneAsync(name, true).ConfigureAwait(false);

	/// <summary>Closes the pane of a SplitView or a NavigationView and lets it finish sliding.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the pane has finished moving.</returns>
	[Given("the pane of {string} is closed")]
	[When("the pane of {string} is closed")]
	public async Task When_the_pane_is_closed(string name) => await SetPaneAsync(name, false).ConfigureAwait(false);

	/// <summary>Asserts that a pane is open. Whether a pane is open is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the pane of {string} is open")]
	public async Task Then_the_pane_is_open(string name) =>
		(await IsPaneOpenAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the pane of \"{0}\" must be open", name);

	/// <summary>Asserts that a pane is shut.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the pane of {string} is shut")]
	public async Task Then_the_pane_is_shut(string name) =>
		(await IsPaneOpenAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the pane of \"{0}\" must be shut", name);

	/// <summary>
	/// Asserts that the strip along one edge of a control is painted one colour - which is how
	/// a compact pane, a sliver of itself left showing beside the content, is seen.
	/// </summary>
	/// <param name="width">How many pixels wide the strip is.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="color">The colour the strip must be.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the leftmost {int} pixels of {string} are uniformly {string}")]
	public async Task Then_the_leftmost_pixels_of_are_uniformly(int width, string name, Color color)
	{
		var region = await ScenarioFrames.RegionAsync(_scenarioContext, name).ConfigureAwait(false);
		region.LeftStrip(width).IsUniformly(color);
	}

	// ------------------------------------------------------------- Expander

	/// <summary>
	/// Shows an Expander whose header is a line of text and whose content is one panel of a
	/// known colour. The Expander is put at the top of the panel, so that opening it makes it
	/// grow downwards rather than move.
	/// </summary>
	/// <param name="name">The name the scenario refers to the Expander by.</param>
	/// <param name="width">The control's width in logical pixels.</param>
	/// <param name="header">The text the header shows.</param>
	/// <param name="contentName">The name the scenario refers to the content panel by.</param>
	/// <param name="contentHeight">The content panel's height in logical pixels.</param>
	/// <param name="color">The colour the content panel is painted.</param>
	/// <returns>A task that completes once the Expander is showing.</returns>
	[Given("the application shows an Expander named {string} {int} wide headed {string} with content named {string} {int} tall painted {string}")]
	public async Task Given_the_application_shows_an_Expander(string name, int width, string header,
		string contentName, int contentHeight, Color color)
	{
		var element = await ElementFactory.CreateAsync("Expander", name, new[]
		{
			new KeyValuePair<string, string>("Width", Number(width)),
			new KeyValuePair<string, string>("VerticalAlignment", "Top"),
			new KeyValuePair<string, string>("Margin", "40"),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var expander = NavigationElements.AsExpander(element, "a header");
			expander.Header = header;

			// A panel with nothing in it has no natural width, and the template's content
			// presenter is left-aligned, so without this the content would be nothing wide.
			expander.HorizontalContentAlignment = HorizontalAlignment.Stretch;

			var content = NavigationElements.BuildPanel(contentName, color);
			content.Height = contentHeight;
			expander.Content = content;
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>Opens an Expander and lets its content finish moving into place.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes once the Expander has settled.</returns>
	[Given("the Expander {string} is expanded")]
	[When("the Expander {string} is expanded")]
	public async Task When_the_Expander_is_expanded(string name) =>
		await SetExpandedAsync(name, true).ConfigureAwait(false);

	/// <summary>Closes an Expander and lets its content finish moving away.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes once the Expander has settled.</returns>
	[When("the Expander {string} is collapsed")]
	public async Task When_the_Expander_is_collapsed(string name) =>
		await SetExpandedAsync(name, false).ConfigureAwait(false);

	/// <summary>Taps an Expander's header, which is what a finger reaches for.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes once the Expander has settled.</returns>
	[When("the header of the Expander {string} is tapped")]
	public async Task When_the_header_of_the_Expander_is_tapped(string name)
	{
		ElementRegistry.Resolve(name);
		await TapPartAsync(ExpanderHeaderPart).ConfigureAwait(false);
		await SettleAsync(ExpandAnimationDelay).ConfigureAwait(false);
	}

	/// <summary>
	/// Captures a frame and remembers where the open Expander's content was at that moment. A
	/// shut Expander's content has no rectangle at all, so the rectangle the requirement is
	/// about has to be taken while the Expander is open.
	/// </summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the content of the Expander {string} is captured as {string}")]
	public async Task When_the_content_of_the_Expander_is_captured_as(string name, string captureName)
	{
		ElementRegistry.Resolve(name);
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, ExpanderContentPart, name)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that the place an open Expander's content occupies was nothing but panel
	/// background in another frame - which is what "the content was folded away" looks like.
	/// </summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <param name="captureName">The capture the content's rectangle is taken from.</param>
	/// <param name="frameName">The frame that must show nothing there.</param>
	[Then("where the content of the Expander {string} is in {string} is blank in frame {string}")]
	public void Then_where_the_content_of_the_Expander_is_blank_in_frame(string name, string captureName,
		string frameName)
	{
		var capture = CapturedParts.Get(_scenarioContext, captureName);
		var frame = ScenarioFrames.Get(_scenarioContext, frameName);
		new Region(frame, capture.Part,
			$"where the content of the Expander \"{name}\" is in capture \"{captureName}\", seen in frame \"{frameName}\"")
			.IsBlank();
	}

	/// <summary>Asserts that an Expander is open. Whether it is open is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Expander {string} is open")]
	public async Task Then_the_Expander_is_open(string name) =>
		(await IsExpandedAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the Expander \"{0}\" must be open", name);

	/// <summary>Asserts that an Expander is shut.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the Expander {string} is shut")]
	public async Task Then_the_Expander_is_shut(string name) =>
		(await IsExpandedAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the Expander \"{0}\" must be shut", name);

	/// <summary>Asserts that an Expander's content is laid out on the panel.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the content of the Expander {string} is showing")]
	public async Task Then_the_content_of_the_Expander_is_showing(string name)
	{
		var height = await ContentHeightAsync(name).ConfigureAwait(false);
		height.Should().BeGreaterThan(0.0, "the content of the Expander \"{0}\" must be showing", name);
	}

	/// <summary>Asserts that an Expander's content takes up no room on the panel.</summary>
	/// <param name="name">The Gherkin name of the Expander.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the content of the Expander {string} is not showing")]
	public async Task Then_the_content_of_the_Expander_is_not_showing(string name)
	{
		var height = await ContentHeightAsync(name).ConfigureAwait(false);
		height.Should().Be(0.0, "the content of the Expander \"{0}\" must not be showing", name);
	}

	// --------------------------------------------------------- ScrollViewer

	/// <summary>
	/// Shows a ScrollViewer holding a column of blocks taller than the viewer itself, which is
	/// the only shape in which a requirement about scrolling can be stated. The first block is
	/// registered under its own name, because where it sits is how far the content has moved.
	/// </summary>
	/// <param name="name">The name the scenario refers to the ScrollViewer by.</param>
	/// <param name="width">The viewer's width in logical pixels.</param>
	/// <param name="height">The viewer's height in logical pixels.</param>
	/// <param name="blocks">How many blocks the column holds.</param>
	/// <param name="blockHeight">Each block's height in logical pixels.</param>
	/// <param name="firstName">The name the scenario refers to the first block by.</param>
	/// <returns>A task that completes once the ScrollViewer is showing.</returns>
	[Given("the application shows a ScrollViewer named {string} {int} by {int} holding {int} blocks {int} tall, the first named {string}")]
	public async Task Given_the_application_shows_a_ScrollViewer(string name, int width, int height,
		int blocks, int blockHeight, string firstName)
	{
		var element = await ElementFactory.CreateAsync("ScrollViewer", name, new[]
		{
			new KeyValuePair<string, string>("Width", Number(width)),
			new KeyValuePair<string, string>("Height", Number(height)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var viewer = NavigationElements.AsScrollViewer(element, "content");
			var column = new StackPanel { Orientation = Orientation.Vertical };

			for (var index = 0; index < blocks; index++)
			{
				var block = new Border
				{
					Height = blockHeight,
					Background = new SolidColorBrush(index % 2 == 0 ? Colors.Parse("Red") : Colors.Parse("Blue")),
				};

				if (index == 0)
				{
					block.Name = firstName;
					ElementRegistry.Register(firstName, block);
				}

				column.Children.Add(block);
			}

			viewer.Content = column;
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>
	/// Captures a frame and remembers where the ScrollViewer's first block was at that moment,
	/// because a scrolled block is somewhere else in the next frame.
	/// </summary>
	/// <param name="blockName">The Gherkin name of the first block.</param>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("the block {string} of the ScrollViewer {string} is captured as {string}")]
	public async Task When_the_block_of_the_ScrollViewer_is_captured_as(string blockName, string name,
		string captureName) =>
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, blockName, name).ConfigureAwait(false);

	/// <summary>
	/// Drags a ScrollViewer's content upwards with one finger: press, several moves, and lift -
	/// the three calls a touch panel actually delivers - and then waits for the panel to come
	/// to rest, because the lift hands the scroll over to inertia.
	/// </summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="distance">How many device pixels upwards the finger travels.</param>
	/// <returns>A task that completes once the content has come to rest.</returns>
	[When("the ScrollViewer {string} is dragged {int} pixels up")]
	public async Task When_the_ScrollViewer_is_dragged_pixels_up(string name, int distance) =>
		await DragAsync(name, distance).ConfigureAwait(false);

	private static async Task DragAsync(string name, int distance)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
		var (x, startY) = bounds.Center;

		TestTargetFixture.Session.TouchPress(PointerId, x, startY);
		await DelayAsync(DragPressDelay).ConfigureAwait(false);

		for (var step = 1; step <= DragSteps; step++)
		{
			var y = startY - (int) Math.Round(distance * (step / (double) DragSteps));
			TestTargetFixture.Session.TouchMove(PointerId, x, y);
			await DelayAsync(DragStepDelay).ConfigureAwait(false);
		}

		TestTargetFixture.Session.TouchRelease(PointerId, x, startY - distance);
		await SettleAsync(ScrollSettleDelay).ConfigureAwait(false);
	}

	/// <summary>
	/// Drags a ScrollViewer's content downwards with one finger, which is how a finger asks to
	/// see what is above what it is looking at.
	/// </summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="distance">How many device pixels downwards the finger travels.</param>
	/// <returns>A task that completes once the content has come to rest.</returns>
	[When("the ScrollViewer {string} is dragged {int} pixels down")]
	public async Task When_the_ScrollViewer_is_dragged_pixels_down(string name, int distance) =>
		await DragAsync(name, -distance).ConfigureAwait(false);

	/// <summary>Scrolls a ScrollViewer to an offset without touching the panel.</summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="offset">The offset to scroll to.</param>
	/// <returns>A task that completes once the content has come to rest.</returns>
	[Given("the ScrollViewer {string} is scrolled to {float}")]
	[When("the ScrollViewer {string} is scrolled to {float}")]
	public async Task When_the_ScrollViewer_is_scrolled_to(string name, float offset)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			NavigationElements.AsScrollViewer(ElementRegistry.Resolve(name), "ChangeView")
				.ChangeView(null, offset, null, true)).ConfigureAwait(false);

		await SettleAsync(ScrollSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Asserts that a ScrollViewer has scrolled past a point.</summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="offset">The offset it must be past.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the VerticalOffset of {string} is more than {float}")]
	public async Task Then_the_VerticalOffset_is_more_than(string name, float offset) =>
		(await VerticalOffsetAsync(name).ConfigureAwait(false)).Should().BeGreaterThan(offset,
			"the VerticalOffset of the ScrollViewer \"{0}\" was asserted", name);

	/// <summary>Asserts exactly how far a ScrollViewer has scrolled.</summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="offset">The offset it must be at.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the VerticalOffset of {string} is {float}")]
	public async Task Then_the_VerticalOffset_is(string name, float offset) =>
		(await VerticalOffsetAsync(name).ConfigureAwait(false)).Should().BeApproximately(offset, 0.5,
			"the VerticalOffset of the ScrollViewer \"{0}\" was asserted", name);

	/// <summary>Asserts that a ScrollViewer holds more than it can show at once.</summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ScrollViewer {string} holds more than it can show")]
	public async Task Then_the_ScrollViewer_holds_more_than_it_can_show(string name)
	{
		var extent = 0.0;
		var viewport = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var viewer = NavigationElements.AsScrollViewer(ElementRegistry.Resolve(name), "ExtentHeight");
			extent = viewer.ExtentHeight;
			viewport = viewer.ViewportHeight;
		}).ConfigureAwait(false);

		extent.Should().BeGreaterThan(viewport,
			"the ScrollViewer \"{0}\" must hold more than one viewport of content", name);
	}

	/// <summary>Asserts that a ScrollViewer's content moved up between two captures.</summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="fromCapture">The earlier capture.</param>
	/// <param name="toCapture">The later capture.</param>
	/// <param name="distance">The fewest pixels the content must have moved.</param>
	[Then("the content of the ScrollViewer {string} moved up from {string} to {string} by at least {int} pixels")]
	public void Then_the_content_of_the_ScrollViewer_moved_up(string name, string fromCapture, string toCapture,
		int distance)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var moved = before.Part.Y - after.Part.Y;

		moved.Should().BeGreaterThanOrEqualTo(distance,
			"the content of the ScrollViewer \"{0}\" must have moved up: its first block was at {1} in \"{2}\" and at {3} in \"{4}\"",
			name, before.Part, fromCapture, after.Part, toCapture);
	}

	/// <summary>Asserts that a ScrollViewer showed something different after it was scrolled.</summary>
	/// <param name="name">The Gherkin name of the ScrollViewer.</param>
	/// <param name="fromCapture">The earlier capture.</param>
	/// <param name="toCapture">The later capture.</param>
	[Then("the ScrollViewer {string} shows something different in {string} than in {string}")]
	public void Then_the_ScrollViewer_shows_something_different(string name, string toCapture, string fromCapture)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var was = before.ContainerRegion($"the ScrollViewer \"{name}\" in capture \"{fromCapture}\"");
		var now = new Region(after.Frame, before.Container,
			$"the ScrollViewer \"{name}\" in capture \"{toCapture}\"");
		now.DiffersFrom(was);
	}

	// --------------------------------------------------------- TwoPaneView

	/// <summary>Shows a TwoPaneView whose two panes are each one panel of a known colour.</summary>
	/// <param name="name">The name the scenario refers to the TwoPaneView by.</param>
	/// <param name="width">The control's width in logical pixels.</param>
	/// <param name="height">The control's height in logical pixels.</param>
	/// <param name="firstName">The name the scenario refers to the first pane's panel by.</param>
	/// <param name="firstColor">The colour the first pane is painted.</param>
	/// <param name="secondName">The name the scenario refers to the second pane's panel by.</param>
	/// <param name="secondColor">The colour the second pane is painted.</param>
	/// <returns>A task that completes once the TwoPaneView is showing.</returns>
	[Given("the application shows a TwoPaneView named {string} {int} by {int} with panes named {string} painted {string} and {string} painted {string}")]
	public async Task Given_the_application_shows_a_TwoPaneView(string name, int width, int height,
		string firstName, Color firstColor, string secondName, Color secondColor)
	{
		var element = await ElementFactory.CreateAsync("TwoPaneView", name, new[]
		{
			new KeyValuePair<string, string>("Width", Number(width)),
			new KeyValuePair<string, string>("Height", Number(height)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var view = NavigationElements.AsTwoPaneView(element, "panes");

			// Pane1Length defaults to Auto, and a panel with nothing in it has no natural size,
			// so a pane left on the default would be nothing wide however much room there is.
			// An equal share is what makes "the panes are side by side" a statement a person
			// can check on the panel.
			view.Pane1Length = new GridLength(1, GridUnitType.Star);
			view.Pane2Length = new GridLength(1, GridUnitType.Star);
			view.Pane1 = NavigationElements.BuildPanel(firstName, firstColor);
			view.Pane2 = NavigationElements.BuildPanel(secondName, secondColor);
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
	}

	/// <summary>Asserts which way a TwoPaneView has decided to lay its panes out.</summary>
	/// <param name="name">The Gherkin name of the TwoPaneView.</param>
	/// <param name="mode">"Wide", "Tall" or "SinglePane".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the TwoPaneView {string} is in {string} mode")]
	public async Task Then_the_TwoPaneView_is_in_mode(string name, string mode)
	{
		var actual = TwoPaneViewMode.SinglePane;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			actual = NavigationElements.AsTwoPaneView(ElementRegistry.Resolve(name), "Mode").Mode)
			.ConfigureAwait(false);

		actual.Should().Be(GherkinValue.ToEnum<TwoPaneViewMode>(mode),
			"the mode of the TwoPaneView \"{0}\" was asserted", name);
	}

	// --------------------------------------------------------------- inner

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static Task DelayAsync(TimeSpan delay) => Task.Delay(delay, TestContext.Current.CancellationToken);

	private static async Task SettleAsync(TimeSpan delay)
	{
		await DelayAsync(delay).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task TapPartAsync(string partName)
	{
		await DelayAsync(BeforePartTapDelay).ConfigureAwait(false);

		var part = await DeviceRect.OfAsync(ElementRegistry.Resolve(partName)).ConfigureAwait(false);
		var (x, y) = part.Center;
		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task<bool> CanGoBackAsync(string name)
	{
		var canGoBack = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			canGoBack = NavigationElements.AsFrame(ElementRegistry.Resolve(name), "CanGoBack").CanGoBack)
			.ConfigureAwait(false);

		return canGoBack;
	}

	private static async Task SetPaneAsync(string name, bool open)
	{
		await ElementFactory.ApplyAsync(name, "IsPaneOpen",
			open.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
		await SettleAsync(PaneAnimationDelay).ConfigureAwait(false);
	}

	private static async Task<bool> IsPaneOpenAsync(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			open = ElementRegistry.Resolve(name) switch
			{
				SplitView split => split.IsPaneOpen,
				NavigationView view => view.IsPaneOpen,
				var other => throw new NotSupportedException(
					$"A {other.GetType().Name} named \"{name}\" has no pane the harness can ask about."),
			};
		}).ConfigureAwait(false);

		return open;
	}

	private static async Task SetExpandedAsync(string name, bool expanded)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			NavigationElements.AsExpander(ElementRegistry.Resolve(name), "IsExpanded").IsExpanded = expanded)
			.ConfigureAwait(false);
		await SettleAsync(ExpandAnimationDelay).ConfigureAwait(false);
	}

	private static async Task<bool> IsExpandedAsync(string name)
	{
		var expanded = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			expanded = NavigationElements.AsExpander(ElementRegistry.Resolve(name), "IsExpanded").IsExpanded)
			.ConfigureAwait(false);

		return expanded;
	}

	private static async Task<double> ContentHeightAsync(string name)
	{
		var height = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			NavigationElements.AsExpander(ElementRegistry.Resolve(name), "content");
			height = ElementRegistry.TryResolve(ExpanderContentPart, out var content) ? content.ActualHeight : 0.0;
			if (content is { Visibility: Visibility.Collapsed })
			{
				height = 0.0;
			}
		}).ConfigureAwait(false);

		return height;
	}

	private static async Task<double> VerticalOffsetAsync(string name)
	{
		var offset = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			offset = NavigationElements.AsScrollViewer(ElementRegistry.Resolve(name), "VerticalOffset").VerticalOffset)
			.ConfigureAwait(false);

		return offset;
	}
}
