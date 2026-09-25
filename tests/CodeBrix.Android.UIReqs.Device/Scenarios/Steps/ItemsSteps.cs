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
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The Items group's steps: the controls that show a collection, the item a finger picks out of
/// one, and the gestures that move a collection around on a touch panel. Every pattern here
/// names the control it belongs to, so that no other coverage group's sentence can match one of
/// them.
/// <para>
/// An item is not something a scenario built, so it has no Gherkin name: a requirement talks
/// about "item 2 of the ListView", and the step reaches the container the control generated
/// for that item. Containers are also where a moving part lives, so the steps that talk about
/// a scrolled item remember its rectangle at capture time rather than reading it again later.
/// </para>
/// </summary>
[Binding]
public sealed class ItemsSteps
{
	/// <summary>How many moves a drag is delivered in, so that it looks like a finger and not a jump.</summary>
	public const int DragSteps = 10;

	/// <summary>How long the panel is given between the moves of a drag.</summary>
	public static readonly TimeSpan DragStepDelay = TimeSpan.FromMilliseconds(20);

	/// <summary>How long a finger rests on the panel before it starts moving.</summary>
	public static readonly TimeSpan DragPressDelay = TimeSpan.FromMilliseconds(80);

	/// <summary>
	/// How long a scrolling control is left alone after the finger lifts. A panning gesture
	/// carries on under its own inertia, and the offset a requirement talks about is the one it
	/// comes to rest at.
	/// </summary>
	public static readonly TimeSpan DragSettleDelay = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// How long the panel is left alone before a tap that targets a part of a template. A real
	/// finger never arrives in the same instant as the control it touches.
	/// </summary>
	public static readonly TimeSpan BeforeTapDelay = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// How long a drop-down is given to finish opening or closing. Both are animated, so a
	/// frame taken the instant the tap is delivered would show the popup halfway there.
	/// </summary>
	public static readonly TimeSpan DropDownSettleDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>
	/// How long an item is given to settle into the look its new state gives it. A row does
	/// not jump from unselected to selected: the finger's own pressed look is animated away
	/// first, and until that has run the row is painted a shade of the selection colour rather
	/// than the selection colour itself.
	/// </summary>
	public static readonly TimeSpan ItemStateSettleDelay = TimeSpan.FromMilliseconds(500);

	/// <summary>How wide a control built from a table of sections is.</summary>
	public const int SectionsWidth = 600;

	/// <summary>How tall a control built from a table of sections is.</summary>
	public const int SectionsHeight = 400;

	/// <summary>
	/// How wide the panel inside one section is. A section's own content area is smaller than
	/// the control - a Pivot keeps a header strip, a TabView keeps a tab strip - and a panel
	/// that only asked to stretch would be given its smallest size, so a section's panel is
	/// given a size that fills most of what is left.
	/// </summary>
	public const int SectionPanelWidth = 400;

	/// <summary>How tall the panel inside one section is.</summary>
	public const int SectionPanelHeight = 200;

	/// <summary>How wide a TreeView built from a table of nodes is.</summary>
	public const int TreeWidth = 400;

	/// <summary>How tall a TreeView built from a table of nodes is.</summary>
	public const int TreeHeight = 400;

	/// <summary>How much of a FlipView's width a swipe travels across.</summary>
	public const double SwipeShareOfWidth = 0.8;

	/// <summary>
	/// How much of a FlipView's width a SHORT swipe travels across - the swipe that turns
	/// exactly one page. The mandatory-snap arithmetic behind a panning FlipView adds one whole
	/// snap interval to the LIVE, mid-pan offset and then rounds that sum to the nearest snap
	/// point, so a finger that has already taken the content past about half a page lands two
	/// pages on rather than one. A share below a half is therefore what "turn the next page"
	/// means to this runtime.
	/// </summary>
	public const double FlipViewShortSwipeFraction = 0.4;

	/// <summary>
	/// How long a FlipView is left alone after a short swipe. Coming to rest on a snap point is
	/// a composition animation of a fixed one second, and the selection only follows the offset
	/// once that animation has finished, so this waits longer than the animation runs rather
	/// than reading the view while it is still moving.
	/// </summary>
	public static readonly TimeSpan FlipViewSnapSettle = TimeSpan.FromSeconds(2);

	private const int PointerId = 0;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public ItemsSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory the Items group's controls, and makes sure the theme colour
	/// names a selected item is painted in can be written in a feature file.
	/// </summary>
	/// <returns>A task that completes once a feature file may name those controls.</returns>
	[BeforeTestRun(Order = 12)]
	public static async Task Register_the_items_controls()
	{
		ItemsElements.Register();
		await ThemeColors.RegisterAsync().ConfigureAwait(false);
	}

	// ------------------------------------------------- sections and nodes

	/// <summary>Shows a Pivot whose sections are a header and a panel of a known colour.</summary>
	/// <param name="name">The name the scenario refers to the Pivot by.</param>
	/// <param name="sections">A Header/Color table, one row per section.</param>
	/// <returns>A task that completes once the Pivot is showing.</returns>
	[Given("the application shows a Pivot named {string} with sections:")]
	public async Task Given_the_application_shows_a_Pivot_with_sections(string name, DataTable sections) =>
		await ShowSectionsAsync("Pivot", name, sections).ConfigureAwait(false);

