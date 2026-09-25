using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using Reqnroll.UnitTestProvider;
using Xunit;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hooks;

/// <summary>
/// The per-scenario hooks: they decide whether a scenario belongs to this assembly's panel at
/// all, they leave the panel empty for the next scenario - which means closing every popup as
/// well as emptying the root, because a popup is hosted beside the root and outlives it - and
/// when a scenario fails they turn the last frame into a PNG a person can open and a report a
/// person can read.
/// </summary>
[Binding]
public sealed class ScenarioHooks
{
	/// <summary>
	/// How long a dismissed popup is given to finish going away. Closing one starts an
	/// animation, and until that animation has run the popup's light-dismiss layer is still
	/// over the panel - where it would swallow the next scenario's first tap.
	/// </summary>
	public static readonly TimeSpan PopupDismissDelay = TimeSpan.FromMilliseconds(400);

	private readonly ScenarioContext _scenarioContext;
	private readonly FeatureContext _featureContext;
	private readonly IReqnrollOutputHelper _outputHelper;
	private readonly IUnitTestRuntimeProvider _unitTestRuntimeProvider;

	/// <summary>Reqnroll builds one of these per scenario and injects the contexts it asks for.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	/// <param name="featureContext">The current feature.</param>
	/// <param name="outputHelper">Where a step's output goes.</param>
	/// <param name="unitTestRuntimeProvider">The runner, which is what can skip a scenario.</param>
	public ScenarioHooks(
		ScenarioContext scenarioContext,
		FeatureContext featureContext,
		IReqnrollOutputHelper outputHelper,
		IUnitTestRuntimeProvider unitTestRuntimeProvider)
	{
		_scenarioContext = scenarioContext;
		_featureContext = featureContext;
		_outputHelper = outputHelper;
		_unitTestRuntimeProvider = unitTestRuntimeProvider;
	}

