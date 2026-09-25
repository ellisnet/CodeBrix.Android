using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.AddIn.CommandBar.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hooks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.UI.Xaml.Media.Imaging.Svg;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Colors = CodeBrix.Platform.UI.Core.UIReqs.Support.Colors;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;
// The add-in's generated resource entry points are aliased under a name of their OWN rather than
// imported under their real one. This file's namespace sits BELOW CodeBrix.Platform.UI, and a type
// found by walking the enclosing namespaces beats a using-alias of the same name: written as
// "GlobalStaticResources" the alias is shadowed by the FRAMEWORK's class of that name, silently, and
// the calls below then register the framework's styles a second time instead of this add-in's -
// MEASURED as a ToolBar with no template and no items host at all.
using CommandBarResources = global::CodeBrix.Platform.UI.CommandBar.GlobalStaticResources;
using IconTintMode = CodeBrix.Platform.UI.CommandBar.IconTintMode;
using LabelMode = CodeBrix.Platform.UI.CommandBar.LabelMode;
using LabelPosition = CodeBrix.Platform.UI.CommandBar.LabelPosition;
using OverflowMode = CodeBrix.Platform.UI.CommandBar.OverflowMode;
using PopupMode = CodeBrix.Platform.UI.CommandBar.PopupMode;
using SvgIcon = CodeBrix.Platform.UI.CommandBar.SvgIcon;
using SvgIconSource = CodeBrix.Platform.UI.CommandBar.SvgIconSource;
using SvgProvider = CodeBrix.Platform.UI.Svg.SvgProvider;
using ToolBar = CodeBrix.Platform.UI.CommandBar.ToolBar;
using ToolBarGroup = CodeBrix.Platform.UI.CommandBar.ToolBarGroup;
using ToolBarOverflowButton = CodeBrix.Platform.UI.CommandBar.ToolBarOverflowButton;
using ToolBarProperties = CodeBrix.Platform.UI.CommandBar.ToolBarProperties;
using ToolBarSeparator = CodeBrix.Platform.UI.CommandBar.ToolBarSeparator;
using ToolBarSpacer = CodeBrix.Platform.UI.CommandBar.ToolBarSpacer;
using ToolBarTray = CodeBrix.Platform.UI.CommandBar.ToolBarTray;
using ToolButton = CodeBrix.Platform.UI.CommandBar.ToolButton;
using ToolDropDownButton = CodeBrix.Platform.UI.CommandBar.ToolDropDownButton;
using ToolToggleButton = CodeBrix.Platform.UI.CommandBar.ToolToggleButton;

namespace CodeBrix.Platform.UI.AddIn.CommandBar.UIReqs.Steps;

/// <summary>
/// The CommandBar group's vocabulary: the bar and the things that go in it, the artwork a button
/// shows, the two halves of a drop-down button, and the answers only the bar itself can give -
/// what it moved behind its chevron, what it drew between two groups, whether a toggle is on and
/// whether a menu is up.
/// <para>
/// Everything else a tool bar scenario says - showing the bar, sizing it, changing one of its
/// settings, tapping something, giving something the keyboard focus, what a region looks like,
/// where one item sits relative to another - is the core harness's own vocabulary, reached
/// through this project's reqnroll.json binding assemblies. Nothing from the core project is
/// duplicated here.
/// </para>
/// <para>
/// There is no sleep anywhere in this class. An icon is parsed and rasterised on the thread pool
/// with no completion event the add-in exposes, so the step that waits for one subscribes to the
/// framework image source's own Opened and OpenFailed events AND polls the rasterised bitmap
/// those events produce - which is what makes the wait correct for a fresh icon and for one the
/// icon cache handed over already finished.
/// </para>
/// </summary>
[Binding]
public sealed class CommandBarSteps
{
	/// <summary>The kind a feature file asks for to get a tool bar.</summary>
	public const string ToolBarKind = "ToolBar";

	/// <summary>The kind a feature file asks for to get the panel that holds several bars.</summary>
	public const string ToolBarTrayKind = "ToolBarTray";

	/// <summary>The kind a feature file asks for to get a run of items that belong together.</summary>
	public const string ToolBarGroupKind = "ToolBarGroup";

	/// <summary>The kind a feature file asks for to get an ordinary bar button.</summary>
	public const string ToolButtonKind = "ToolButton";

