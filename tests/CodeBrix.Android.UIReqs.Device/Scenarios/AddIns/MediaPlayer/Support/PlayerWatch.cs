using System;
using System.Collections.Generic;
using System.Linq;
using MediaPlayerFailedEventArgs = Windows.Media.Playback.MediaPlayerFailedEventArgs;

namespace CodeBrix.Platform.UI.AddIn.MediaPlayer.UIReqs.Support;

/// <summary>
/// What each player has reported about its media so far, kept per Gherkin name for the whole
/// scenario.
/// <para>
/// The harness's own event recorder already counts these events, and the scenarios use its
/// sentences to state them; this keeps the same facts a second time for the steps that WAIT on
/// one. A wait has to survive "the recorded events are forgotten", which a scenario is entitled
/// to say in the middle of its arrange run, and it needs the failure ARGUMENTS rather than a
/// count - what a person could read on the screen is the message the engine sent, not the fact
/// that something went wrong.
/// </para>
/// </summary>
public static class PlayerWatch
{
	private static readonly object Gate = new();
	private static readonly Dictionary<string, Report> Reports = new(StringComparer.Ordinal);

	/// <summary>The players this scenario has heard from, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Watched
	{
		get
		{
			lock (Gate)
			{
				var names = new List<string>(Reports.Keys);
				names.Sort(StringComparer.Ordinal);
				return names;
			}
		}
	}

	/// <summary>Forgets everything every player has reported. The scenario hooks call this.</summary>
	public static void Clear()
	{
		lock (Gate)
		{
			Reports.Clear();
		}
	}

	/// <summary>Records that a player opened its media.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	public static void Opened(string name)
	{
		lock (Gate)
		{
			Of(name).OpenedCount++;
		}
	}

	/// <summary>Records that a player reached the end of its media.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	public static void Ended(string name)
	{
		lock (Gate)
		{
			Of(name).EndedCount++;
		}
	}

	/// <summary>Records that a player could not play its media, and what it said about it.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	/// <param name="failure">What the player reported.</param>
	public static void Failed(string name, MediaPlayerFailedEventArgs failure)
	{
		lock (Gate)
		{
			var report = Of(name);
			report.FailedCount++;
			report.LastFailure = failure;
		}
	}

	/// <summary>How often a player has opened a media this scenario.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	/// <returns>The count.</returns>
	public static int OpenedCount(string name) => Read(name, report => report.OpenedCount);

	/// <summary>How often a player has reached the end of its media this scenario.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	/// <returns>The count.</returns>
	public static int EndedCount(string name) => Read(name, report => report.EndedCount);

	/// <summary>How often a player has failed this scenario.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	/// <returns>The count.</returns>
	public static int FailedCount(string name) => Read(name, report => report.FailedCount);

	/// <summary>What a player said the last time it failed, or <c>null</c> when it never has.</summary>
	/// <param name="name">The Gherkin name of the element the player belongs to.</param>
	/// <returns>The failure, or <c>null</c>.</returns>
	public static MediaPlayerFailedEventArgs? LastFailure(string name) =>
		Read(name, report => report.LastFailure);

	/// <summary>A one-line description of what every player has reported, for a failure message.</summary>
	/// <returns>The description.</returns>
	public static string Describe()
	{
		lock (Gate)
		{
			return Reports.Count == 0
				? "no player has reported anything"
				: string.Join(", ", Reports
					.OrderBy(pair => pair.Key, StringComparer.Ordinal)
					.Select(pair =>
						$"\"{pair.Key}\" opened {pair.Value.OpenedCount}, ended {pair.Value.EndedCount}, "
						+ $"failed {pair.Value.FailedCount}"));
		}
	}

	private static T Read<T>(string name, Func<Report, T> read)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		lock (Gate)
		{
			return Reports.TryGetValue(name, out var report) ? read(report) : read(new Report());
		}
	}

	// Called under the gate.
	private static Report Of(string name)
	{
		if (!Reports.TryGetValue(name, out var report))
		{
			report = new Report();
			Reports[name] = report;
		}

		return report;
	}

	private sealed class Report
	{
		public int OpenedCount { get; set; }

		public int EndedCount { get; set; }

		public int FailedCount { get; set; }

		public MediaPlayerFailedEventArgs? LastFailure { get; set; }
	}
}
