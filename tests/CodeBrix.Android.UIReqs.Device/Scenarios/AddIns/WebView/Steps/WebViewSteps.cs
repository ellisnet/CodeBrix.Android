using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.WebView.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Reqnroll;
using SilverAssertions;
using Windows.UI;
using Xunit;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;
using WebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace CodeBrix.Platform.UI.AddIn.WebView.UIReqs.Steps;

/// <summary>
/// The WebView group's vocabulary: the one control the scenarios share, the pages it is given,
/// and the four things the core harness cannot say about an engine that runs in another process
/// - that a navigation has finished, that a picture has arrived on the panel, what a script
/// answered, and what the page said back to its host.
/// <para>
/// Everything else a scenario says - showing the control, sizing it, tapping it, typing into it,
/// where the colours landed, which pixels changed - is the core harness's own vocabulary,
/// reached through this project's reqnroll.json binding assemblies. Nothing from the core
/// project is duplicated here.
/// </para>
/// <para>
/// There is no sleep anywhere in this class. The engine raises no event when a frame reaches the
/// panel - a navigation reports completion when the LOAD ends, which is before the picture is
/// composited - so the two waits that need it are BOUNDED POLLS with the budget written in the
/// feature file: the requirement then reads "within so many milliseconds" and a run that never
/// gets there fails with a number and a picture instead of hanging. The engine's own web process
/// has no failure event on this head, so a budget that expires is also how a dead web process
/// shows itself; the failure messages say so.
/// </para>
/// </summary>
[Binding]
public sealed class WebViewSteps
{
	/// <summary>The kind a feature file asks for to get the shared web view control.</summary>
	public const string WebViewKind = "WebView";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-wpe</c> tag: the system
	/// web engine this add-in binds. It is not a package - it is three shared libraries that come
	/// from the machine - so a machine without them reports "skipped: wpe is not on this machine"
	/// with the install line, rather than failing every scenario over a region that is blank
	/// because no engine ever started.
	/// </summary>
	public const string EnginePrerequisite = "wpe";

	/// <summary>
	/// The colour the engine's warm-up page paints. It is shared with no other page these
	/// scenarios show, which is what makes every later colour claim a claim about the page the
	/// scenario navigated to rather than about whatever was left on the panel.
	/// </summary>
	public static readonly Color WarmupColor = Color.FromArgb(0xFF, 0x80, 0x80, 0x80);

	/// <summary>
	/// How long the harness waits for a script to answer. A script that runs at all answers in
	/// milliseconds; this budget is what turns a web process that has died into a failure that
	/// says so.
	/// </summary>
	public static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(15);

	/// <summary>How often a bounded wait looks again while it is waiting.</summary>
	public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

	private const double UniformFraction = 0.99;

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	private readonly IReqnrollOutputHelper _outputHelper;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	/// <param name="outputHelper">Where a step's output goes.</param>
	public WebViewSteps(ScenarioContext scenarioContext, IReqnrollOutputHelper outputHelper)
	{
		_scenarioContext = scenarioContext;
		_outputHelper = outputHelper;
	}

	// -------------------------------------------------------- registration

	/// <summary>
	/// Looks for the system web engine and teaches the element factory this group's one noun,
	/// before the first scenario. The engine is three shared libraries loaded by name at run
	/// time, so the only honest way to ask whether this machine has it is to try to load each
	/// one; the add-in itself names the first missing one the same way.
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_WebView_vocabulary()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;