	/// <summary>The kind a feature file asks for to get a button that stays pressed.</summary>
	public const string ToolToggleButtonKind = "ToolToggleButton";

	/// <summary>The kind a feature file asks for to get a button with a menu.</summary>
	public const string ToolDropDownButtonKind = "ToolDropDownButton";

	/// <summary>The kind a feature file asks for to get a divider.</summary>
	public const string ToolBarSeparatorKind = "ToolBarSeparator";

	/// <summary>The kind a feature file asks for to get empty space.</summary>
	public const string ToolBarSpacerKind = "ToolBarSpacer";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-commandbar</c> tag: the
	/// add-in assembly, its default styles and the SVG provider its icons draw through. Every
	/// machine that can build this project has all three, so nothing is ever skipped for it here -
	/// but a run whose add-in could not be used reports "skipped: the CommandBar add-in is not
	/// usable" instead of failing fifteen times over an element kind the factory does not know.
	/// </summary>
	public const string CommandBarPrerequisite = "commandbar";

	/// <summary>The template part a drop-down button opens its menu from.</summary>
	public const string ArrowPartName = "PART_Arrow";

	/// <summary>The template part a button draws its icon and its label in.</summary>
	public const string LayoutPartName = "PART_Layout";

	/// <summary>
	/// How long an icon is given to parse and rasterise. Parsing runs on the thread pool and the
	/// first icon of a run pays for the SVG parser's own start-up, so the budget is generous; a
	/// run that never gets there fails with this number rather than hanging.
	/// </summary>
	public static readonly TimeSpan IconLoadBudget = TimeSpan.FromSeconds(10);

	/// <summary>How long a flyout is given to put its popup on the panel after a tap.</summary>
	public static readonly TimeSpan PopupBudget = TimeSpan.FromSeconds(5);

