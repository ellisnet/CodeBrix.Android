using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// Counts the events the elements a scenario built have raised. A requirement like "tapping
/// the button raises Click" is a statement about the tree, not about pixels, so it is checked
/// here rather than on the canvas.
/// </summary>
public static class EventRecorder
{
	private static readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);

	/// <summary>Every event that has been raised this scenario, as "element.Event = count".</summary>
	public static IReadOnlyCollection<string> Recorded =>
		Counts.Select(pair => $"{pair.Key} = {pair.Value}").ToArray();

	/// <summary>Records one raising of an event.</summary>
	/// <param name="elementName">The Gherkin name of the element that raised it.</param>
	/// <param name="eventName">The event's name.</param>
	public static void Record(string elementName, string eventName)
	{
		ArgumentException.ThrowIfNullOrEmpty(elementName);
		ArgumentException.ThrowIfNullOrEmpty(eventName);

		var key = Key(elementName, eventName);
		Counts.TryGetValue(key, out var count);
		Counts[key] = count + 1;
	}

	/// <summary>How often an element has raised an event this scenario.</summary>
	/// <param name="elementName">The Gherkin name of the element.</param>
	/// <param name="eventName">The event's name.</param>
	/// <returns>The count, which is zero when the event has never been raised.</returns>
	public static int Count(string elementName, string eventName)
	{
		Counts.TryGetValue(Key(elementName, eventName), out var count);
		return count;
	}

	/// <summary>Forgets every count. The scenario hooks call this per scenario.</summary>
	public static void Clear() => Counts.Clear();

	private static string Key(string elementName, string eventName) => elementName + "." + eventName;
}
