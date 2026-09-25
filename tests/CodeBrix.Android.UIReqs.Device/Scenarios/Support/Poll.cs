using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using Reqnroll;
using Xunit;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The bounded poll: a completion signal with a budget, for the one thing a frame handshake
/// cannot express - "it is not there yet, and there is no event that says when it will be".
/// <para>
/// This is NOT a sleep, and it is not a substitute for waiting on a signal: a step that has an
/// event, a property change or a returned status to wait on waits on that. A poll is for an
/// engine that reports nothing - a first composited frame, a decoder's first picture, an
/// indeterminate indicator that sweeps off the end of its own track - and it always states its
/// budget, so a run that never gets there fails with a number rather than hanging.
/// </para>
/// </summary>
public static class Poll
{
	/// <summary>How long a poll leaves the panel alone between two attempts, unless told otherwise.</summary>
	public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Calls a probe until it answers <c>true</c> or the budget is gone. The probe fetches
	/// whatever it looks at for itself - a probe that looks at pixels captures a fresh frame
	/// through the harness's own next-frame path first, which is what makes every attempt look
	/// at a NEW frame rather than at the one the step started with.
	/// </summary>
	/// <param name="probe">What to try; it returns <c>true</c> when the thing has happened.</param>
	/// <param name="budget">How long the thing has to happen in.</param>
	/// <param name="interval">How long to leave the panel alone between two attempts.</param>
	/// <returns><c>true</c> when the probe answered <c>true</c> inside the budget.</returns>
	public static async Task<bool> UntilAsync(Func<Task<bool>> probe, TimeSpan budget, TimeSpan? interval = null)
	{
		ArgumentNullException.ThrowIfNull(probe);

		var wait = interval ?? DefaultInterval;
		var elapsed = Stopwatch.StartNew();

		while (true)
		{
			if (await probe().ConfigureAwait(false))
			{
				return true;
			}

			if (elapsed.Elapsed >= budget)
			{
				return false;
			}

			await Task.Delay(wait, TestContext.Current.CancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Captures a fresh frame and looks at one element's region in it, until the region shows
	/// what the step is waiting for or the budget is gone. The frame is captured through
	/// <see cref="ScenarioFrames.CaptureAsync"/>, so the scenario's "the frame" is the last one
	/// the poll looked at and a later assertion talks about that same frame.
	/// </summary>
	/// <param name="context">The current scenario.</param>
	/// <param name="elementName">The Gherkin name of the element whose region is watched.</param>
	/// <param name="shows">What the region has to show.</param>
	/// <param name="budget">How long it has to show it.</param>
	/// <param name="interval">How long to leave the panel alone between two attempts.</param>
	/// <returns><c>true</c> when the region showed it inside the budget.</returns>
	public static Task<bool> UntilTheRegionShowsAsync(ScenarioContext context, string elementName,
		Func<Region, bool> shows, TimeSpan budget, TimeSpan? interval = null)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrEmpty(elementName);
		ArgumentNullException.ThrowIfNull(shows);

		return UntilAsync(
			async () =>
			{
				await ScenarioFrames.CaptureAsync(context, ScenarioFrames.CurrentFrameName).ConfigureAwait(false);
				var region = await ScenarioFrames.RegionAsync(context, elementName).ConfigureAwait(false);
				return shows(region);
			},
			budget,
			interval);
	}
}