	/// <summary>How long a menu is given to report itself open after a tap on the arrow.</summary>
	public static readonly TimeSpan MenuBudget = TimeSpan.FromSeconds(5);

	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	/// <summary>
	/// Brings the add-in's default styles into a process that has no application of its own, and
	/// then teaches the element factory this add-in's nouns.
	/// <para>
	/// In an application the XAML source generator writes the application's own resource entry
	/// points, and the head calls them at start-up; that is how a button in a real application
	/// finds the template in the add-in's theme dictionary. A UIReqs project generates none of
	/// that, and MEASURED without these three calls a <c>new ToolBar()</c> is 0 by 0 with no
	/// template and no items host at all.
	/// </para>
	/// <para>
	/// The calls are made HERE, from a test-run hook, rather than from a module initializer as the
	/// add-in's own unit suite makes them: registering a resource dictionary creates one, and
	/// creating one asks the dispatcher whether the calling thread has access. The unit suite
	/// answers that with a process-wide override installed by a shim of its own; this harness has
	/// a real dispatcher and a real UI thread instead, so the calls are made ON that thread, after
	/// the core's <c>Order = 0</c> hook has launched the application. All three are idempotent, so
	/// a second call from anywhere is harmless.
	/// </para>
	/// </summary>
	/// <returns>A task that completes once a feature file may name this add-in's controls.</returns>
	[BeforeTestRun(Order = 10)]
	public static async Task Register_the_CommandBar_vocabulary()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;
		}

		try
		{
			// Without the SVG provider every icon in every bar draws nothing at all and says
			// nothing about it, so a run in that state would report blank buttons rather than the
			// one thing that is actually wrong.
			if (!ApiExtensibility.IsRegistered<ISvgProvider>())
			{
				Prerequisite.Missing(CommandBarPrerequisite,
					"nothing registered an SVG provider in this process, so no tool bar icon can be "
					+ "parsed (Registration.cs is the module initializer that does it)");
				return;
			}

			await RegisterTheDefaultStylesAsync().ConfigureAwait(false);
			RegisterTheNouns();
		}
		catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
			or FileLoadException or MissingMemberException)
		{
			// The add-in itself is what these scenarios are about, so a machine that cannot load
			// it has no requirement to state - it has a report to make.
			Prerequisite.Missing(CommandBarPrerequisite,
				$"the CommandBar add-in could not be used ({failure.GetType().Name}: {failure.Message})");
		}
	}

	// ------------------------------------------------------------ building a bar

	/// <summary>
	/// Fills a bar that is already showing from a table: one row per item, the first two columns
	/// naming what to build and what to call it, and every further column a property to set on it.
	/// </summary>
	/// <remarks>
	/// A bar holds Items rather than children, so the core harness's "the layout ... holds ..."
	/// cannot fill one; and a bar is only worth looking at with several items in it, which is why
	/// this takes a table rather than being written out one item at a time.
	/// </remarks>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <param name="items">A table whose header starts Kind, Name.</param>
	/// <returns>A task that completes once every item is in the bar and the tree is laid out.</returns>
	[Given("the bar {string} holds:")]
	[When("the bar {string} holds:")]
	public async Task Given_the_bar_holds(string barName, DataTable items) =>
		await AddItemsAsync(barName, items).ConfigureAwait(false);

	/// <summary>
	/// Fills a group that is already in a bar from the same table shape. A group is one item as
	/// far as the bar is concerned - it overflows whole - so its contents are named separately.
	/// </summary>
	/// <param name="groupName">The Gherkin name of the group.</param>
	/// <param name="items">A table whose header starts Kind, Name.</param>
	/// <returns>A task that completes once every item is in the group and the tree is laid out.</returns>
	[Given("the group {string} holds:")]
	[When("the group {string} holds:")]
	public async Task Given_the_group_holds(string groupName, DataTable items) =>
		await AddItemsAsync(groupName, items).ConfigureAwait(false);

	/// <summary>
	/// Gives a drop-down button the menu it opens: one panel of a known colour, so that "the menu
	/// is showing" is something a person can see on the frame.
	/// </summary>
	/// <remarks>
	/// A drop-down button keeps its menu in its own Flyout property rather than in the attached
	/// flyout every element has, which is what makes it a drop-down at all - so the core harness's
	/// flyout steps, which attach one, cannot give it its menu.
	/// </remarks>
	/// <param name="buttonName">The Gherkin name of the drop-down button.</param>
	/// <param name="panelName">The name the scenario refers to the menu's panel by.</param>
	/// <param name="width">The panel's width in logical pixels.</param>
	/// <param name="height">The panel's height in logical pixels.</param>
	/// <param name="color">The colour the panel is painted.</param>
	/// <returns>A task that completes once the button has its menu.</returns>
	[Given("the drop-down {string} has a menu panel named {string} {int} by {int} painted {string}")]
	public async Task Given_the_drop_down_has_a_menu_panel(string buttonName, string panelName,
		int width, int height, Color color) =>
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

			DropDown(buttonName).Flyout = new Flyout { Content = panel };
		}).ConfigureAwait(false);

	// ------------------------------------------------------------------- icons

	/// <summary>
	/// Waits until every icon in a bar has finished loading, so that what the next frame shows is
	/// the artwork rather than the empty square that stands in for it.
	/// </summary>
	/// <remarks>
	/// The add-in starts an icon's parse and does not keep the task, and it raises nothing of its
	/// own when the parse finishes; the framework's image source raises Opened or OpenFailed, and
	/// the SVG route rasterises the bitmap immediately after Opened. Both are used: the events say
	/// WHY a load failed, and the rasterised bitmap is what says an icon is ready even when the
	/// icon cache handed this button a source another button had already finished with, for which
	/// no event will ever be raised again.
	/// </remarks>
	/// <param name="name">The Gherkin name of the element the icons are inside.</param>
	/// <returns>A task that completes once every icon below that element has loaded.</returns>
	[Given("the icons of {string} are loaded")]
	[When("the icons of {string} are loaded")]
	public async Task Given_the_icons_of_are_loaded(string name)
	{
		var failures = new List<string>();
		var subscribed = new List<SvgImageSource>();

		var loaded = await Poll.UntilAsync(
			async () =>
			{
				var ready = false;
				await TestTargetFixture.RunOnUIThreadAsync(() =>
					ready = IconsAreReady(name, subscribed, failures)).ConfigureAwait(false);
				return ready;
			},
			IconLoadBudget,
			PollInterval).ConfigureAwait(false);

		if (!loaded)
		{
			var state = string.Empty;
			await TestTargetFixture.RunOnUIThreadAsync(() => state = DescribeIcons(name)).ConfigureAwait(false);
			var reported = failures.Count == 0 ? "none reported a failure" : string.Join("; ", failures);

			loaded.Should().BeTrue(
				"every icon in \"{0}\" must have loaded within {1}; the icons are [{2}] and {3}",
				name, IconLoadBudget, state, reported);
		}

		// The icon invalidates its own image from the parse, which may finish off the UI thread:
		// draining the dispatcher here means the next frame the scenario asks for shows it.
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts which of an icon's two artworks a button is drawing, which is the tree fact behind
	/// a theme swap.
	/// </summary>
	/// <param name="name">The Gherkin name of the button.</param>
	/// <param name="fileName">The artwork's file name, as <c>Assets/</c> spells it.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the icon of {string} is drawn from {string}")]
	public async Task Then_the_icon_of_is_drawn_from(string name, string fileName)
	{
		var resolved = default(Uri);
		await TestTargetFixture.RunOnUIThreadAsync(() => resolved = IconOf(name).ResolvedUriSource)
			.ConfigureAwait(false);

		var actual = resolved is null ? "no artwork at all" : Path.GetFileName(resolved.LocalPath);

		actual.Should().Be(GherkinValue.Unquote(fileName),
			"the artwork the icon of \"{0}\" resolved to was asserted", name);
	}

	// ---------------------------------------------------------------- overflow

	/// <summary>
	/// Registers the chevron the bar shows when its items do not all fit, under a name the rest of
	/// the scenario can talk about. The chevron is built by the bar itself and is in no feature
	/// file, so this is the only way a requirement can say anything about it.
	/// </summary>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <param name="chevronName">The name the scenario will refer to the chevron by.</param>
	/// <returns>A task that completes once the chevron is registered.</returns>
	[Given("the chevron of {string} is named {string}")]
	[When("the chevron of {string} is named {string}")]
	public async Task When_the_chevron_of_is_named(string barName, string chevronName)
	{
		ToolBarOverflowButton? chevron = null;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			chevron = VisualTreeSearch.FindDescendant<ToolBarOverflowButton>(Bar(barName));
			if (chevron is not null)
			{
				chevron.Name = chevronName;
				ElementRegistry.Register(chevronName, chevron);
			}
		}).ConfigureAwait(false);

		chevron.Should().NotBeNull(
			"the bar \"{0}\" must be showing a chevron for the scenario to talk about one", barName);
	}

	/// <summary>
	/// Registers the separator the bar put between two adjacent groups, under a name the rest of
	/// the scenario can talk about. Nothing in the feature file wrote that separator, which is the
	/// point of the requirement, so nothing else can name it.
	/// </summary>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <param name="separatorName">The name the scenario will refer to the separator by.</param>
	/// <returns>A task that completes once the separator is registered.</returns>
	[When("the separator between the groups of {string} is named {string}")]
	public async Task When_the_separator_between_the_groups_of_is_named(string barName, string separatorName)
	{
		var found = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var separators = VisualTreeSearch.FindDescendants<ToolBarSeparator>(Bar(barName));
			found = separators.Count;
			if (found == 1)
			{
				separators[0].Name = separatorName;
				ElementRegistry.Register(separatorName, separators[0]);
			}
		}).ConfigureAwait(false);

		found.Should().Be(1,
			"the bar \"{0}\" must have put exactly one separator between its two groups", barName);
	}

	/// <summary>Asserts that a bar has moved some of its items behind its chevron.</summary>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ToolBar {string} has overflow items")]
	public async Task Then_the_ToolBar_has_overflow_items(string barName) =>
		(await HasOverflowItemsAsync(barName).ConfigureAwait(false)).Should()
			.BeTrue("the bar \"{0}\" must have items it could not fit", barName);

	/// <summary>Asserts that everything a bar was given fits along it.</summary>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ToolBar {string} has no overflow items")]
	public async Task Then_the_ToolBar_has_no_overflow_items(string barName) =>
		(await HasOverflowItemsAsync(barName).ConfigureAwait(false)).Should()
			.BeFalse("everything the bar \"{0}\" was given must fit along it", barName);

	/// <summary>
	/// Asserts that the item a scenario named is one of the items the bar moved behind its
	/// chevron, and that it is the VERY element the scenario put in the bar rather than a copy of
	/// it - which is what lets an application read an overflowed button's state.
	/// </summary>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <param name="itemName">The Gherkin name of the item.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the overflow of {string} holds {string}")]
	public async Task Then_the_overflow_of_holds(string barName, string itemName)
	{
		var holds = false;
		var described = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var bar = Bar(barName);
			var item = ElementRegistry.Resolve(itemName);
			var overflow = bar.OverflowItems;
			holds = overflow.Any(candidate => ReferenceEquals(candidate, item));
			described = Describe(overflow);
		}).ConfigureAwait(false);

		holds.Should().BeTrue(
			"the very \"{0}\" the scenario put in \"{1}\" must be among its overflow items, which are [{2}]",
			itemName, barName, described);
	}

	/// <summary>
	/// Waits for the flyout a chevron opens to reach the panel, and asserts that it did. Opening a
	/// flyout builds a popup and animates it in, so the signal is the popup's arrival rather than
	/// the tap returning.
	/// </summary>
	/// <param name="barName">The Gherkin name of the bar.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the overflow flyout of {string} is showing")]
	public async Task Then_the_overflow_flyout_of_is_showing(string barName)
	{
		var showing = await Poll.UntilAsync(
			async () => await ScenarioHooks.OpenPopupCountAsync().ConfigureAwait(false) > 0,
			PopupBudget,
			PollInterval).ConfigureAwait(false);

		showing.Should().BeTrue(
			"the chevron of \"{0}\" must put its overflow flyout on the panel within {1}",
			barName, PopupBudget);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// ----------------------------------------------------------- the two halves

	/// <summary>Taps the part of a drop-down button that runs its command.</summary>
	/// <param name="name">The Gherkin name of the drop-down button.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the main part of {string} is tapped")]
	public async Task When_the_main_part_of_is_tapped(string name) =>
		await TapPartAsync(name, LayoutPartName).ConfigureAwait(false);

	/// <summary>Taps the part of a drop-down button that opens its menu.</summary>
	/// <param name="name">The Gherkin name of the drop-down button.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the arrow part of {string} is tapped")]
	public async Task When_the_arrow_part_of_is_tapped(string name) =>
		await TapPartAsync(name, ArrowPartName).ConfigureAwait(false);

	/// <summary>
	/// Asserts that a drop-down button's menu is up, waiting for it to say so. The button learns
	/// it from its flyout's own Opened event, which is raised as the popup arrives.
	/// </summary>
	/// <param name="name">The Gherkin name of the drop-down button.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the menu of {string} is open")]
	public async Task Then_the_menu_of_is_open(string name)
	{
		var open = await Poll.UntilAsync(
			async () =>
			{
				var opened = false;
				await TestTargetFixture.RunOnUIThreadAsync(() => opened = DropDown(name).IsFlyoutOpen)
					.ConfigureAwait(false);
				return opened;
			},
			MenuBudget,
			PollInterval).ConfigureAwait(false);

		open.Should().BeTrue("the menu of \"{0}\" must be open within {1}", name, MenuBudget);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts that a drop-down button's menu is not up.</summary>
	/// <param name="name">The Gherkin name of the drop-down button.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the menu of {string} is closed")]
	public async Task Then_the_menu_of_is_closed(string name)
	{
		var open = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => open = DropDown(name).IsFlyoutOpen)
			.ConfigureAwait(false);

		open.Should().BeFalse("the menu of \"{0}\" must still be closed", name);
	}

	// ------------------------------------------------------------- the toggle

	/// <summary>Asserts that a tool bar toggle is switched on.</summary>
	/// <param name="name">The Gherkin name of the toggle.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ToolToggleButton {string} is checked")]
	public async Task Then_the_ToolToggleButton_is_checked(string name) =>
		(await IsCheckedAsync(name).ConfigureAwait(false)).Should()
			.BeTrue("the toggle \"{0}\" must be checked", name);

	/// <summary>Asserts that a tool bar toggle is switched off.</summary>
	/// <param name="name">The Gherkin name of the toggle.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ToolToggleButton {string} is not checked")]
	public async Task Then_the_ToolToggleButton_is_not_checked(string name) =>
		(await IsCheckedAsync(name).ConfigureAwait(false)).Should()
			.BeFalse("the toggle \"{0}\" must not be checked", name);

	// --------------------------------------------------------------- the nouns

	private static async Task RegisterTheDefaultStylesAsync() =>
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			CommandBarResources.Initialize();
			CommandBarResources.RegisterDefaultStyles();
			CommandBarResources.RegisterResourceDictionariesBySource();
		}).ConfigureAwait(false);

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(ToolBarTrayKind, () => new ToolBarTray());
		ElementFactory.RegisterKind(ToolBarKind, () => new ToolBar());
		ElementFactory.RegisterKind(ToolBarGroupKind, () => new ToolBarGroup());
		ElementFactory.RegisterKind(ToolButtonKind, () => new ToolButton());
		ElementFactory.RegisterKind(ToolToggleButtonKind, BuildToolToggleButton);
		ElementFactory.RegisterKind(ToolDropDownButtonKind, () => new ToolDropDownButton());
		ElementFactory.RegisterKind(ToolBarSeparatorKind, () => new ToolBarSeparator());
		ElementFactory.RegisterKind(ToolBarSpacerKind, () => new ToolBarSpacer());

		// The four settings a bar hands down to its items are INHERITED ATTACHED properties: they
		// are set on the tray, the bar or the button and every item below reads them, so they go in
		// the general table under their own names and apply to whatever the scenario names.
		ElementFactory.RegisterProperty("IconSize",
			(element, value) => ToolBarProperties.SetIconSize(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("LabelMode",
			(element, value) => ToolBarProperties.SetLabelMode(element, GherkinValue.ToEnum<LabelMode>(value)));
		ElementFactory.RegisterProperty("LabelPosition",
			(element, value) =>
				ToolBarProperties.SetLabelPosition(element, GherkinValue.ToEnum<LabelPosition>(value)));
		ElementFactory.RegisterProperty("ShowToolTips",
			(element, value) => ToolBarProperties.SetShowToolTips(element, ReadBoolean(value)));

		// "Title" is a universal word - a dialog, an information bar and a teaching tip all have
		// one - so the bar's goes in as a TYPED setter, which leaves the word alone everywhere else.
		ElementFactory.RegisterProperty<ToolBar>("Title", (bar, value) => bar.Title = value);
		ElementFactory.RegisterProperty<ToolBar>("OverflowMode",
			(bar, value) => bar.OverflowMode = GherkinValue.ToEnum<OverflowMode>(value));
		ElementFactory.RegisterProperty<ToolBar>("ItemSpacing",
			(bar, value) => bar.ItemSpacing = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty<ToolBar>("IsCompact", (bar, value) => bar.IsCompact = ReadBoolean(value));
		ElementFactory.RegisterProperty<ToolBar>("SeparatorBetweenGroups",
			(bar, value) => bar.SeparatorBetweenGroups = ReadBoolean(value));

		// "Text" is the most universal word of all, and a bar button spells its own: typed, so a
		// TextBlock and a TextBox go on meaning what they meant.
		ElementFactory.RegisterProperty<ToolButton>("Text", (button, value) => button.Text = value);

		// The artwork and how it is painted. Each one fills in an icon source for the button if it
		// has not got one yet, so a table may write these columns in any order.
		ElementFactory.RegisterProperty<ToolButton>("Icon",
			(button, value) => IconSourceOf(button).Markup = DemoIcons.Document(value));
		ElementFactory.RegisterProperty<ToolButton>("IconArtwork",
			(button, value) => IconSourceOf(button).Source = DemoIcons.ArtworkUri(value));
		ElementFactory.RegisterProperty<ToolButton>("DarkIconArtwork",
			(button, value) => IconSourceOf(button).Dark = DemoIcons.ArtworkUri(value));
		ElementFactory.RegisterProperty<ToolButton>("IconTint",
			(button, value) => IconSourceOf(button).Tint = new SolidColorBrush(Colors.Parse(value)));
		ElementFactory.RegisterProperty<ToolButton>("IconTintMode",
			(button, value) => IconSourceOf(button).TintMode = GherkinValue.ToEnum<IconTintMode>(value));

		// A bar toggle is not the framework's ToggleButton - it is a bar button that stays pressed
		// - so the core harness's "IsChecked" cannot reach it, and this typed one can.
		ElementFactory.RegisterProperty<ToolToggleButton>("IsChecked",
			(toggle, value) => toggle.IsChecked = ReadBoolean(value));
		ElementFactory.RegisterProperty<ToolDropDownButton>("PopupMode",
			(button, value) => button.PopupMode = GherkinValue.ToEnum<PopupMode>(value));

		// A shape's "Fill" is a brush and a spacer's is a yes or a no, so the spacer's is typed.
		ElementFactory.RegisterProperty<ToolBarSpacer>("Fill",
			(spacer, value) => spacer.Fill = ReadBoolean(value));

		// Not "Thickness": the word already means a border's four sides everywhere else.
		ElementFactory.RegisterProperty<ToolBarSeparator>("LineThickness",
			(separator, value) => separator.Thickness = GherkinValue.ToDouble(value));

		// No colour NAME is registered for the add-in's palette on purpose: every theme colour a
		// scenario claims is written out in the feature file as the hexadecimal value the add-in's
		// theme dictionary states, so a person reviewing a requirement can check it against that
		// file without following a name into C#.
	}

	private static FrameworkElement BuildToolToggleButton()
	{
		var toggle = new ToolToggleButton();

		// The element factory names an element only after building it, so the name is read when
		// the event fires rather than when the handler is attached.
		toggle.IsCheckedChanged += (sender, _) => EventRecorder.Record(sender.Name, "IsCheckedChanged");
		return toggle;
	}

	// -------------------------------------------------------------- the inners

	private static async Task AddItemsAsync(string containerName, DataTable items)
	{
		ArgumentNullException.ThrowIfNull(items);

		var header = items.Header.ToArray();
		if (header.Length < 2
			|| !string.Equals(header[0], "Kind", StringComparison.Ordinal)
			|| !string.Equals(header[1], "Name", StringComparison.Ordinal))
		{
			throw new FormatException(
				"A table of tool bar items starts with a Kind column and a Name column; every "
				+ "further column is a property to set on the item that row builds. This one starts "
				+ $"[{string.Join(", ", header)}].");
		}

		foreach (var row in items.Rows)
		{
			// A bar item fills the slot the bar gives it, which is what a bar item written in XAML
			// does: the element factory centres a stand-alone element, and a centred separator
			// would be drawn at its own desired height, which is nothing at all.
			var properties = new List<KeyValuePair<string, string>>
			{
				new("HorizontalAlignment", "Stretch"),
				new("VerticalAlignment", "Stretch"),
			};

			for (var column = 2; column < header.Length; column++)
			{
				var value = row[header[column]];
				if (!string.IsNullOrWhiteSpace(value))
				{
					properties.Add(new KeyValuePair<string, string>(header[column], value));
				}
			}

			var item = await ElementFactory
				.CreateAsync(row[header[0]], row[header[1]], properties)
				.ConfigureAwait(false);

			await TestTargetFixture.RunOnUIThreadAsync(() =>
			{
				switch (ElementRegistry.Resolve(containerName))
				{
					case ToolBar bar:
						bar.Items.Add(item);
						break;
					case ToolBarGroup group:
						group.Children.Add(item);
						break;
					case Panel panel:
						panel.Children.Add(item);
						break;
					default:
						throw new NotSupportedException(
							$"\"{containerName}\" is not a tool bar or a group, so it holds no items.");
				}
			}).ConfigureAwait(false);
		}

		await TestTargetFixture.RunOnUIThreadAsync(() => VirtualApplication.Instance.Root.UpdateLayout())
			.ConfigureAwait(false);
	}

	private static async Task TapPartAsync(string name, string partName)
	{
		FrameworkElement part = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var button = ElementRegistry.Resolve(name);
			part = VisualTreeSearch.FindDescendantNamed(button, partName)
				?? throw new InvalidOperationException(
					$"\"{name}\" carries no \"{partName}\" part, so there is no half of it to tap.");
		}).ConfigureAwait(false);

		var bounds = await DeviceRect.OfAsync(part).ConfigureAwait(false);
		var (x, y) = bounds.Center;

		TestTargetFixture.Session.Tap(x, y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private static async Task<bool> HasOverflowItemsAsync(string barName)
	{
		var hasOverflow = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => hasOverflow = Bar(barName).HasOverflowItems)
			.ConfigureAwait(false);

		return hasOverflow;
	}

	private static async Task<bool> IsCheckedAsync(string name)
	{
		var isChecked = false;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var toggle = ElementRegistry.Resolve(name) as ToolToggleButton
				?? throw new NotSupportedException(
					$"\"{name}\" is not a ToolToggleButton, so it has nothing to be checked.");
			isChecked = toggle.IsChecked;
		}).ConfigureAwait(false);

		return isChecked;
	}

	/// <summary>
	/// Whether every icon below an element has finished loading. Call it on the UI thread.
	/// </summary>
	/// <param name="name">The Gherkin name of the element the icons are inside.</param>
	/// <param name="subscribed">The sources this step has already hooked, so it hooks each once.</param>
	/// <param name="failures">Where a source that reported a failure records what it was.</param>
	/// <returns><c>true</c> when every icon has a rasterised bitmap.</returns>
	private static bool IconsAreReady(string name, List<SvgImageSource> subscribed, List<string> failures)
	{
		var icons = VisualTreeSearch.FindDescendants<SvgIcon>(ElementRegistry.Resolve(name));
		if (icons.Count == 0)
		{
			return false;
		}

		var ready = true;
		foreach (var icon in icons)
		{
			if (icon.Source is not SvgImageSource source)
			{
				ready = false;
				continue;
			}

			if (!subscribed.Any(candidate => ReferenceEquals(candidate, source)))
			{
				subscribed.Add(source);
				source.OpenFailed += (sender, args) => failures.Add(
					string.Create(CultureInfo.InvariantCulture, $"one icon reported {args.Status}"));
			}

			if (SvgProvider.GetRasterizedPixelSize(source).Width <= 0)
			{
				ready = false;
			}
		}

		return ready;
	}

	/// <summary>What every icon below an element is showing. Call it on the UI thread.</summary>
	/// <param name="name">The Gherkin name of the element the icons are inside.</param>
	/// <returns>One description per icon.</returns>
	private static string DescribeIcons(string name)
	{
		var icons = VisualTreeSearch.FindDescendants<SvgIcon>(ElementRegistry.Resolve(name));
		if (icons.Count == 0)
		{
			return "no icon at all is below it";
		}

		return string.Join(", ", icons.Select(icon =>
		{
			var artwork = icon.ResolvedUriSource is { } uri
				? Path.GetFileName(uri.LocalPath)
				: string.IsNullOrEmpty(icon.Markup) ? "nothing" : "an inline document";
			var raster = icon.Source is SvgImageSource source
				? SvgProvider.GetRasterizedPixelSize(source).ToString()
				: "no image source";

			return string.Create(CultureInfo.InvariantCulture, $"{artwork} rasterised as {raster}");
		}));
	}

	private static SvgIcon IconOf(string name)
	{
		var element = ElementRegistry.Resolve(name);
		return VisualTreeSearch.FindDescendant<SvgIcon>(element)
			?? throw new NotSupportedException(
				$"\"{name}\" is showing no SVG icon, so nothing about its artwork can be asserted.");
	}

	private static SvgIconSource IconSourceOf(ToolButton button)
	{
		if (button.Icon is SvgIconSource existing)
		{
			return existing;
		}

		if (button.Icon is not null)
		{
			throw new NotSupportedException(
				$"The icon of \"{button.Name}\" is a {button.Icon.GetType().Name}, not an SVG one.");
		}

		var source = new SvgIconSource();
		button.Icon = source;
		return source;
	}

	private static ToolBar Bar(string name) =>
		ElementRegistry.Resolve(name) as ToolBar
		?? throw new NotSupportedException($"\"{name}\" is not a ToolBar.");

	private static ToolDropDownButton DropDown(string name) =>
		ElementRegistry.Resolve(name) as ToolDropDownButton
		?? throw new NotSupportedException($"\"{name}\" is not a ToolDropDownButton, so it has no menu.");

	private static string Describe(IReadOnlyList<UIElement> items) => items.Count == 0
		? "empty"
		: string.Join(", ", items.Select(item => item is FrameworkElement element && !string.IsNullOrEmpty(element.Name)
			? "\"" + element.Name + "\""
			: item.GetType().Name));

	private static bool ReadBoolean(string value) =>
		bool.TryParse(GherkinValue.Unquote(value), out var parsed)
			? parsed
			: throw new FormatException($"\"{value}\" is not True or False.");
}