			try
			{
				if (MissingEngineLibrary() is { } missing)
				{
					Prerequisite.Missing(EnginePrerequisite, missing);
					return;
				}

				// One control for the whole run: the kind hands back the SAME control every time
				// a scenario asks for a "WebView". The factory names it, points the scenario's
				// name at it and centres it, exactly as it does for a control it just built.
				ElementFactory.RegisterKind(WebViewKind, () => WebViewFixture.Shared);
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(EnginePrerequisite,
					$"the WebView add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	/// <summary>
	/// Forgets what the previous scenario's page did. It runs after the harness has decided
	/// whether this scenario is skipped, so a skipped scenario touches nothing at all.
	/// </summary>
	[BeforeScenario(Order = 200)]
	public static void Begin_the_scenario() => WebViewFixture.BeginScenario();

	/// <summary>
	/// Takes the shared control off whatever was holding it. The harness empties the root panel
	/// after every scenario, which is enough for a control the root itself held - but a scenario
	/// that put this one inside a layout of its own would leave it parented to a panel that is no
	/// longer showing, and the next scenario could not show it.
	/// </summary>
	/// <returns>A task that completes once the control has no parent.</returns>
	[AfterScenario(Order = 50)]
	public static async Task End_the_scenario() => await WebViewFixture.DetachAsync().ConfigureAwait(false);

	// ------------------------------------------------------------- the engine

	/// <summary>
	/// Starts the engine and puts a known picture on the panel, which is the state every
	/// scenario here begins from.
	/// <para>
	/// It is one step and not two because the two halves are one requirement: the control has a
	/// native view (which is what the framework's own "ensure" reports), and that view has
	/// painted something a person can see. The FIRST scenario of a run pays for the engine's
	/// thread and its web process here, which is why the budget a feature file writes is large;
	/// every later scenario finds the engine running and this step costs a navigation.
	/// </para>
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="milliseconds">How long the engine has to start and paint.</param>
	/// <returns>A task that completes once the warm-up page is on the panel.</returns>
	[Given("the web engine of {string} has started within {int} milliseconds")]
	public async Task Given_the_web_engine_of_has_started_within(string elementName, int milliseconds)
	{
		var budget = TimeSpan.FromMilliseconds(milliseconds);
		var browser = WebViewOf(elementName);

		var ready = Task.CompletedTask;
		await TestTargetFixture.RunOnUIThreadAsync(() => ready = browser.EnsureCoreWebView2Async().AsTask())
			.ConfigureAwait(false);
		await ready.WaitAsync(budget, TestContext.Current.CancellationToken).ConfigureAwait(false);

		await NavigateAsync(browser, () => browser.NavigateToString(HtmlPages.Get(HtmlPages.Warmup)))
			.ConfigureAwait(false);

		var elapsed = Stopwatch.StartNew();
		_ = await Poll.UntilTheRegionShowsAsync(_scenarioContext, elementName,
			region => Matches(region, WarmupColor), budget, PollInterval).ConfigureAwait(false);
		elapsed.Stop();

		if (!WebViewFixture.IsEngineStarted)
		{
			WebViewFixture.RecordFirstFrame(elapsed.ElapsedMilliseconds);
			var latency = string.Create(CultureInfo.InvariantCulture,
				$"{elapsed.ElapsedMilliseconds} ms");
			_outputHelper.WriteLine(
				$"the engine's first composited frame reached the panel {latency} after the page was handed over");
		}

		var region = await RegionAsync(elementName).ConfigureAwait(false);
		region.IsUniformly(WarmupColor, UniformFraction);
	}

	// ---------------------------------------------------------- navigation

	/// <summary>
	/// Hands the control a page as text. The page never leaves this process: the control turns
	/// it into a <c>data:</c> document, which is what its navigation is announced with.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="pageName">The name of the page in <see cref="HtmlPages"/>.</param>
	/// <returns>A task that completes once the navigation has been asked for.</returns>
	[Given("{string} shows the page {string}")]
	[When("{string} shows the page {string}")]
	public async Task When_shows_the_page(string elementName, string pageName)
	{
		var browser = WebViewOf(elementName);
		var document = HtmlPages.Get(GherkinValue.Unquote(pageName));
		await NavigateAsync(browser, () => browser.NavigateToString(document)).ConfigureAwait(false);
	}

	/// <summary>
	/// Points the control at one of the two HTML files that ship beside these scenarios, through
	/// a <c>file://</c> URI. It is the only route to a page the control did not receive as text,
	/// and it is deliberately a local file: nothing in this assembly ever reaches a network.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="fileName">The file name under the assembly's Assets folder.</param>
	/// <returns>A task that completes once the navigation has been asked for.</returns>
	[Given("{string} shows the local page {string}")]
	[When("{string} shows the local page {string}")]
	public async Task When_shows_the_local_page(string elementName, string fileName)
	{
		var browser = WebViewOf(elementName);
		var address = LocalPageUri(GherkinValue.Unquote(fileName));
		await NavigateAsync(browser, () => browser.Source = address).ConfigureAwait(false);
	}

	/// <summary>
	/// Asks for a page and refuses it as it is announced, which is what an application does when
	/// it will not let its web view leave the page it is on. The step returns once the refusal
	/// has been made, so what follows is about a control that has been told no.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="pageName">The name of the page in <see cref="HtmlPages"/>.</param>
	/// <param name="milliseconds">How long the control has to announce the navigation.</param>
	/// <returns>A task that completes once the navigation has been announced and refused.</returns>
	[When("the navigation of {string} to the page {string} is refused within {int} milliseconds")]
	public async Task When_the_navigation_to_the_page_is_refused(string elementName, string pageName,
		int milliseconds)
	{
		var browser = WebViewOf(elementName);
		var document = HtmlPages.Get(GherkinValue.Unquote(pageName));
		var announced = WebViewFixture.NavigationsStarted;

		WebViewFixture.RefuseTheNextNavigation();
		await NavigateAsync(browser, () => browser.NavigateToString(document)).ConfigureAwait(false);

		var refused = await Poll.UntilAsync(
			async () =>
			{
				await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
				return WebViewFixture.NavigationsStarted > announced;
			},
			TimeSpan.FromMilliseconds(milliseconds), PollInterval).ConfigureAwait(false);

		refused.Should().BeTrue(
			"\"{0}\" must announce the navigation it was asked for, which is the only moment one can be "
			+ "refused; it announced none within {1} ms",
			elementName, milliseconds);
	}

	/// <summary>Goes back to the page the control was showing before this one.</summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <returns>A task that completes once the control has been asked to go back.</returns>
	[Given("{string} goes back")]
	[When("{string} goes back")]
	public async Task When_goes_back(string elementName)
	{
		var browser = WebViewOf(elementName);
		await NavigateAsync(browser, browser.GoBack).ConfigureAwait(false);
	}

	/// <summary>Loads the page the control is showing all over again.</summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <returns>A task that completes once the control has been asked to reload.</returns>
	[Given("{string} is reloaded")]
	[When("{string} is reloaded")]
	public async Task When_is_reloaded(string elementName)
	{
		var browser = WebViewOf(elementName);
		await NavigateAsync(browser, browser.Reload).ConfigureAwait(false);
	}

	/// <summary>
	/// Waits for the navigation the scenario asked for to finish. This is a real completion
	/// signal - the control raises it when the load ends - but it is not a picture: the frame the
	/// page painted is composited afterwards, which is why every visual claim here is a separate,
	/// bounded wait of its own.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="milliseconds">How long the navigation has to finish.</param>
	/// <returns>A task that completes when the navigation has.</returns>
	[Given("the navigation of {string} completes within {int} milliseconds")]
	[Then("the navigation of {string} completes within {int} milliseconds")]
	public async Task Then_the_navigation_completes_within(string elementName, int milliseconds)
	{
		var mark = NavigationMark(elementName);
		var completed = await Poll.UntilAsync(
			async () =>
			{
				await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
				return WebViewFixture.NavigationsCompleted > mark;
			},
			TimeSpan.FromMilliseconds(milliseconds), PollInterval).ConfigureAwait(false);

		completed.Should().BeTrue(
			"the navigation \"{0}\" was asked for must report that it finished within {1} ms; the engine "
			+ "raises no other event when its web process dies, so a budget that runs out here is also "
			+ "what a dead web process looks like",
			elementName, milliseconds);
	}

	/// <summary>Asserts that the navigation that finished reported success.</summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	[Then("the navigation of {string} reported success")]
	public void Then_the_navigation_reported_success(string elementName)
	{
		WebViewFixture.NavigationsCompleted.Should().BeGreaterThanOrEqualTo(1,
			"\"{0}\" must have completed a navigation before anything can be said about how it went",
			elementName);
		WebViewFixture.LastNavigationSucceeded.Should().BeTrue(
			"the navigation \"{0}\" completed must report success", elementName);
	}

	/// <summary>
	/// Asserts what the control announced its navigation as. A page handed over as text is
	/// announced as the <c>data:</c> document it becomes, which is the control's own account of
	/// where it is going - not what the engine reports once it is there.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="prefix">What the announced URI must begin with.</param>
	[Then("the navigation of {string} was announced as {string}")]
	public void Then_the_navigation_was_announced_as(string elementName, string prefix)
	{
		var expected = GherkinValue.Unquote(prefix);
		var announced = WebViewFixture.LastNavigationStartUri;

		(announced is not null && announced.StartsWith(expected, StringComparison.Ordinal)).Should().BeTrue(
			"the navigation \"{0}\" announced must begin with \"{1}\"; it announced {2}",
			elementName, expected, Describe(announced));
	}

	/// <summary>
	/// Asserts where the control says it is, read from the engine rather than from the property
	/// an application sets to send it somewhere.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="prefix">What the address must begin with.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the address of {string} begins with {string}")]
	public async Task Then_the_address_of_begins_with(string elementName, string prefix)
	{
		var expected = GherkinValue.Unquote(prefix);
		var address = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(
			() => address = WebViewOf(elementName).SourceFromCore ?? string.Empty).ConfigureAwait(false);

		address.StartsWith(expected, StringComparison.Ordinal).Should().BeTrue(
			"the address \"{0}\" reports must begin with \"{1}\"; it reports {2}",
			elementName, expected, Describe(address));
	}

	/// <summary>Asserts that the control can go back to a page it was showing before.</summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} can go back")]
	public async Task Then_can_go_back(string elementName)
	{
		var canGoBack = await CanGoBackAsync(elementName).ConfigureAwait(false);
		canGoBack.Should().BeTrue("\"{0}\" must have a page behind it to go back to", elementName);
	}

	/// <summary>Asserts that the control has nothing behind it to go back to.</summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("{string} cannot go back")]
	public async Task Then_cannot_go_back(string elementName)
	{
		var canGoBack = await CanGoBackAsync(elementName).ConfigureAwait(false);
		canGoBack.Should().BeFalse("\"{0}\" must have nothing behind it to go back to", elementName);
	}

	// -------------------------------------------------------------- pixels

	/// <summary>
	/// Waits until one rectangle INSIDE the control is painted one colour. The coordinates are
	/// measured from the control's own top left corner, so they are the numbers the page was
	/// told to draw at.
	/// </summary>
	/// <param name="width">The block's width.</param>
	/// <param name="height">The block's height.</param>
	/// <param name="x">How far right of the control's left edge the block starts.</param>
	/// <param name="y">How far below the control's top edge the block starts.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="color">The colour the block must be.</param>
	/// <param name="milliseconds">How long it has to become that colour.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("the {int} by {int} block at {int}, {int} inside {string} becomes uniformly {string} within {int} milliseconds")]
	[Then("the {int} by {int} block at {int}, {int} inside {string} becomes uniformly {string} within {int} milliseconds")]
	public async Task Then_the_block_inside_becomes_uniformly_within(int width, int height, int x, int y,
		string elementName, Color color, int milliseconds)
	{
		var block = new DeviceRect(x, y, width, height);
		var description = string.Create(CultureInfo.InvariantCulture,
			$"the {width} by {height} block at {x}, {y} inside \"{elementName}\"");

		_ = await Poll.UntilAsync(
			async () =>
			{
				await ScenarioFrames.CaptureAsync(_scenarioContext, ScenarioFrames.CurrentFrameName)
					.ConfigureAwait(false);
				var region = await ScenarioFrames
					.SubRegionAsync(_scenarioContext, elementName, block, description).ConfigureAwait(false);
				return Matches(region, color);
			},
			TimeSpan.FromMilliseconds(milliseconds), PollInterval).ConfigureAwait(false);

		var painted = await ScenarioFrames
			.SubRegionAsync(_scenarioContext, elementName, block, description).ConfigureAwait(false);
		painted.IsUniformly(color, UniformFraction);
	}

	// ------------------------------------------------------------- scripts

	/// <summary>
	/// Runs a script in the page and throws the answer away. This is how a scenario reaches into
	/// the page for something that is not a requirement of its own - focusing a text box, or
	/// changing what the page is painted.
	/// </summary>
	/// <param name="script">The JavaScript to run.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <returns>A task that completes once the script has run.</returns>
	[Given("the script {string} runs in {string}")]
	[When("the script {string} runs in {string}")]
	public async Task When_the_script_runs_in(string script, string elementName) =>
		await RunScriptAsync(elementName, GherkinValue.Unquote(script)).ConfigureAwait(false);

	/// <summary>
	/// Runs a script in the page and asserts the text it answered with. The engine answers with a
	/// JSON literal, because a script may answer with any JavaScript value; the harness reads the
	/// string out of that literal, so a feature file states the text a person would expect rather
	/// than its encoding.
	/// </summary>
	/// <param name="script">The JavaScript to run.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="expected">The text the script must answer with.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the script {string} in {string} returns the text {string}")]
	public async Task Then_the_script_in_returns_the_text(string script, string elementName, string expected)
	{
		var answer = await RunScriptAsync(elementName, GherkinValue.Unquote(script)).ConfigureAwait(false);
		var text = ReadJsonString(answer, script);

		text.Should().Be(GherkinValue.Unquote(expected),
			"the script \"{0}\" run in \"{1}\" answered {2}", script, elementName, Describe(answer));
	}

	/// <summary>
	/// Runs a script in the page until it answers with the text the scenario expects, or the
	/// budget runs out. It is the bounded form of the assertion above, for the one thing that
	/// reaches the page WITHOUT going through the engine's own reply: what a person types travels
	/// from the panel to the engine's thread and on to the process that owns the page, and the
	/// script that reads it back is a separate journey - so the page can legitimately be one
	/// keystroke behind when it is first asked.
	/// </summary>
	/// <param name="script">The JavaScript to run.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="expected">The text the script must answer with.</param>
	/// <param name="milliseconds">How long it has to answer with it.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("the script {string} in {string} returns the text {string} within {int} milliseconds")]
	[Then("the script {string} in {string} returns the text {string} within {int} milliseconds")]
	public async Task Then_the_script_in_returns_the_text_within(string script, string elementName,
		string expected, int milliseconds)
	{
		var code = GherkinValue.Unquote(script);
		var wanted = GherkinValue.Unquote(expected);
		var answer = default(string);

		_ = await Poll.UntilAsync(
			async () =>
			{
				answer = await RunScriptAsync(elementName, code).ConfigureAwait(false);
				return string.Equals(ReadJsonString(answer, script), wanted, StringComparison.Ordinal);
			},
			TimeSpan.FromMilliseconds(milliseconds), PollInterval).ConfigureAwait(false);

		ReadJsonString(answer, script).Should().Be(wanted,
			"the script \"{0}\" run in \"{1}\" answered {2} within {3} ms",
			script, elementName, Describe(answer), milliseconds);
	}

	/// <summary>
	/// Asserts that the engine's answer to a script is a JSON literal rather than a bare value.
	/// It is the shape of the answer that an application has to know about, and the reason the
	/// text assertion above has to read one out.
	/// </summary>
	/// <param name="script">The JavaScript to run.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the script {string} in {string} answers with a JSON string")]
	public async Task Then_the_script_in_answers_with_a_JSON_string(string script, string elementName)
	{
		var answer = await RunScriptAsync(elementName, GherkinValue.Unquote(script)).ConfigureAwait(false);
		var quoted = answer is { Length: >= 2 }
			&& answer.StartsWith('"')
			&& answer.EndsWith('"');

		quoted.Should().BeTrue(
			"the script \"{0}\" run in \"{1}\" must answer with a JSON string literal, quotation marks and "
			+ "all; it answered {2}",
			script, elementName, Describe(answer));
	}

	/// <summary>Asserts what the engine reports as the title of the page it is showing.</summary>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="expected">The title the page gave itself.</param>
	/// <param name="milliseconds">How long the engine has to report it.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("the DocumentTitle of {string} becomes {string} within {int} milliseconds")]
	[Then("the DocumentTitle of {string} becomes {string} within {int} milliseconds")]
	public async Task Then_the_DocumentTitle_becomes_within(string elementName, string expected, int milliseconds)
	{
		var wanted = GherkinValue.Unquote(expected);
		_ = await Poll.UntilAsync(
			async () => string.Equals(await DocumentTitleAsync(elementName).ConfigureAwait(false), wanted,
				StringComparison.Ordinal),
			TimeSpan.FromMilliseconds(milliseconds), PollInterval).ConfigureAwait(false);

		var title = await DocumentTitleAsync(elementName).ConfigureAwait(false);
		title.Should().Be(wanted, "the title \"{0}\" reports for the page it is showing was asserted",
			elementName);
	}

	// ------------------------------------------------------------ messages

	/// <summary>
	/// Waits for a message the page posted to its host. A page posts when it likes - as it
	/// loads, or because a finger landed on it - so the wait is bounded and the budget is in the
	/// feature file.
	/// </summary>
	/// <param name="message">The message the page posts.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="milliseconds">How long the page has to post it.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("the web message {string} reaches {string} within {int} milliseconds")]
	[Then("the web message {string} reaches {string} within {int} milliseconds")]
	public async Task Then_the_web_message_reaches_within(string message, string elementName, int milliseconds) =>
		await WaitForMessagesAsync(message, elementName, 1, milliseconds).ConfigureAwait(false);

	/// <summary>
	/// Waits until a page has posted the same message a stated number of times, and asserts that
	/// it posted it exactly that often - which is how "the page ran again" is said about a page
	/// that speaks as it loads.
	/// </summary>
	/// <param name="message">The message the page posts.</param>
	/// <param name="elementName">The Gherkin name of the web view.</param>
	/// <param name="times">How often it must have been posted.</param>
	/// <param name="milliseconds">How long the page has to post it that often.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("the web message {string} reaches {string} {int} times within {int} milliseconds")]
	[Then("the web message {string} reaches {string} {int} times within {int} milliseconds")]
	public async Task Then_the_web_message_reaches_times_within(string message, string elementName, int times,
		int milliseconds) =>
		await WaitForMessagesAsync(message, elementName, times, milliseconds).ConfigureAwait(false);

	// ------------------------------------------------------------- helpers

	private async Task WaitForMessagesAsync(string message, string elementName, int times, int milliseconds)
	{
		var wanted = GherkinValue.Unquote(message);
		_ = await Poll.UntilAsync(
			async () =>
			{
				await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
				return WebViewFixture.MessageCount(wanted) >= times;
			},
			TimeSpan.FromMilliseconds(milliseconds), PollInterval).ConfigureAwait(false);

		WebViewFixture.MessageCount(wanted).Should().Be(times,
			"the page in \"{0}\" must post \"{1}\" {2} time(s) within {3} ms; it posted [{4}]",
			elementName, wanted, times, milliseconds, string.Join(", ", WebViewFixture.RecordedMessages()));
	}

	private async Task<string?> RunScriptAsync(string elementName, string script)
	{
		var browser = WebViewOf(elementName);
		var answering = Task.FromResult<string?>(null);
		await TestTargetFixture.RunOnUIThreadAsync(() => answering = browser.ExecuteScriptAsync(script).AsTask())
			.ConfigureAwait(false);

		try
		{
			var answer = await answering.WaitAsync(ScriptTimeout, TestContext.Current.CancellationToken)
				.ConfigureAwait(false);
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
			return answer;
		}
		catch (TimeoutException timeout)
		{
			var subject = string.Create(CultureInfo.InvariantCulture,
				$"The script \"{script}\" run in \"{elementName}\" did not answer within {ScriptTimeout}.");

			throw new TimeoutException(
				subject + " The engine's web process raises no failure event on this head, so a script that "
				+ "never answers is what a web process that has stopped looks like.", timeout);
		}
	}

	private async Task NavigateAsync(WebView2 browser, Action navigate)
	{
		_scenarioContext[NavigationMarkKey(browser.Name)] = WebViewFixture.NavigationsCompleted;
		await TestTargetFixture.RunOnUIThreadAsync(navigate).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	private int NavigationMark(string elementName)
	{
		if (_scenarioContext.TryGetValue(NavigationMarkKey(elementName), out var stored) && stored is int mark)
		{
			return mark;
		}

		throw new InvalidOperationException(
			$"The scenario has not asked \"{elementName}\" to go anywhere, so there is no navigation of its "
			+ "own to wait for. A scenario starts one with \"<name>\" shows the page \"<page>\".");
	}

	private static string NavigationMarkKey(string elementName) => "uireqs.webview.navigation." + elementName;

	private Task<Region> RegionAsync(string elementName) =>
		ScenarioFrames.RegionAsync(_scenarioContext, elementName);

	private static bool Matches(Region region, Color color) =>
		CanvasAssert.FractionMatching(region, color) >= UniformFraction;

	private static async Task<bool> CanGoBackAsync(string elementName)
	{
		var canGoBack = false;
		await TestTargetFixture.RunOnUIThreadAsync(() => canGoBack = WebViewOf(elementName).CanGoBack)
			.ConfigureAwait(false);
		return canGoBack;
	}

	private static async Task<string> DocumentTitleAsync(string elementName)
	{
		var title = string.Empty;
		await TestTargetFixture.RunOnUIThreadAsync(
			() => title = WebViewOf(elementName).CoreWebView2.DocumentTitle ?? string.Empty).ConfigureAwait(false);
		return title;
	}

	private static string ReadJsonString(string? answer, string script)
	{
		if (answer is null)
		{
			throw new InvalidOperationException(
				$"The script \"{script}\" answered nothing at all, which is what a control with no native "
				+ "web view behind it answers. Has the engine started?");
		}

		try
		{
			return JsonSerializer.Deserialize<string>(answer) ?? string.Empty;
		}
		catch (JsonException failure)
		{
			throw new InvalidOperationException(
				$"The script \"{script}\" answered {Describe(answer)}, which is not the JSON string literal "
				+ "the engine answers a script with when the script's value is a string.", failure);
		}
	}

	private static string Describe(string? value) =>
		value is null ? "nothing" : "\"" + value + "\"";

	private static WebView2 WebViewOf(string elementName)
	{
		var element = ElementRegistry.Resolve(elementName);
		return element as WebView2 ?? throw new NotSupportedException(NotAWebView(element));
	}

	// The message is built before it is handed over: string.Create's culture overload takes an
	// interpolated-string handler by reference, and a CONCATENATION of interpolated strings
	// cannot be passed that way (CS1620).
	private static string NotAWebView(FrameworkElement element)
	{
		var subject = string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\"");

		return $"{subject} is not a web view, so it can show no page. Ask for a \"{WebViewKind}\".";
	}

	// ANDROID PORT (b): an APK has no folder beside its assemblies, so the pages of Assets/ (copied verbatim next to
	// this folder) are written, byte for byte, into the app's temporary directory the first time a scenario asks for
	// one; the control then loads them as file URIs exactly as on the desktop.
	private static readonly string LocalPageFolder = WriteLocalPages(Path.Combine(Path.GetTempPath(), "uireqs-webview", "Assets"));

	private static string WriteLocalPages(string folder)
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, "page.html"), "<!DOCTYPE html>\n<html>\n<head><meta charset=\"utf-8\"><title>First local page</title></head>\n<body style=\"margin:0;background:#800080\"></body>\n</html>\n");
		File.WriteAllText(Path.Combine(folder, "second.html"), "<!DOCTYPE html>\n<html>\n<head><meta charset=\"utf-8\"><title>Second local page</title></head>\n<body style=\"margin:0;background:#008080\"></body>\n</html>\n");
		File.WriteAllText(Path.Combine(folder, "talkative.html"), "<!DOCTYPE html>\n<html>\n<head><meta charset=\"utf-8\"><title>A local page that talks to its host</title></head>\n<body style=\"margin:0;background:#800080\">\n<script>\nwindow.addEventListener('load', function () {\n\twindow.webkit.messageHandlers.codebrixWebView.postMessage('ready');\n});\n</script>\n</body>\n</html>\n");
		return folder;
	}

	private static Uri LocalPageUri(string fileName)
	{
		var path = Path.Combine(LocalPageFolder, fileName);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException(
				$"There is no page \"{fileName}\" beside the scenarios. The pages that ship with them are in "
				+ "the Assets folder of this project, copied to the output folder by the project file.", path);
		}

		return new Uri(path);
	}

	// ANDROID PORT (d): the engine is the system WebView of the device, not three Linux libraries; it is missing only
	// when the device has no WebView provider installed.
	private static string? MissingEngineLibrary() =>
		global::Android.Webkit.WebView.CurrentWebViewPackage == null
			? "the device has no system WebView (Android System WebView / Chrome) installed or enabled"
			: null;
}
