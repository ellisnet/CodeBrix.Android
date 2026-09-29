using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Xunit;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hosting;

/// <summary>
/// The one virtual application a scenario run owns: it is set up and launched once, before the
/// first scenario, and shut down after the last one. Everything a step needs to reach the
/// running application - the session, the panel size, the UI thread and the frame handshake -
/// goes through here.
/// </summary>
public static class TestTargetFixture
{
	/// <summary>How long the application is given to launch and publish its first frame.</summary>
	public static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(30);

	/// <summary>
	/// How long a requested frame is waited for. Two seconds is deliberately short: a hang
	/// becomes a clear failure instead of a silently stale frame.
	/// </summary>
	public static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

	/// <summary>How long the application is given to lay itself out on the panel after launch.</summary>
	public static readonly TimeSpan LayoutTimeout = TimeSpan.FromSeconds(10);

	/// <summary>How long the application is given to shut down.</summary>
	public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

	/// <summary>
	/// How long any wait on the UI thread is given before the run is failed. Sixty seconds is
	/// deliberately generous: nothing this harness asks the UI thread to do takes anything like
	/// that long, so a wait that reaches it is a WEDGED UI thread and not a slow one.
	/// <para>
	/// It has to be bounded at all because a UI thread can be lost for the rest of the process:
	/// an exception thrown out of a layout pass - a missing assembly a template's code first
	/// touches, say - can leave the dispatcher unable to finish that pass, and every later
	/// <see cref="RunOnUIThreadAsync(Action)"/>, <see cref="WaitForIdleAsync"/> and frame
	/// handshake then waits on a thread that will never answer. Unbounded, that is a run which
	/// hangs for as long as the machine is left alone, holding whatever lock the build was taken
	/// under; bounded, it is a scenario that fails with a sentence saying which wait it was.
	/// </para>
	/// <para>
	/// Set it to <see cref="TimeSpan.Zero"/> (or anything not positive) to take the bound off
	/// again, which is what a session under a debugger wants.
	/// </para>
	/// </summary>
	public static TimeSpan UIThreadTimeout { get; set; } = TimeSpan.FromSeconds(60);

	private static readonly TimeSpan LayoutPollInterval = TimeSpan.FromMilliseconds(25);

	private static TestTargetSession? _session;
	private static TestDisplayOrientation? _orientation;

	/// <summary>
	/// The assembly whose scenarios are running: the process's entry assembly, which under the
	/// test platform is the test executable itself.
	/// <para>
	/// This is deliberately NOT the assembly this harness is compiled into. An add-in coverage
	/// group is its own executable that REFERENCES the core harness and takes its steps and
	/// hooks from it: the panel it declares and the feature folder it carries are on that
	/// executable, not on the harness. Falling back to the harness's own assembly keeps a
	/// process with no entry assembly at all - a host that loads the tests rather than running
	/// them - working exactly as it did.
	/// </para>
	/// </summary>
	public static Assembly TestAssembly =>
		Assembly.GetEntryAssembly() ?? typeof(TestTargetFixture).Assembly;

	/// <summary>The session that owns the panel.</summary>
	/// <exception cref="InvalidOperationException">The application has not launched yet.</exception>
	public static TestTargetSession Session =>
		_session ?? throw new InvalidOperationException(
			"The test target has not been launched. TestRunHooks launches it before the first scenario.");

	/// <summary>Whether the application is up.</summary>
	public static bool IsLaunched => _session is { IsLaunched: true };

	/// <summary>The orientation this assembly declared through its <see cref="TestPanelAttribute"/>.</summary>
	/// <exception cref="InvalidOperationException">The attribute has not been read yet.</exception>
	public static TestDisplayOrientation Orientation =>
		_orientation ?? throw new InvalidOperationException(
			"The panel orientation has not been read yet. TestRunHooks reads it before the first scenario.");

	/// <summary>The panel width in device pixels. Display scale is 1.0, so this is also logical pixels.</summary>
	public static int PanelWidth => Session.Width;

