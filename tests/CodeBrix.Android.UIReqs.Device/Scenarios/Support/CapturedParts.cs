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
/// Where a moving template part was, and what the panel looked like, at the moment a frame was
/// taken. A knob or a thumb is at a different place in every frame, so a requirement that says
/// it moved has to remember the rectangle it had WHEN each frame was captured; reading the
/// rectangle again afterwards would only ever describe where the part is now.
/// </summary>
/// <param name="Name">The name the scenario gave this capture.</param>
/// <param name="Frame">The frame that was captured.</param>
/// <param name="Part">Where the moving part was, in device pixels.</param>
/// <param name="Container">Where the track the part moves along was, in device pixels.</param>
public sealed record CapturedPart(string Name, TestFrame Frame, DeviceRect Part, DeviceRect Container)
{
	/// <summary>The part's rectangle as a region of the frame it was captured in.</summary>
	/// <param name="description">What the region is, in the words a feature file used.</param>
	/// <returns>The region.</returns>
	public Region PartRegion(string description) => new(Frame, Part, description);

	/// <summary>The track's rectangle as a region of the frame it was captured in.</summary>
	/// <param name="description">What the region is, in the words a feature file used.</param>
	/// <returns>The region.</returns>
	public Region ContainerRegion(string description) => new(Frame, Container, description);
}

/// <summary>
/// The captures a scenario has taken of a moving part, kept in the scenario's own context so
/// nothing leaks from one scenario to the next.
/// </summary>
public static class CapturedParts
{
	private const string KeyPrefix = "uireqs.part.";

	/// <summary>
	/// Captures a frame, records where a moving part and its track were at that moment, and
	/// stores the frame under the same name so the shared frame steps can talk about it too.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="name">The name to store the capture under.</param>
	/// <param name="partName">The name of the moving template part.</param>
	/// <param name="containerName">The name of the track the part moves along.</param>
	/// <param name="partInset">
	/// How far inside the moving part its rectangle is measured. A part only a couple of pixels
	/// wide - a scroll bar's panning thumb, for one - has no rectangle left after the usual
	/// one-pixel inset, so those pass zero.
	/// </param>
	/// <returns>The capture.</returns>
	public static async Task<CapturedPart> CaptureAsync(ScenarioContext context, string name,
		string partName, string containerName, int partInset = DeviceRect.DefaultInset)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrEmpty(name);

		var part = await DeviceRect.OfAsync(ElementRegistry.Resolve(partName), partInset).ConfigureAwait(false);
		var container = await DeviceRect.OfAsync(ElementRegistry.Resolve(containerName)).ConfigureAwait(false);
		var frame = await ScenarioFrames.CaptureAsync(context, name).ConfigureAwait(false);

		var capture = new CapturedPart(name, frame, part, container);
		context[KeyPrefix + name] = capture;
		return capture;
	}

	/// <summary>The capture a scenario took under a name.</summary>
	/// <param name="context">The scenario.</param>
	/// <param name="name">The name the scenario gave the capture.</param>
	/// <returns>The capture.</returns>
	/// <exception cref="InvalidOperationException">The scenario never took a capture of that name.</exception>
	public static CapturedPart Get(ScenarioContext context, string name)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.TryGetValue(KeyPrefix + name, out var stored) && stored is CapturedPart capture)
		{
			return capture;
		}

		var known = Names(context);
		var taken = known.Count == 0 ? "no capture has been taken" : "captured: " + string.Join(", ", known);
		throw new InvalidOperationException(
			$"The scenario has no capture named \"{name}\" ({taken}).");
	}

	/// <summary>Every capture name the scenario has taken.</summary>
	/// <param name="context">The scenario.</param>
	/// <returns>The names.</returns>
	public static IReadOnlyCollection<string> Names(ScenarioContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Keys
			.Where(key => key.StartsWith(KeyPrefix, StringComparison.Ordinal))
			.Select(key => "\"" + key[KeyPrefix.Length..] + "\"")
			.ToArray();
	}
}