	/// <summary>Shows a TabView whose tabs are a header and a panel of a known colour.</summary>
	/// <param name="name">The name the scenario refers to the TabView by.</param>
	/// <param name="sections">A Header/Color table, one row per tab.</param>
	/// <returns>A task that completes once the TabView is showing.</returns>
	[Given("the application shows a TabView named {string} with sections:")]
	public async Task Given_the_application_shows_a_TabView_with_sections(string name, DataTable sections) =>
		await ShowSectionsAsync("TabView", name, sections).ConfigureAwait(false);

	/// <summary>
	/// Shows a SelectorBar and, below it, the panel the application repaints when the bar's
	/// selection changes - a SelectorBar carries no content of its own, so swapping content is
	/// the application's job and the requirement is about the bar making it happen.
	/// </summary>
	/// <param name="name">The name the scenario refers to the SelectorBar by.</param>
	/// <param name="sections">A Header/Color table, one row per bar item.</param>
	/// <returns>A task that completes once the SelectorBar is showing.</returns>
	[Given("the application shows a SelectorBar named {string} with sections:")]
	public async Task Given_the_application_shows_a_SelectorBar_with_sections(string name, DataTable sections) =>
		await ShowSectionsAsync("SelectorBar", name, sections).ConfigureAwait(false);

	/// <summary>Shows a TreeView built from a table of root nodes and their children.</summary>
	/// <param name="name">The name the scenario refers to the TreeView by.</param>
	/// <param name="nodes">A Node/Children table; Children is a comma-separated list.</param>
	/// <returns>A task that completes once the TreeView is showing.</returns>
	[Given("the application shows a TreeView named {string} with the nodes:")]
	public async Task Given_the_application_shows_a_TreeView_with_the_nodes(string name, DataTable nodes)
	{
		ArgumentNullException.ThrowIfNull(nodes);

		var rows = nodes.Rows
			.Select(row => (Node: row["Node"], Children: nodes.Header.Contains("Children") ? row["Children"] : string.Empty))
			.ToArray();

		var element = await ElementFactory.CreateAsync("TreeView", name, new[]
		{
			new KeyValuePair<string, string>("Width", TreeWidth.ToString(CultureInfo.InvariantCulture)),
			new KeyValuePair<string, string>("Height", TreeHeight.ToString(CultureInfo.InvariantCulture)),
		}).ConfigureAwait(false);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var tree = (TreeView) element;
			foreach (var row in rows)
			{
				var node = new TreeViewNode { Content = row.Node };
				foreach (var child in ItemsElements.ReadList(row.Children))
				{
					node.Children.Add(new TreeViewNode { Content = child });
				}

				tree.RootNodes.Add(node);
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		EventRecorder.Clear();
	}

	// -------------------------------------------------------------- counts

	/// <summary>Asserts how many items a control holds. The count is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="count">How many items it must hold.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ListView {string} holds {int} items")]
	[Then("the GridView {string} holds {int} items")]
	[Then("the ComboBox {string} holds {int} items")]
	[Then("the ItemsControl {string} holds {int} items")]
	[Then("the ItemsRepeater {string} holds {int} items")]
	[Then("the FlipView {string} holds {int} items")]
	[Then("the Pivot {string} holds {int} items")]
	[Then("the TabView {string} holds {int} items")]
	[Then("the SelectorBar {string} holds {int} items")]
	public async Task Then_holds_items(string name, int count)
	{
		var actual = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() => actual = ItemCount(ElementRegistry.Resolve(name)))
			.ConfigureAwait(false);

		actual.Should().Be(count, "the number of items \"{0}\" holds was asserted", name);
	}

	/// <summary>Asserts how many rows a TreeView is showing right now.</summary>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <param name="count">How many rows must be showing.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the TreeView {string} shows {int} rows")]
	public async Task Then_the_TreeView_shows_rows(string name, int count)
	{
		var rows = await TreeRowsAsync(name).ConfigureAwait(false);
		rows.Count.Should().Be(count,
			"the number of rows the TreeView \"{0}\" shows was asserted; it shows [{1}]",
			name, string.Join(", ", rows.Select(row => row.Content?.ToString() ?? "?")));
	}

	// ----------------------------------------------------------- selection

	/// <summary>Asserts which item a control has selected. SelectedIndex is a fact about the tree.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="index">The expected index, counted from zero as the property does.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the SelectedIndex of the ListView {string} is {int}")]
	[Then("the SelectedIndex of the GridView {string} is {int}")]
	[Then("the SelectedIndex of the ComboBox {string} is {int}")]
	[Then("the SelectedIndex of the FlipView {string} is {int}")]
	[Then("the SelectedIndex of the Pivot {string} is {int}")]
	[Then("the SelectedIndex of the TabView {string} is {int}")]
	public async Task Then_the_SelectedIndex_is(string name, int index)
	{
		var actual = int.MinValue;
		await TestTargetFixture.RunOnUIThreadAsync(() => actual = SelectedIndex(ElementRegistry.Resolve(name)))
			.ConfigureAwait(false);

		actual.Should().Be(index, "the SelectedIndex of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that one item of a list is selected.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} is selected")]
	[Then("item {int} of the GridView {string} is selected")]
	public async Task Then_item_is_selected(int index, string name) =>
		(await IsItemSelectedAsync(name, index).ConfigureAwait(false)).Should()
			.BeTrue("item {0} of \"{1}\" must be selected", index, name);

