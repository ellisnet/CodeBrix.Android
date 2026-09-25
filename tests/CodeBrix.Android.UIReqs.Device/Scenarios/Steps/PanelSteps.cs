using System;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hooks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using Reqnroll;
using SilverAssertions;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// The steps about the panel itself: how big it is, that the application fills it, and that
/// nothing of an earlier scenario is left on it. Panel numbers always come from the running
/// session, never from a constant in a feature file, so one feature reads the same in the
/// Landscape and the Portrait assembly.
/// </summary>
[Binding]
public sealed class PanelSteps
{
	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public PanelSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>Runs the very reset the scenario hooks run between scenarios.</summary>
	/// <returns>A task that completes once the panel is empty and the UI thread is idle.</returns>
	[When("the scenario reset runs")]
	public async Task When_the_scenario_reset_runs()
	{
		await ScenarioHooks.CloseOpenPopupsAsync().ConfigureAwait(false);
		await TestTargetFixture.ClearContentAsync().ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		ElementRegistry.Clear();
		EventRecorder.Clear();
	}

	/// <summary>Asserts that the application's root panel covers the whole panel.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the root fills the panel")]
	public async Task Then_the_root_fills_the_panel()
	{
		var (width, height) = TestTargetFixture.PanelSize;
		var actualWidth = 0;
		var actualHeight = 0;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var root = VirtualApplication.Instance.Root;
			actualWidth = (int) Math.Round(root.ActualWidth);
			actualHeight = (int) Math.Round(root.ActualHeight);
		}).ConfigureAwait(false);

		actualWidth.Should().Be(width, "the root must fill the {0}", TestTargetFixture.DescribePanel());
		actualHeight.Should().Be(height, "the root must fill the {0}", TestTargetFixture.DescribePanel());
	}

	/// <summary>Asserts that the captured frame is exactly the panel.</summary>
	[Then("the frame is the size of the panel")]
	public void Then_the_frame_is_the_size_of_the_panel()
	{
		var (width, height) = TestTargetFixture.PanelSize;
		var frame = ScenarioFrames.Current(_scenarioContext);

		frame.Width.Should().Be(width, "the frame must be the whole {0}", TestTargetFixture.DescribePanel());
		frame.Height.Should().Be(height, "the frame must be the whole {0}", TestTargetFixture.DescribePanel());
	}

	/// <summary>Asserts that the panel is a landscape one.</summary>
	[Then("the panel is wider than it is tall")]
	public void Then_the_panel_is_wider_than_it_is_tall()
	{
		var (width, height) = TestTargetFixture.PanelSize;
		width.Should().BeGreaterThan(height, "this is the {0}", TestTargetFixture.DescribePanel());
	}

	/// <summary>Asserts that the panel is a portrait one.</summary>
	[Then("the panel is taller than it is wide")]
	public void Then_the_panel_is_taller_than_it_is_wide()
	{
		var (width, height) = TestTargetFixture.PanelSize;
		height.Should().BeGreaterThan(width, "this is the {0}", TestTargetFixture.DescribePanel());
	}

	/// <summary>Asserts which panel this assembly runs against.</summary>
	/// <param name="orientation">Landscape or Portrait.</param>
	[Then("the panel orientation is {string}")]
	public void Then_the_panel_orientation_is(TestDisplayOrientation orientation) =>
		TestTargetFixture.Orientation.Should().Be(orientation,
			"this assembly declared its panel with [assembly: TestPanel(...)]");

	/// <summary>
	/// Runs the popup half of that reset on its own, so that a requirement can be stated about
	/// it while the control that opened the popup is still on the panel.
	/// </summary>
	/// <returns>A task that completes once no popup is showing and the UI thread is idle.</returns>
	[When("the popups the scenario left open are closed")]
	public async Task When_the_popups_the_scenario_left_open_are_closed() =>
		await ScenarioHooks.CloseOpenPopupsAsync().ConfigureAwait(false);

	/// <summary>Asserts that something is open beside the application's root.</summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("a popup is open")]
	public async Task Then_a_popup_is_open() =>
		(await ScenarioHooks.OpenPopupCountAsync().ConfigureAwait(false)).Should().BeGreaterThan(0,
			"the scenario is about a popup that is showing");

	/// <summary>
	/// Asserts that nothing is left open beside the application's root. A popup is hosted there
	/// rather than below the root, so this is the half of the reset that emptying the root does
	/// not do: a popup left open goes on painting over the panel and its light-dismiss layer
	/// swallows the next tap.
	/// </summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("no popup is open")]
	public async Task Then_no_popup_is_open() =>
		(await ScenarioHooks.OpenPopupCountAsync().ConfigureAwait(false)).Should().Be(0,
			"a popup left open would still be over the panel when the next scenario starts");

	/// <summary>Asserts that the whole panel is the known background colour.</summary>
	[Then("the panel is blank")]
	public void Then_the_panel_is_blank() =>
		Region.WholeFrame(ScenarioFrames.Current(_scenarioContext)).IsBlank();

	/// <summary>
	/// Asserts that nothing a scenario built is left: no registered name, and no named element
	/// below the root apart from the root itself.
	/// </summary>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Given("no scenario content is present")]
	[Then("no scenario content is present")]
	public async Task Then_no_scenario_content_is_present()
	{
		var childCount = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			childCount = VirtualApplication.Instance.Root.Children.Count).ConfigureAwait(false);

		ElementRegistry.IsEmpty.Should().BeTrue(
			"a scenario starts with nothing registered, but [{0}] is",
			string.Join(", ", ElementRegistry.Names));
		childCount.Should().Be(0,
			"a scenario starts with an empty root, but the tree holds [{0}]",
			string.Join(", ", VirtualApplication.Instance.NamedElements()));
	}
}