	/// <summary>
	/// Closes every popup that is open, whichever control opened it. A Flyout, a MenuFlyout and
	/// a bare Popup are all hosted BESIDE the application's root rather than inside it, so
	/// emptying the root does not take one off the panel: one left open would still be showing
	/// when the next scenario starts, and its light-dismiss layer would swallow that scenario's
	/// first tap. Closing them is therefore part of the reset every scenario gets, not
	/// something each steps class has to remember for its own controls.
	/// </summary>
	/// <returns>A task that completes once no popup is showing and the UI thread is idle.</returns>
	public static async Task CloseOpenPopupsAsync()
	{
		if (!TestTargetFixture.IsLaunched)
		{
			return;
		}

		var closed = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			foreach (var popup in OpenPopups())
			{
				closed++;

				// A popup a FlyoutBase owns is asked through its flyout first, because Hide is
				// the framework's own way of taking one of those down. The flyout is reached
				// through the popup's placement target - the control it was shown at - since a
				// popup's own link back to its flyout is internal to the framework.
				OwningFlyout(popup)?.Hide();

				// ... and then it is insisted upon: a Closing handler may cancel Hide, and a
				// bare Popup has no flyout to ask in the first place.
				popup.IsOpen = false;
			}
		}).ConfigureAwait(false);

		if (closed == 0)
		{
			return;
		}

		await Task.Delay(PopupDismissDelay, TestContext.Current.CancellationToken).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Lifts a finger a scenario left resting on the panel. A scenario that puts one down and
	/// then fails before its lift would otherwise hand the next scenario a panel with a pointer
	/// still captured on it, which is not the known state every scenario starts from.
	/// </summary>
	/// <returns>A task that completes once no finger is down and the UI thread is idle.</returns>
	public static async Task ReleaseAFingerLeftDownAsync()
	{
		if (!Finger.TryLift(out var position) || !TestTargetFixture.IsLaunched)
		{
			return;
		}

		TestTargetFixture.Session.TouchRelease(Finger.PointerId, position.X, position.Y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>How many popups are open right now, which is what "no popup is open" asks.</summary>
	/// <returns>The number of popups showing beside the application's root.</returns>
	public static async Task<int> OpenPopupCountAsync()
	{
		if (!TestTargetFixture.IsLaunched)
		{
			return 0;
		}

		var count = 0;
		await TestTargetFixture.RunOnUIThreadAsync(() => count = OpenPopups().Count).ConfigureAwait(false);
		return count;
	}

	/// <summary>
	/// Starts the scenario with an empty registry, no event counts and a named archive - the
	/// failure archive always, and the opt-in review archive when a folder was asked for.
	/// </summary>
	[BeforeScenario(Order = 0)]
	public void Begin_scenario()
	{
		FrameArchive.BeginScenario(_featureContext.FeatureInfo.Title, _scenarioContext.ScenarioInfo.Title);
		FrameReview.BeginScenario(_featureContext, _scenarioContext);
		ElementRegistry.Clear();
		EventRecorder.Clear();
		CanvasAssert.ResetLastReport();
	}

	/// <summary>
	/// Skips a scenario that belongs to the other orientation. Tags are the only way a scenario
	/// is excluded from one panel, and one process is one panel, so the assembly that does not
	/// own the tag reports the scenario as skipped rather than silently not having it.
	/// </summary>
	[BeforeScenario(Order = 100)]
	public void Skip_a_scenario_that_belongs_to_the_other_panel()
	{
		var foreignTag = TestTargetFixture.ForeignTag;
		var foreign = _scenarioContext.ScenarioInfo.CombinedTags
			.Any(tag => string.Equals(tag, foreignTag, StringComparison.OrdinalIgnoreCase));

		if (foreign)
		{
			_unitTestRuntimeProvider.TestIgnore(
				$"The scenario is tagged @{foreignTag}; this assembly runs {TestTargetFixture.Orientation}.");
		}
	}

	/// <summary>
	/// Skips a scenario whose system prerequisite this machine has not got. A coverage group
	/// records what it could not find from its own <c>[BeforeTestRun]</c>; a scenario says what
	/// it needs with a <c>@needs-&lt;name&gt;</c> tag. The point is the report: a machine without
	/// the engine a scenario is about says so, instead of failing with "the region is blank".
	/// Nothing is skipped on a machine that has everything, which is every machine that records
	/// no missing prerequisite.
	/// </summary>
	[BeforeScenario(Order = 101)]
	public void Skip_a_scenario_whose_prerequisite_is_missing()
	{
		var reason = Prerequisite.SkipReason(_scenarioContext.ScenarioInfo.CombinedTags);
		if (reason is not null)
		{
			_unitTestRuntimeProvider.TestIgnore(reason);
		}
	}

	/// <summary>
	/// Archives and attaches the last frame when the scenario failed, prints the report of the
	/// region that failed, and then resets the panel so the next scenario starts from the known
	/// background: every open popup is closed and the root is emptied.
	/// </summary>
	/// <returns>A task that completes once the panel is empty again.</returns>
	[AfterScenario(Order = 100)]
	public async Task End_scenario()
	{
		if (_scenarioContext.ScenarioExecutionStatus == ScenarioExecutionStatus.TestError)
		{
			ReportFailure();
		}

		// The reset is bounded as a whole, as well as through the fixture's own bound on every
		// wait inside it: a UI thread wedged by the scenario that has just ended must fail this
		// run rather than park it. A run that hangs here holds the machine - and whatever lock
		// the build was taken under - until somebody notices.
		await TestTargetFixture.BoundAsync(ResetThePanelAsync(), "the between-scenario reset")
			.ConfigureAwait(false);

		ElementRegistry.Clear();
		EventRecorder.Clear();
	}

	private static async Task ResetThePanelAsync()
	{
		await ReleaseAFingerLeftDownAsync().ConfigureAwait(false);

		if (TestTargetFixture.IsLaunched)
		{
			await CloseOpenPopupsAsync().ConfigureAwait(false);
			await TestTargetFixture.ClearContentAsync().ConfigureAwait(false);
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		}
	}

	private static IReadOnlyList<Popup> OpenPopups() =>
		VisualTreeHelper.GetOpenPopups(VirtualApplication.Instance.Window);

	private static FlyoutBase? OwningFlyout(Popup popup) => popup.PlacementTarget switch
	{
		SplitButton split => split.Flyout,
		Button button => button.Flyout,
		{ } target => FlyoutBase.GetAttachedFlyout(target),
		_ => null,
	};

	private void ReportFailure()
	{
		var path = FrameArchive.LastSavedPath;
		if (path is null && TestTargetFixture.IsLaunched)
		{
			path = FrameArchive.TrySave(TestTargetFixture.LatestFrame);
		}

		if (path is not null && File.Exists(path))
		{
			_outputHelper.WriteLine($"frame for \"{_scenarioContext.ScenarioInfo.Title}\" -> {path}");
			_outputHelper.AddAttachment(path);
			TestContext.Current.AddAttachment("frame.png", File.ReadAllBytes(path), "image/png");
			TestContext.Current.AddAttachment("frame.path", path);
		}

		var report = CanvasAssert.LastReport;
		if (report is not null)
		{
			_outputHelper.WriteLine($"canvas report:{Environment.NewLine}{report}");
		}

		_outputHelper.WriteLine(
			$"panel: {TestTargetFixture.DescribePanel()}; elements: [{string.Join(", ", ElementRegistry.Names)}]; "
			+ $"events: [{string.Join(", ", EventRecorder.Recorded)}]");
	}
}
