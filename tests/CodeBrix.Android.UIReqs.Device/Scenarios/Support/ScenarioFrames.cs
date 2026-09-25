using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using Reqnroll;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The frames a scenario has captured, kept in the scenario's own context so that nothing
/// leaks from one scenario to the next. A scenario that captures one frame talks about "the
/// frame"; a scenario that compares two gives them names.
/// </summary>
public static class ScenarioFrames
{
	/// <summary>The name the most recently captured frame is also stored under.</summary>
	public const string CurrentFrameName = "the frame";

	private const string KeyPrefix = "uireqs.frame.";

	/// <summary>Stores a frame under a name the scenario chose, and as the current frame.</summary>
	/// <param name="context">The scenario.</param>
	/// <param name="name">The name the scenario gave the frame.</param>
	/// <param name="frame">The frame.</param>
	public static void Store(ScenarioContext context, string name, TestFrame frame)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentNullException.ThrowIfNull(frame);

		context[KeyPrefix + name] = frame;
		context[KeyPrefix + CurrentFrameName] = frame;
	}

	/// <summary>The frame a scenario captured under a name.</summary>
	/// <param name="context">The scenario.</param>
	/// <param name="name">The name the scenario gave the frame.</param>
	/// <returns>The frame.</returns>
	/// <exception cref="InvalidOperationException">The scenario never captured a frame of that name.</exception>
	public static TestFrame Get(ScenarioContext context, string name)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.TryGetValue(KeyPrefix + name, out var stored) && stored is TestFrame frame)
		{
			return frame;
		}

		var captured = context.Keys
			.Where(key => key.StartsWith(KeyPrefix, StringComparison.Ordinal))
			.Select(key => "\"" + key[KeyPrefix.Length..] + "\"")
			.ToArray();
		var known = captured.Length == 0 ? "no frame has been captured" : "captured: " + string.Join(", ", captured);

		throw new InvalidOperationException(
			$"The scenario has no frame named \"{name}\" ({known}). "
			+ "A scenario captures a frame with \"When the frame is captured\" or "
			+ "\"When the frame is captured as ...\".");
	}

	/// <summary>The most recently captured frame.</summary>
	/// <param name="context">The scenario.</param>
	/// <returns>The frame.</returns>
	public static TestFrame Current(ScenarioContext context) => Get(context, CurrentFrameName);

	/// <summary>
	/// Captures a frame through the fixture's handshake and stores it under a name.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="name">The name to store it under.</param>
	/// <returns>The frame.</returns>
	public static async Task<TestFrame> CaptureAsync(ScenarioContext context, string name)
	{
		// The name goes along as the review archive's label, EXCEPT for the unnamed "current"
		// frame: a scenario that never named its frames is better read by capture order alone.
		var label = string.Equals(name, CurrentFrameName, StringComparison.Ordinal) ? null : name;
		var frame = await TestTargetFixture.NextFrameAsync(label: label).ConfigureAwait(false);
		Store(context, name, frame);
		return frame;
	}

	/// <summary>The region one named element covers in one captured frame.</summary>
	/// <param name="context">The scenario.</param>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="frameName">The frame to cut the region from; the default is the current one.</param>
	/// <returns>The region.</returns>
	public static async Task<Region> RegionAsync(ScenarioContext context, string elementName,
		string frameName = CurrentFrameName)
	{
		var element = ElementRegistry.Resolve(elementName);
		var bounds = await DeviceRect.OfAsync(element).ConfigureAwait(false);
		var frame = Get(context, frameName);
		var description = frameName == CurrentFrameName
			? $"the region of \"{elementName}\""
			: $"the region of \"{elementName}\" in frame \"{frameName}\"";
		return new Region(frame, bounds, description);
	}

	/// <summary>
	/// A rectangle CUT OUT of one named element's region, addressed in that element's own
	/// coordinates. The element's rectangle is taken with NO inset, so the coordinates a scenario
	/// writes are measured from the element's real top left corner rather than from a shrunken
	/// one; <paramref name="inset"/> then pulls the block's own edges in, which is what a shape
	/// that was stroked as well as filled needs before anything is claimed about its fill.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="elementName">The Gherkin name of the element the block is inside.</param>
	/// <param name="insideElement">The block, in the element's own coordinates.</param>
	/// <param name="description">What the block is, in the words a feature file used.</param>
	/// <param name="inset">How far to pull the block's own edges in; the default is none.</param>
	/// <param name="frameName">The frame to cut the region from; the default is the current one.</param>
	/// <returns>The region.</returns>
	public static async Task<Region> SubRegionAsync(ScenarioContext context, string elementName,
		DeviceRect insideElement, string description, int inset = 0, string frameName = CurrentFrameName)
	{
		var element = ElementRegistry.Resolve(elementName);
		var bounds = await DeviceRect.OfAsync(element, inset: 0).ConfigureAwait(false);
		var block = new DeviceRect(
			bounds.X + insideElement.X,
			bounds.Y + insideElement.Y,
			insideElement.Width,
			insideElement.Height).Inset(inset);

		if (block.IsEmpty)
		{
			throw new InvalidOperationException(
				$"{description} came out as {block}, which has nothing in it to look at.");
		}

		return new Region(Get(context, frameName), block, description);
	}

	/// <summary>Every frame name the scenario has captured.</summary>
	/// <param name="context">The scenario.</param>
	/// <returns>The names.</returns>
	public static IReadOnlyCollection<string> Names(ScenarioContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Keys
			.Where(key => key.StartsWith(KeyPrefix, StringComparison.Ordinal))
			.Select(key => key[KeyPrefix.Length..])
			.ToArray();
	}
}