	/// <summary>The panel height in device pixels.</summary>
	public static int PanelHeight => Session.Height;

	/// <summary>
	/// The panel size in device pixels. Scenarios whose expectations depend on the panel derive
	/// their numbers from this, never from constants in a feature file.
	/// </summary>
	public static (int Width, int Height) PanelSize => (Session.Width, Session.Height);

	/// <summary>
	/// The tag that marks a scenario as belonging only to the OTHER orientation, and therefore
	/// as one this assembly skips.
	/// </summary>
	public static string ForeignTag =>
		Orientation == TestDisplayOrientation.Landscape ? "portrait-only" : "landscape-only";

	/// <summary>The tag that marks a scenario as belonging only to this assembly's orientation.</summary>
	public static string OwnTag =>
		Orientation == TestDisplayOrientation.Landscape ? "landscape-only" : "portrait-only";

	/// <summary>
	/// The most recent frame the fixture handed out. The scenario hooks save this one when a
	/// scenario fails.
	/// </summary>
	public static TestFrame? LatestFrame { get; private set; }

	/// <summary>
	/// Reads the orientation off the test assembly, sets the panel up, launches the virtual
	/// application and settles the first frame. Called once, from the test-run hook.
	/// </summary>
	/// <returns>A task that completes when the application is showing its laid-out first frame.</returns>
	public static async Task LaunchAsync()
	{
		if (_session is not null)
		{
			throw new InvalidOperationException("The test target has already been launched in this process.");
		}

		// ANDROID PORT: the runner script launched the scenario app (its activity started the
		// virtual application) and the host runner declared the orientation it rotated the
		// emulator to; the session wraps the running app instead of setting a panel up.
		_orientation = global::CodeBrix.Android.UIReqs.Device.Runtime.AppHost.RequestedOrientation;
		var session = global::CodeBrix.Android.UIReqs.Device.Runtime.AppHost.Session;
		_session = session;

		// The first published frame is the host's own forced repaint and can beat the
		// application's first layout pass, so it is legitimately empty. Wait for the tree to be
		// laid out on the panel, then do the handshake once: from here on every frame the
		// fixture hands out shows a laid-out application.
		await WaitForLayoutAsync().ConfigureAwait(false);
		LatestFrame = await NextFrameAsync().ConfigureAwait(false);
	}

	/// <summary>Shuts the virtual application down. Called once, from the test-run hook.</summary>
	/// <returns>A task that completes when the host thread has ended.</returns>
	public static Task ShutdownAsync()
	{
		// ANDROID PORT: the app is stopped by the runner script; the session is only forgotten.
		_session = null;
		LatestFrame = null;
		return Task.CompletedTask;
	}

	/// <summary>Reads the assembly-level <see cref="TestPanelAttribute"/>.</summary>
	/// <param name="testAssembly">The assembly the scenarios were generated into.</param>
	/// <returns>The orientation the assembly declared.</returns>
	/// <exception cref="InvalidOperationException">The assembly carries no such attribute.</exception>
	public static TestDisplayOrientation ReadOrientation(Assembly testAssembly)
	{
		ArgumentNullException.ThrowIfNull(testAssembly);

		var attribute = testAssembly.GetCustomAttribute<TestPanelAttribute>()
			?? throw new InvalidOperationException(
				$"The assembly the scenarios are running from, '{testAssembly.GetName().Name}', carries no "
				+ "[assembly: TestPanel(...)] attribute, so there is no way to know which panel its scenarios "
				+ "run against. Every UIReqs project declares one in its own PanelOrientation.cs.");

		return attribute.Orientation;
	}

	/// <summary>Runs an action on the UI thread and waits for it, with a bound.</summary>
	/// <param name="action">The work to run.</param>
	/// <returns>A task that completes once the action has run.</returns>
	/// <exception cref="InvalidOperationException">The UI thread did not answer in time.</exception>
	public static Task RunOnUIThreadAsync(Action action) =>
		BoundAsync(Session.RunOnUIThreadAsync(action), "an action on the UI thread");

