using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hosting;

/// <summary>
/// The bridge between the names a feature file uses and the elements a scenario built. A
/// scenario says "box" and the harness hands back the element that was registered under that
/// name; the registry is emptied between scenarios, which is half of what proves the reset.
/// </summary>
public static class ElementRegistry
{
	private static readonly Dictionary<string, FrameworkElement> Elements = new(StringComparer.Ordinal);

	/// <summary>The names registered by the current scenario, in registration order.</summary>
	public static IReadOnlyCollection<string> Names => Elements.Keys.ToArray();

	/// <summary>Whether the registry currently holds nothing.</summary>
	public static bool IsEmpty => Elements.Count == 0;

	/// <summary>Registers an element under the name a feature file refers to it by.</summary>
	/// <param name="name">The Gherkin name.</param>
	/// <param name="element">The element that name refers to.</param>
	public static void Register(string name, FrameworkElement element)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentNullException.ThrowIfNull(element);

		Elements[name] = element;
	}

	/// <summary>Forgets every registered name.</summary>
	public static void Clear() => Elements.Clear();

	/// <summary>Looks a name up, falling back to a walk of the live visual tree.</summary>
	/// <param name="name">The Gherkin name.</param>
	/// <param name="element">The element found, when one was.</param>
	/// <returns><c>true</c> when the name resolves.</returns>
	public static bool TryResolve(string name, out FrameworkElement element)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (Elements.TryGetValue(name, out var registered))
		{
			element = registered;
			return true;
		}

		// Elements inside a control template are never registered here, so the tree is the
		// second place to look.
		var found = VirtualApplication.Running?.FindByName(name);
		if (found is not null)
		{
			element = found;
			return true;
		}

		element = null!;
		return false;
	}

	/// <summary>Looks a name up and fails loudly when it does not resolve.</summary>
	/// <param name="name">The Gherkin name.</param>
	/// <returns>The element that name refers to.</returns>
	/// <exception cref="InvalidOperationException">No element of that name exists.</exception>
	public static FrameworkElement Resolve(string name)
	{
		if (TryResolve(name, out var element))
		{
			return element;
		}

		var known = Elements.Count == 0
			? "nothing is registered"
			: "registered: " + string.Join(", ", Elements.Keys);
		var inTree = VirtualApplication.Running is { } application
			? string.Join(", ", application.NamedElements())
			: string.Empty;

		throw new InvalidOperationException(
			$"No element named \"{name}\" is present. The scenario has {known}; "
			+ $"the visual tree holds [{inTree}].");
	}
}
