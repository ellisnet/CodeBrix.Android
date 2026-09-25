using System;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Reqnroll;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hooks;

/// <summary>
/// The once-per-process hooks. One virtual application serves every scenario of an assembly:
/// the window wrapper, the pointer source and the dispatcher overrides are process-wide, so
/// the application is launched here and only its content changes between scenarios.
/// </summary>
[Binding]
public sealed class TestRunHooks
{
	/// <summary>
	/// Reads the panel this assembly declared, launches the virtual application and settles its
	/// first frame, so no scenario is ever handed the empty frame the host publishes before the
	/// application has laid itself out. The application's own font is warmed straight away, so
	/// that no scenario is the one that pays for the first text layout. The opt-in review
	/// archive is set up afterwards, because it files its frames by panel orientation and the
	/// panel is only known once the application is up - which also keeps the launch handshake's
	/// own frame out of it.
	/// </summary>
	/// <returns>A task that completes when the application is showing.</returns>
	[BeforeTestRun(Order = 0)]
	public static async Task Launch_the_virtual_application()
	{
		await TestTargetFixture.LaunchAsync().ConfigureAwait(false);
		await FontWarmup.WarmAsync(VirtualApplication.TextFontFamily).ConfigureAwait(false);
		FrameReview.Initialize();
	}

	/// <summary>
	/// Says which assembly is running, on which panel, where its frames go and what this machine
	/// has not got - the first line of a run, and the one a person reads when a run reports
	/// something odd. It runs LAST of the test-run hooks, after every coverage group has
	/// registered its vocabulary and looked for its prerequisites, so what it says about them is
	/// what the scenarios will actually see.
	/// </summary>
	[BeforeTestRun(Order = 1000)]
	public static void Announce_the_run() => AnnounceThePanel();

	private static void AnnounceThePanel() =>
		Console.Out.WriteLine(
			$"UIReqs: {TestTargetFixture.TestAssembly.GetName().Name}; {TestTargetFixture.DescribePanel()}; "
			+ $"{FrameArchive.Describe()}; {FrameReview.Describe()}; {Prerequisite.Describe()}.");

	/// <summary>Shuts the virtual application down after the last scenario.</summary>
	/// <returns>A task that completes when the host thread has ended.</returns>
	[AfterTestRun(Order = 0)]
	public static async Task Shut_the_virtual_application_down() =>
		await TestTargetFixture.ShutdownAsync().ConfigureAwait(false);
}