	/// <summary>Runs asynchronous work on the UI thread and waits for it, with a bound.</summary>
	/// <param name="action">The work to run.</param>
	/// <returns>A task that completes once the work has run.</returns>
	/// <exception cref="InvalidOperationException">The UI thread did not answer in time.</exception>
	public static Task RunOnUIThreadAsync(Func<Task> action) =>
		BoundAsync(Session.RunOnUIThreadAsync(action), "asynchronous work on the UI thread");

	/// <summary>
	/// Gives a wait on the UI thread a bound, so that a wedged UI thread fails the run instead
	/// of hanging it. Every wait this fixture makes goes through here; a hook or a steps class
	/// with a wait of its own - a teardown that resets the panel, say - wraps it the same way.
	/// </summary>
	/// <param name="work">The wait to bound. It has already started.</param>
	/// <param name="waitName">What the wait is, named as the failure message will name it.</param>
	/// <returns>The same wait, failed after <see cref="UIThreadTimeout"/> if it has not finished.</returns>
	/// <exception cref="InvalidOperationException">The wait did not finish inside the budget.</exception>
	public static async Task BoundAsync(Task work, string waitName)
	{
		ArgumentNullException.ThrowIfNull(work);
		ArgumentException.ThrowIfNullOrEmpty(waitName);

		var budget = UIThreadTimeout;
		if (budget <= TimeSpan.Zero)
		{
			await work.ConfigureAwait(false);
			return;
		}

		try
		{
			await work.WaitAsync(budget).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// The abandoned wait may still fault later, and a faulted task nobody looks at is
			// reported by the runtime as an unobserved exception; this looks at it.
			_ = work.ContinueWith(
				static abandoned => _ = abandoned.Exception,
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);

			// Formatted in two pieces on purpose: a CONCATENATED interpolated string binds
			// string.Create to its ref-handler overload and does not compile (CS1620).
			var wait = string.Create(CultureInfo.InvariantCulture,
				$"The UI thread did not answer within {budget.TotalSeconds} seconds, waiting for {waitName}.");

			throw new InvalidOperationException(wait
				+ " The panel's UI thread is wedged rather than slow - an exception thrown out of a layout"
				+ " pass leaves it unable to finish that pass, and every wait after it waits forever - so the"
				+ " run is failed here instead of hanging. TestTargetFixture.UIThreadTimeout sets this budget.");
		}
	}

	/// <summary>
	/// Waits for the UI thread to go idle: everything already queued has run by the time this
	/// returns. Implemented as one low-priority no-op queued behind the existing work, which is
	/// what makes an input event's effect complete before a frame is asked for.
	/// </summary>
	/// <returns>A task that completes when the dispatcher has drained.</returns>
	/// <exception cref="InvalidOperationException">The UI thread did not drain in time.</exception>
	public static async Task WaitForIdleAsync()
	{
		var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		await RunOnUIThreadAsync(() =>
		{
			var queue = VirtualApplication.Running?.Root.DispatcherQueue;
			if (queue is null || !queue.TryEnqueue(DispatcherQueuePriority.Low, () => idle.TrySetResult()))
			{
				idle.TrySetResult();
			}
		}).ConfigureAwait(false);

		// The no-op is queued BEHIND everything the UI thread already had to do, so this is the
		// wait that a wedged pass never ends: it is bounded for the same reason the others are.
		await BoundAsync(idle.Task, "the UI thread to go idle").ConfigureAwait(false);
	}

	/// <summary>Waits for a newly rendered frame without recording a scenario capture.</summary>
	/// <returns>A task that completes when queued UI changes have reached a rendered frame.</returns>
	public static Task WaitForRenderAsync() => Session.WaitForRenderAsync(FrameTimeout);

