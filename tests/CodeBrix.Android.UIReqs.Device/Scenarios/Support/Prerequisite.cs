using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// What a coverage group needs from the machine, and what happens when the machine has not got
/// it. A group's <c>[BeforeTestRun]</c> records a prerequisite it could not find by name, with
/// the message a person needs; every scenario tagged <c>@needs-&lt;name&gt;</c> is then SKIPPED
/// with that message instead of failing somewhere inside the engine that is not there.
/// <para>
/// Nothing has to be declared present: a name nobody recorded as missing is present, so a
/// machine that has everything skips nothing at all. The point of the mechanism is that a
/// machine which is missing a system library reports "skipped: libvlc.so.5 not found" rather
/// than "the region is blank".
/// </para>
/// </summary>
public static class Prerequisite
{
	/// <summary>The tag prefix that ties a scenario to a prerequisite name.</summary>
	public const string TagPrefix = "needs-";

	private static readonly object MissingLock = new();
	private static readonly Dictionary<string, string> MissingByName = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The prerequisite names that were recorded as missing, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> MissingNames
	{
		get
		{
			lock (MissingLock)
			{
				var names = new List<string>(MissingByName.Keys);
				names.Sort(StringComparer.OrdinalIgnoreCase);
				return names;
			}
		}
	}

	/// <summary>
	/// Records that a prerequisite is not on this machine. Called from a coverage group's
	/// <c>[BeforeTestRun]</c>, once, after that group has looked for it.
	/// </summary>
	/// <param name="name">
	/// The name a scenario writes after <c>@needs-</c>, such as <c>libvlc</c> for
	/// <c>@needs-libvlc</c>. Names are compared without regard to case.
	/// </param>
	/// <param name="message">Why it is missing, in the words a person needs to fix it.</param>
	public static void Missing(string name, string message)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentException.ThrowIfNullOrEmpty(message);

		lock (MissingLock)
		{
			MissingByName[name] = message;
		}
	}

	/// <summary>Whether a prerequisite was recorded as missing.</summary>
	/// <param name="name">The prerequisite name.</param>
	/// <param name="message">Why it is missing, when it is.</param>
	/// <returns><c>true</c> when the name was recorded as missing.</returns>
	public static bool IsMissing(string name, [MaybeNullWhen(false)] out string message)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		lock (MissingLock)
		{
			return MissingByName.TryGetValue(name, out message);
		}
	}

	/// <summary>
	/// The reason a scenario carrying these tags cannot run, or <c>null</c> when everything it
	/// asks for is present. A scenario that names several prerequisites reports the first
	/// missing one, in the order it wrote its tags.
	/// </summary>
	/// <param name="tags">The scenario's tags, without their leading '@'.</param>
	/// <returns>The skip message, or <c>null</c>.</returns>
	public static string? SkipReason(IEnumerable<string>? tags)
	{
		if (tags is null)
		{
			return null;
		}

		foreach (var tag in tags)
		{
			if (tag is null || !tag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var name = tag[TagPrefix.Length..];
			if (name.Length > 0 && IsMissing(name, out var message))
			{
				return $"The scenario is tagged @{tag}, and {name} is not on this machine: {message}";
			}
		}

		return null;
	}

	/// <summary>
	/// Forgets every recorded prerequisite. Only a scenario that is ABOUT this mechanism has any
	/// business calling it: the records belong to the whole run.
	/// </summary>
	public static void Clear()
	{
		lock (MissingLock)
		{
			MissingByName.Clear();
		}
	}

	/// <summary>A one-line description of what is missing, for a report header.</summary>
	/// <returns>What is missing, or that nothing is.</returns>
	public static string Describe()
	{
		var names = MissingNames;
		return names.Count == 0
			? "every prerequisite is present"
			: "missing prerequisites: " + string.Join(", ", names.Select(name => "@" + TagPrefix + name));
	}
}