	/// <summary>Asserts that one item of a list is not selected.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} is not selected")]
	[Then("item {int} of the GridView {string} is not selected")]
	public async Task Then_item_is_not_selected(int index, string name) =>
		(await IsItemSelectedAsync(name, index).ConfigureAwait(false)).Should()
			.BeFalse("item {0} of \"{1}\" must not be selected", index, name);

	/// <summary>Asserts how many items of a list are selected at once.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="count">How many must be selected.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ListView {string} has {int} selected items")]
	[Then("the GridView {string} has {int} selected items")]
	public async Task Then_has_selected_items(string name, int count)
	{
		var selected = Array.Empty<string>();
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var list = ElementRegistry.Resolve(name) as ListViewBase
				?? throw new NotSupportedException($"\"{name}\" is not a list, so it has no selected items.");
			selected = list.SelectedItems.Select(item => item?.ToString() ?? "?").ToArray();
		}).ConfigureAwait(false);

		selected.Length.Should().Be(count,
			"the number of selected items of \"{0}\" was asserted; it has selected [{1}]",
			name, string.Join(", ", selected));
	}

	/// <summary>Asserts how often a control reported that its selection changed.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="times">How often it must have been raised.</param>
	[Then("the SelectionChanged of the ListView {string} was raised {int} times")]
	[Then("the SelectionChanged of the GridView {string} was raised {int} times")]
	[Then("the SelectionChanged of the ComboBox {string} was raised {int} times")]
	[Then("the SelectionChanged of the FlipView {string} was raised {int} times")]
	[Then("the SelectionChanged of the Pivot {string} was raised {int} times")]
	[Then("the SelectionChanged of the TabView {string} was raised {int} times")]
	[Then("the SelectionChanged of the SelectorBar {string} was raised {int} times")]
	public void Then_the_SelectionChanged_was_raised_times(string name, int times) =>
		AssertEventCount(name, ItemsElements.SelectionChangedEvent, times);

	/// <summary>Asserts which item a SelectorBar has selected, by the text it shows.</summary>
	/// <param name="name">The Gherkin name of the SelectorBar.</param>
	/// <param name="text">The text of the item that must be selected.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the SelectorBar {string} has selected {string}")]
	public async Task Then_the_SelectorBar_has_selected(string name, string text)
	{
		var selected = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var bar = ElementRegistry.Resolve(name) as SelectorBar
				?? throw new NotSupportedException($"\"{name}\" is not a SelectorBar.");
			selected = bar.SelectedItem?.Text ?? string.Empty;
		}).ConfigureAwait(false);

		selected.Should().Be(text, "the selected item of the SelectorBar \"{0}\" was asserted", name);
	}

	// -------------------------------------------------------- item content

	/// <summary>Asserts what an item's container carries. Content is a fact about the tree.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="text">The text the item must carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} carries the text {string}")]
	[Then("item {int} of the GridView {string} carries the text {string}")]
	[Then("item {int} of the ComboBox {string} carries the text {string}")]
	public async Task Then_item_carries_the_text(int index, string name, string text)
	{
		var content = string.Empty;
		var container = await ContainerAsync(name, index).ConfigureAwait(false);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			content = (container as ContentControl)?.Content?.ToString() ?? string.Empty).ConfigureAwait(false);

		content.Should().Be(text, "the content of item {0} of \"{1}\" was asserted", index, name);
	}

	/// <summary>Asserts that an item's rectangle is mostly painted one colour.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="color">The colour it must be filled with.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} is filled with {string}")]
	[Then("item {int} of the GridView {string} is filled with {string}")]
	[Then("item {int} of the ComboBox {string} is filled with {string}")]
	public async Task Then_item_is_filled_with(int index, string name, Color color) =>
		(await ItemRegionAsync(name, index).ConfigureAwait(false)).IsFilledWith(color);

	/// <summary>Asserts that an item's rectangle is not painted a colour.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} is not filled with {string}")]
	[Then("item {int} of the GridView {string} is not filled with {string}")]
	[Then("item {int} of the ComboBox {string} is not filled with {string}")]
	public async Task Then_item_is_not_filled_with(int index, string name, Color color) =>
		(await ItemRegionAsync(name, index).ConfigureAwait(false)).DoesNotContain(color);

	/// <summary>Asserts that an item was actually drawn.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} has ink")]
	[Then("item {int} of the GridView {string} has ink")]
	[Then("item {int} of the ComboBox {string} has ink")]
	public async Task Then_item_has_ink(int index, string name) =>
		(await ItemRegionAsync(name, index).ConfigureAwait(false)).HasInk();

	/// <summary>Asserts that one item looks different from how it looked in an earlier frame.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} in frame {string} differs from frame {string}")]
	[Then("item {int} of the GridView {string} in frame {string} differs from frame {string}")]
	public async Task Then_item_in_frame_differs_from_frame(int index, string name,
		string frameName, string otherFrameName)
	{
		var region = await ItemRegionAsync(name, index, frameName).ConfigureAwait(false);
		var other = await ItemRegionAsync(name, index, otherFrameName).ConfigureAwait(false);
		region.DiffersFrom(other);
	}

	/// <summary>Asserts that one item looks the same as it did in an earlier frame.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="frameName">The later frame.</param>
	/// <param name="otherFrameName">The earlier frame.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} in frame {string} is unchanged from frame {string}")]
	[Then("item {int} of the GridView {string} in frame {string} is unchanged from frame {string}")]
	public async Task Then_item_in_frame_is_unchanged_from_frame(int index, string name,
		string frameName, string otherFrameName)
	{
		var region = await ItemRegionAsync(name, index, frameName).ConfigureAwait(false);
		var other = await ItemRegionAsync(name, index, otherFrameName).ConfigureAwait(false);
		region.SameAs(other);
	}

	/// <summary>Asserts that two items of a list do not look the same.</summary>
	/// <param name="index">One item, counted from one.</param>
	/// <param name="other">The other item, counted from one.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the ListView {string} looks different from item {int}")]
	public async Task Then_item_looks_different_from_item(int index, string name, int other)
	{
		var region = await ItemRegionAsync(name, index).ConfigureAwait(false);
		var otherRegion = await ItemRegionAsync(name, other).ConfigureAwait(false);
		region.DiffersFrom(otherRegion);
	}

	/// <summary>Asserts that one item of a GridView sits to the right of another, on one row.</summary>
	/// <param name="index">The item that must be on the right, counted from one.</param>
	/// <param name="name">The Gherkin name of the GridView.</param>
	/// <param name="other">The item it must be to the right of, counted from one.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("item {int} of the GridView {string} is to the right of item {int}")]
	public async Task Then_item_is_to_the_right_of_item(int index, string name, int other)
	{
		var rect = await DeviceRect.OfAsync(await ContainerAsync(name, index).ConfigureAwait(false))
			.ConfigureAwait(false);
		var otherRect = await DeviceRect.OfAsync(await ContainerAsync(name, other).ConfigureAwait(false))
			.ConfigureAwait(false);

		rect.X.Should().BeGreaterThan(otherRect.X,
			"item {0} of \"{1}\" must sit to the right of item {2}: they are at {3} and {4}",
			index, name, other, rect, otherRect);
		rect.Y.Should().Be(otherRect.Y,
			"item {0} of \"{1}\" must sit on the same row as item {2}: they are at {3} and {4}",
			index, name, other, rect, otherRect);
	}

	// --------------------------------------------------------------- input

	/// <summary>Taps the centre of one item of a control.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("item {int} of the ListView {string} is tapped")]
	[When("item {int} of the GridView {string} is tapped")]
	[When("item {int} of the ComboBox {string} is tapped")]
	public async Task When_item_is_tapped(int index, string name)
	{
		var container = await ContainerAsync(name, index).ConfigureAwait(false);
		await TapAsync(container).ConfigureAwait(false);
		await SettleAsync(ItemStateSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Taps the header of one section of a control that swaps its content.</summary>
	/// <param name="index">The header, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("header {int} of the Pivot {string} is tapped")]
	[When("header {int} of the TabView {string} is tapped")]
	[When("header {int} of the SelectorBar {string} is tapped")]
	public async Task When_header_is_tapped(int index, string name)
	{
		var header = await HeaderAsync(name, index).ConfigureAwait(false);
		await TapAsync(header).ConfigureAwait(false);
	}

	/// <summary>Opens a ComboBox's drop-down with a tap and lets the popup finish opening.</summary>
	/// <param name="name">The Gherkin name of the ComboBox.</param>
	/// <returns>A task that completes once the drop-down is open and still.</returns>
	[When("the ComboBox {string} is opened")]
	public async Task When_the_ComboBox_is_opened(string name)
	{
		await TapAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
		await SettleAsync(DropDownSettleDelay).ConfigureAwait(false);
	}

	/// <summary>Taps one row of a TreeView.</summary>
	/// <param name="index">The row, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("row {int} of the TreeView {string} is tapped")]
	public async Task When_row_of_the_TreeView_is_tapped(int index, string name)
	{
		var rows = await TreeRowsAsync(name).ConfigureAwait(false);
		await TapAsync(Row(rows, name, index)).ConfigureAwait(false);
	}

	/// <summary>Taps the chevron a TreeView row carries for expanding and collapsing it.</summary>
	/// <param name="index">The row, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the expand chevron of row {int} of the TreeView {string} is tapped")]
	public async Task When_the_expand_chevron_is_tapped(int index, string name)
	{
		var rows = await TreeRowsAsync(name).ConfigureAwait(false);
		var row = Row(rows, name, index);

		FrameworkElement chevron = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			chevron = VisualTreeSearch.FindDescendantNamed(row, TreeViewChevronPart)
				?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
					$"Row {index} of the TreeView \"{name}\" carries no \"{TreeViewChevronPart}\" part.")))
			.ConfigureAwait(false);

		await TapAsync(chevron).ConfigureAwait(false);
	}

	/// <summary>Asserts that a TreeView row is showing its children.</summary>
	/// <param name="index">The row, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of the TreeView {string} is expanded")]
	public async Task Then_row_is_expanded(int index, string name) =>
		(await IsRowExpandedAsync(name, index).ConfigureAwait(false)).Should()
			.BeTrue("row {0} of the TreeView \"{1}\" must be expanded", index, name);

	/// <summary>Asserts that a TreeView row is not showing its children.</summary>
	/// <param name="index">The row, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of the TreeView {string} is not expanded")]
	public async Task Then_row_is_not_expanded(int index, string name) =>
		(await IsRowExpandedAsync(name, index).ConfigureAwait(false)).Should()
			.BeFalse("row {0} of the TreeView \"{1}\" must not be expanded", index, name);

	/// <summary>Asserts that a TreeView row was actually drawn.</summary>
	/// <param name="index">The row, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of the TreeView {string} has ink")]
	public async Task Then_row_has_ink(int index, string name)
	{
		var rows = await TreeRowsAsync(name).ConfigureAwait(false);
		var row = Row(rows, name, index);
		var bounds = await DeviceRect.OfAsync(row).ConfigureAwait(false);
		new Region(ScenarioFrames.Current(_scenarioContext), bounds, string.Create(
			CultureInfo.InvariantCulture, $"row {index} of the TreeView \"{name}\"")).HasInk();
	}

	/// <summary>Asserts what a TreeView row carries. Content is a fact about the tree.</summary>
	/// <param name="index">The row, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the TreeView.</param>
	/// <param name="text">The text the row must carry.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("row {int} of the TreeView {string} carries the text {string}")]
	public async Task Then_row_carries_the_text(int index, string name, string text)
	{
		var rows = await TreeRowsAsync(name).ConfigureAwait(false);
		var row = Row(rows, name, index);
		var content = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() => content = row.Content?.ToString() ?? string.Empty)
			.ConfigureAwait(false);

		content.Should().Be(text, "the content of row {0} of the TreeView \"{1}\" was asserted", index, name);
	}

	// ------------------------------------------------------------ dragging

	/// <summary>
	/// Drags a list upwards with one finger: press inside it, move in several steps, and lift -
	/// the three calls a touch panel actually delivers - then let the panning come to rest.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="distance">How many device pixels the finger travels upwards.</param>
	/// <returns>A task that completes once the finger has been lifted and the list is still.</returns>
	[When("the ListView {string} is dragged up by {int} pixels")]
	[When("the GridView {string} is dragged up by {int} pixels")]
	public async Task When_the_list_is_dragged_up_by_pixels(string name, int distance)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
		var (x, startY) = bounds.Center;
		await DragAsync(x, startY, 0, -distance).ConfigureAwait(false);
	}

	/// <summary>Swipes a FlipView from right to left, which is how a finger asks for the next item.</summary>
	/// <param name="name">The Gherkin name of the FlipView.</param>
	/// <returns>A task that completes once the finger has been lifted and the view is still.</returns>
	[When("the FlipView {string} is swiped to the left")]
	public async Task When_the_FlipView_is_swiped_to_the_left(string name) =>
		await SwipeFlipViewAsync(name, toTheLeft: true).ConfigureAwait(false);

	/// <summary>Swipes a FlipView from left to right, which is how a finger asks for the previous item.</summary>
	/// <param name="name">The Gherkin name of the FlipView.</param>
	/// <returns>A task that completes once the finger has been lifted and the view is still.</returns>
	[When("the FlipView {string} is swiped to the right")]
	public async Task When_the_FlipView_is_swiped_to_the_right(string name) =>
		await SwipeFlipViewAsync(name, toTheLeft: false).ConfigureAwait(false);

	/// <summary>
	/// Swipes a FlipView a short way to the left - <see cref="FlipViewShortSwipeFraction"/> of
	/// its width - and then leaves it alone for <see cref="FlipViewSnapSettle"/>, which is how a
	/// finger asks for the next page and waits for the view to come to rest on it.
	/// </summary>
	/// <param name="name">The Gherkin name of the FlipView.</param>
	/// <returns>A task that completes once the view has come to rest on a page.</returns>
	[When("the FlipView {string} is swiped a short way to the left")]
	public async Task When_the_FlipView_is_swiped_a_short_way_to_the_left(string name) =>
		await SwipeFlipViewAsync(name, toTheLeft: true, FlipViewShortSwipeFraction, FlipViewSnapSettle)
			.ConfigureAwait(false);

	/// <summary>
	/// Asserts how far sideways a FlipView's own scroll viewer has been taken. A FlipView turns
	/// its page by panning: the offset is what a swipe actually moves, and the selection
	/// follows it.
	/// </summary>
	/// <param name="name">The Gherkin name of the FlipView.</param>
	/// <param name="distance">The fewest device pixels it must have moved.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the FlipView {string} has panned sideways by at least {int} pixels")]
	public async Task Then_the_FlipView_has_panned_sideways(string name, int distance) =>
		(await HorizontalOffsetAsync(name).ConfigureAwait(false)).Should().BeGreaterThanOrEqualTo(distance,
			"the FlipView \"{0}\" must have panned when a finger swiped it", name);

	/// <summary>Asserts how far a list has scrolled, read from the scroll viewer inside it.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="distance">The fewest device pixels it must have scrolled.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ListView {string} has scrolled down by at least {int} pixels")]
	[Then("the GridView {string} has scrolled down by at least {int} pixels")]
	public async Task Then_the_list_has_scrolled_down_by_at_least(string name, int distance) =>
		(await VerticalOffsetAsync(name).ConfigureAwait(false)).Should().BeGreaterThanOrEqualTo(distance,
			"the list \"{0}\" must have scrolled down when a finger dragged it up", name);

	/// <summary>Asserts that a list is still showing its first item, at the top.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ListView {string} has not scrolled")]
	[Then("the GridView {string} has not scrolled")]
	public async Task Then_the_list_has_not_scrolled(string name) =>
		(await VerticalOffsetAsync(name).ConfigureAwait(false)).Should().Be(0,
			"the list \"{0}\" must still be at the top", name);

	/// <summary>
	/// Captures a frame and remembers where one item was in it, because a scrolled item is
	/// somewhere else in the next frame.
	/// </summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="captureName">The name to remember the capture under.</param>
	/// <returns>A task that completes when the frame has arrived.</returns>
	[When("item {int} of the ListView {string} is captured as {string}")]
	[When("item {int} of the GridView {string} is captured as {string}")]
	public async Task When_item_is_captured_as(int index, string name, string captureName)
	{
		var itemName = await RegisterContainerAsync(name, index).ConfigureAwait(false);
		await CapturedParts.CaptureAsync(_scenarioContext, captureName, itemName, name).ConfigureAwait(false);
	}

	/// <summary>Asserts that an item moved up the panel between two captures.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="fromCapture">The earlier capture.</param>
	/// <param name="toCapture">The later capture.</param>
	/// <param name="minimum">The fewest device pixels the item must have travelled.</param>
	[Then("item {int} of the ListView {string} moved up from {string} to {string} by at least {int} pixels")]
	[Then("item {int} of the GridView {string} moved up from {string} to {string} by at least {int} pixels")]
	public void Then_item_moved_up(int index, string name, string fromCapture, string toCapture, int minimum)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);

		(before.Part.Y - after.Part.Y).Should().BeGreaterThanOrEqualTo(minimum,
			"item {0} of \"{1}\" must move up: it was at {2} in capture \"{3}\" and at {4} in capture \"{5}\"",
			index, name, before.Part, fromCapture, after.Part, toCapture);
	}

	/// <summary>Asserts that an item was actually drawn in a capture.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="captureName">The capture to look at.</param>
	[Then("item {int} of the ListView {string} had ink in {string}")]
	[Then("item {int} of the GridView {string} had ink in {string}")]
	public void Then_item_had_ink_in(int index, string name, string captureName) =>
		CapturedParts.Get(_scenarioContext, captureName)
			.PartRegion(string.Create(CultureInfo.InvariantCulture,
				$"item {index} of \"{name}\" in capture \"{captureName}\""))
			.HasInk();

	/// <summary>Asserts that the place an item used to occupy no longer looks the way it did.</summary>
	/// <param name="index">The item, counted from one as a feature file counts.</param>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <param name="fromCapture">The capture the item's old place is taken from.</param>
	/// <param name="toCapture">The capture that must look different there.</param>
	[Then("where item {int} of the ListView {string} was in {string} looks different in {string}")]
	[Then("where item {int} of the GridView {string} was in {string} looks different in {string}")]
	public void Then_where_item_was_looks_different(int index, string name, string fromCapture, string toCapture)
	{
		var before = CapturedParts.Get(_scenarioContext, fromCapture);
		var after = CapturedParts.Get(_scenarioContext, toCapture);
		var was = before.PartRegion(string.Create(CultureInfo.InvariantCulture,
			$"where item {index} of \"{name}\" was in capture \"{fromCapture}\""));
		var now = new Region(after.Frame, before.Part, $"that same place in capture \"{toCapture}\"");
		now.DiffersFrom(was);
	}

	// -------------------------------------------------------- drop-downs

	/// <summary>Asserts that a ComboBox's drop-down is showing.</summary>
	/// <param name="name">The Gherkin name of the ComboBox.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ComboBox {string} is open")]
	public async Task Then_the_ComboBox_is_open(string name) =>
		(await IsDropDownOpenAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the drop-down of the ComboBox \"{0}\" must be open", name);

	/// <summary>Asserts that a ComboBox's drop-down is not showing.</summary>
	/// <param name="name">The Gherkin name of the ComboBox.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ComboBox {string} is closed")]
	public async Task Then_the_ComboBox_is_closed(string name) =>
		(await IsDropDownOpenAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the drop-down of the ComboBox \"{0}\" must be closed", name);

	// ---------------------------------------------------------------- inner

	/// <summary>The template part a TreeView row carries for expanding and collapsing it.</summary>
	public const string TreeViewChevronPart = "ExpandCollapseChevron";

	private static void AssertEventCount(string name, string eventName, int expected) =>
		EventRecorder.Count(name, eventName).Should().Be(expected,
			"the {0} of \"{1}\" was asserted; the scenario recorded [{2}]",
			eventName, name, string.Join(", ", EventRecorder.Recorded));

	private async Task ShowSectionsAsync(string kind, string name, DataTable sections)
	{
		ArgumentNullException.ThrowIfNull(sections);

		var rows = sections.Rows
			.Select(row => (Header: row["Header"], Color: row["Color"]))
			.ToArray();

		// A SelectorBar is a bar, not a page: it takes its natural size at the top of a page
		// the scenario builds around it, while a Pivot and a TabView are the page themselves.
		var settings = string.Equals(kind, "SelectorBar", StringComparison.Ordinal)
			? Array.Empty<KeyValuePair<string, string>>()
			: new[]
			{
				new KeyValuePair<string, string>("Width", SectionsWidth.ToString(CultureInfo.InvariantCulture)),
				new KeyValuePair<string, string>("Height", SectionsHeight.ToString(CultureInfo.InvariantCulture)),
			};

		var element = await ElementFactory.CreateAsync(kind, name, settings).ConfigureAwait(false);

		FrameworkElement content = element;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			switch (element)
			{
				case Pivot pivot:
					foreach (var row in rows)
					{
						pivot.Items.Add(new PivotItem
						{
							Header = row.Header,
							HorizontalContentAlignment = HorizontalAlignment.Stretch,
							VerticalContentAlignment = VerticalAlignment.Stretch,
							Content = SectionPanel(row.Color, row.Header),
						});
					}

					break;
				case TabView tabs:
					foreach (var row in rows)
					{
						tabs.TabItems.Add(new TabViewItem
						{
							Header = row.Header,
							IsClosable = false,
							HorizontalContentAlignment = HorizontalAlignment.Stretch,
							VerticalContentAlignment = VerticalAlignment.Stretch,
							Content = SectionPanel(row.Color, row.Header),
						});
					}

					break;
				case SelectorBar bar:
					content = BuildSelectorBarPage(bar, name, rows);
					break;
				default:
					throw new NotSupportedException(
						$"A {element.GetType().Name} named \"{name}\" is not built from sections.");
			}
		}).ConfigureAwait(false);

		await TestTargetFixture.SetContentAsync(content).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);

		// Adding sections selects the first one, so the counting a requirement does starts
		// once the control is showing rather than while it is being built.
		EventRecorder.Clear();
	}

	private static FrameworkElement BuildSelectorBarPage(SelectorBar bar, string name,
		IReadOnlyList<(string Header, string Color)> rows)
	{
		var contentName = ContentNameOf(name);
		var page = new Grid
		{
			Width = SectionsWidth,
			Height = SectionsHeight,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

		var panel = new Border
		{
			Name = contentName,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};
		Grid.SetRow(panel, 1);

		bar.HorizontalAlignment = HorizontalAlignment.Center;
		bar.VerticalAlignment = VerticalAlignment.Top;

		foreach (var row in rows)
		{
			bar.Items.Add(new SelectorBarItem { Text = row.Header });
		}

		bar.SelectionChanged += (sender, _) =>
		{
			var selected = sender.SelectedItem?.Text;
			var section = rows.FirstOrDefault(row => string.Equals(row.Header, selected, StringComparison.Ordinal));
			panel.Background = section.Color is null
				? null
				: new SolidColorBrush(Colors.Parse(section.Color));
		};

		if (bar.Items.Count > 0)
		{
			bar.SelectedItem = bar.Items[0];
		}

		page.Children.Add(bar);
		page.Children.Add(panel);
		ElementRegistry.Register(contentName, panel);
		return page;
	}

	private static Border SectionPanel(string color, string header)
	{
		var panel = ItemsElements.BuildColorPanel(color, header);
		panel.Width = SectionPanelWidth;
		panel.Height = SectionPanelHeight;
		return panel;
	}

	/// <summary>The name the panel a SelectorBar swaps is registered under.</summary>
	/// <param name="name">The Gherkin name of the SelectorBar.</param>
	/// <returns>The panel's name.</returns>
	public static string ContentNameOf(string name) => name + " content";

	private static int ItemCount(FrameworkElement element) => element switch
	{
		ItemsRepeater repeater => repeater.ItemsSourceView?.Count ?? 0,
		TabView tabs => tabs.TabItems.Count,
		SelectorBar bar => bar.Items.Count,
		ItemsControl items => items.Items.Count,
		_ => throw new NotSupportedException(
			$"A {element.GetType().Name} named \"{element.Name}\" holds no items the harness can count."),
	};

	private static int SelectedIndex(FrameworkElement element) => element switch
	{
		Selector selector => selector.SelectedIndex,
		TabView tabs => tabs.SelectedIndex,
		Pivot pivot => pivot.SelectedIndex,
		_ => throw new NotSupportedException(
			$"A {element.GetType().Name} named \"{element.Name}\" has no SelectedIndex."),
	};

	private static async Task<bool> IsItemSelectedAsync(string name, int index)
	{
		var container = await ContainerAsync(name, index).ConfigureAwait(false);
		var selected = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => selected = container is SelectorItem { IsSelected: true })
			.ConfigureAwait(false);

		return selected;
	}

	private static async Task<bool> IsDropDownOpenAsync(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var combo = ElementRegistry.Resolve(name) as ComboBox
				?? throw new NotSupportedException($"\"{name}\" is not a ComboBox, so it has no drop-down.");
			open = combo.IsDropDownOpen;
		}).ConfigureAwait(false);

		return open;
	}

	private static async Task<bool> IsRowExpandedAsync(string name, int index)
	{
		var rows = await TreeRowsAsync(name).ConfigureAwait(false);
		var row = Row(rows, name, index);
		var expanded = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => expanded = row.IsExpanded).ConfigureAwait(false);
		return expanded;
	}

	private static async Task<IReadOnlyList<TreeViewItem>> TreeRowsAsync(string name)
	{
		IReadOnlyList<TreeViewItem> rows = Array.Empty<TreeViewItem>();
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var tree = ElementRegistry.Resolve(name) as TreeView
				?? throw new NotSupportedException($"\"{name}\" is not a TreeView, so it shows no rows.");

			// A row a scenario counts is a row a person sees, top to bottom. The containers
			// themselves come back in the order the panel happens to hold them - a node opened
			// later is appended rather than inserted - so they are put in the order they are
			// drawn in before anyone counts them.
			rows = VisualTreeSearch.FindDescendants<TreeViewItem>(tree)
				.OrderBy(row => row.TransformToVisual(tree).TransformPoint(default).Y)
				.ToArray();
		}).ConfigureAwait(false);

		return rows;
	}

	private static TreeViewItem Row(IReadOnlyList<TreeViewItem> rows, string name, int index)
	{
		if (index < 1 || index > rows.Count)
		{
			throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
				$"The TreeView \"{name}\" shows {rows.Count} rows, so it has no row {index}."));
		}

		return rows[index - 1];
	}

	private static async Task<FrameworkElement> HeaderAsync(string name, int index)
	{
		FrameworkElement header = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			IReadOnlyList<FrameworkElement> headers = element switch
			{
				Pivot pivot => VisualTreeSearch.FindDescendants<PivotHeaderItem>(pivot),
				TabView tabs => VisualTreeSearch.FindDescendants<TabViewItem>(tabs),
				SelectorBar bar => VisualTreeSearch.FindDescendants<SelectorBarItem>(bar),
				_ => throw new NotSupportedException(
					$"A {element.GetType().Name} named \"{name}\" carries no headers."),
			};

			if (index < 1 || index > headers.Count)
			{
				throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
					$"\"{name}\" shows {headers.Count} headers, so it has no header {index}."));
			}

			header = headers[index - 1];
		}).ConfigureAwait(false);

		return header;
	}

	private static async Task<FrameworkElement> ContainerAsync(string name, int index)
	{
		FrameworkElement container = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			var items = element as ItemsControl
				?? throw new NotSupportedException(
					$"A {element.GetType().Name} named \"{name}\" generates no item containers.");

			container = items.ContainerFromIndex(index - 1) as FrameworkElement
				?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
					$"\"{name}\" has generated no container for item {index}; it holds {items.Items.Count} items. An item that is not realised - one scrolled out of view, or one inside a drop-down that is not open - has no container and no rectangle."));
		}).ConfigureAwait(false);

		return container;
	}

	private static async Task<string> RegisterContainerAsync(string name, int index)
	{
		var container = await ContainerAsync(name, index).ConfigureAwait(false);
		var itemName = string.Create(CultureInfo.InvariantCulture, $"{name} item {index}");
		ElementRegistry.Register(itemName, container);
		return itemName;
	}

	private async Task<Region> ItemRegionAsync(string name, int index,
		string frameName = ScenarioFrames.CurrentFrameName)
	{
		var container = await ContainerAsync(name, index).ConfigureAwait(false);
		var bounds = await DeviceRect.OfAsync(container).ConfigureAwait(false);
		var frame = ScenarioFrames.Get(_scenarioContext, frameName);
		var where = frameName == ScenarioFrames.CurrentFrameName
			? string.Empty
			: $" in frame \"{frameName}\"";
		return new Region(frame, bounds, string.Create(CultureInfo.InvariantCulture,
			$"item {index} of \"{name}\"{where}"));
	}

	private static async Task<double> HorizontalOffsetAsync(string name)
	{
		var offset = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			var scroller = ItemsElements.ScrollViewerOf(element)
				?? throw new NotSupportedException(
					$"A {element.GetType().Name} named \"{name}\" has no scroll viewer in its template.");
			offset = scroller.HorizontalOffset;
		}).ConfigureAwait(false);

		return offset;
	}

	private static async Task<double> VerticalOffsetAsync(string name)
	{
		var offset = 0.0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			var scroller = ItemsElements.ScrollViewerOf(element)
				?? throw new NotSupportedException(
					$"A {element.GetType().Name} named \"{name}\" has no scroll viewer in its template.");
			offset = scroller.VerticalOffset;
		}).ConfigureAwait(false);

		return offset;
	}

	private async Task SwipeFlipViewAsync(
		string name, bool toTheLeft, double? shareOfWidth = null, TimeSpan? settle = null)
	{
		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
		var (centerX, y) = bounds.Center;

		// A swipe has to start well inside the view and travel a known share of its width,
		// because how far the finger has taken the content is what decides where the view
		// snaps to when the finger lifts.
		var travel = (int) (bounds.Width * (shareOfWidth ?? SwipeShareOfWidth));
		var startX = toTheLeft ? centerX + (travel / 2) : centerX - (travel / 2);
		await DragAsync(startX, y, toTheLeft ? -travel : travel, 0, settle).ConfigureAwait(false);
	}

	private static async Task DragAsync(
		int startX, int startY, int deltaX, int deltaY, TimeSpan? settle = null)
	{
		TestTargetFixture.Session.TouchPress(PointerId, startX, startY);
		await DelayAsync(DragPressDelay).ConfigureAwait(false);

		for (var step = 1; step <= DragSteps; step++)
		{
			var x = startX + (int) Math.Round(deltaX * (step / (double) DragSteps));
			var y = startY + (int) Math.Round(deltaY * (step / (double) DragSteps));
			TestTargetFixture.Session.TouchMove(PointerId, x, y);
			await DelayAsync(DragStepDelay).ConfigureAwait(false);
		}

		TestTargetFixture.Session.TouchRelease(PointerId, startX + deltaX, startY + deltaY);
		await SettleAsync(settle ?? DragSettleDelay).ConfigureAwait(false);
	}

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
		await DelayAsync(delay).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static Task DelayAsync(TimeSpan delay) =>
		Task.Delay(delay, TestContext.Current.CancellationToken);
}