	/// <summary>
	/// The frame handshake: lay the tree out on the UI thread, let the dispatcher drain, then
	/// ask for ONE frame. The session only completes such a request with a frame whose
	/// rendering began after the request, so a render that was already in flight cannot answer
	/// it - there is no polling and no "after N frames" anywhere in this harness.
	/// </summary>
	/// <param name="timeout">How long to wait; the default is <see cref="FrameTimeout"/>.</param>
	/// <param name="label">
	/// The name the step is capturing this frame under, or <c>null</c> for the scenario's
	/// unnamed "current" frame. It is only used to name the file when the opt-in review archive
	/// is on; see <see cref="FrameReview"/>.
	/// </param>
	/// <returns>The frame that shows everything applied before the call.</returns>
	/// <exception cref="InvalidOperationException">The UI thread did not lay out or drain in time.</exception>
	public static async Task<TestFrame> NextFrameAsync(TimeSpan? timeout = null, string? label = null)
	{
		// Both halves of the drain are bounded (see UIThreadTimeout): the layout pass through
		// RunOnUIThreadAsync and the queue through WaitForIdleAsync. The frame request itself
		// has always had FrameTimeout, so from here on nothing in the handshake can hang.
		await RunOnUIThreadAsync(() => VirtualApplication.Running?.Root.UpdateLayout()).ConfigureAwait(false);
		await WaitForIdleAsync().ConfigureAwait(false);

		var frame = await Session.RequestFrameAsync(timeout ?? FrameTimeout).ConfigureAwait(false);
		LatestFrame = frame;

		// ANDROID PORT: the layout-replay check - every named element's native view must sit at
		// its Core rectangle (within 1 pixel) in the frame just captured.
		await global::CodeBrix.Android.UIReqs.Device.Runtime.GeometryAudit.VerifyAsync().ConfigureAwait(false);

		// Every frame the harness hands out passes through here, which is what makes "every
		// frame a scenario evaluated is on disk" a fact rather than a promise. Saving is off
		// unless the reviewer asked for it, and it never throws.
		FrameReview.Save(frame, label);
		return frame;
	}

	/// <summary>Shows one element, replacing whatever the root was holding.</summary>
	/// <param name="content">The element to show.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	public static Task SetContentAsync(UIElement content) => VirtualApplication.Instance.SetContentAsync(content);

	/// <summary>Empties the root panel.</summary>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	public static Task ClearContentAsync() => VirtualApplication.Instance.ClearContentAsync();

	/// <summary>Finds an element by name anywhere below the root.</summary>
	/// <param name="name">The element name.</param>
	/// <returns>The element, or <c>null</c> when the tree holds no element of that name.</returns>
	public static FrameworkElement? FindByName(string name) => VirtualApplication.Instance.FindByName(name);

	private static async Task WaitForLayoutAsync()
	{
		var stopwatch = Stopwatch.StartNew();
		var width = Session.Width;
		var height = Session.Height;

		while (stopwatch.Elapsed < LayoutTimeout)
		{
			var laidOut = false;
			await RunOnUIThreadAsync(() =>
			{
				var root = VirtualApplication.Running?.Root;
				// ANDROID PORT: the panel is in device pixels, Core lays out in DIPs (density per XamlRoot).
				var density = root?.XamlRoot?.RasterizationScale ?? 1;
				laidOut = root is not null
					&& Math.Abs((root.ActualWidth * density) - width) < 1
					&& Math.Abs((root.ActualHeight * density) - height) < 1;
			}).ConfigureAwait(false);

			if (laidOut)
			{
				return;
			}

			await Task.Delay(LayoutPollInterval, TestContext.Current.CancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException(FormattableString.Invariant(
			$"The virtual application did not lay out on a {width} x {height} panel within {LayoutTimeout}."));
	}

	/// <summary>A one-line description of the panel, for failure messages.</summary>
	/// <returns>The orientation and the panel size.</returns>
	public static string DescribePanel() => string.Create(CultureInfo.InvariantCulture,
		$"{Orientation} panel, {PanelWidth} x {PanelHeight} device pixels");
}
