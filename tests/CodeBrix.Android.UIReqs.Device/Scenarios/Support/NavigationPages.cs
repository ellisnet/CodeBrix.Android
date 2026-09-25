using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// What a page a scenario navigates to is made of: one panel of a known colour, so that "the
/// Frame is showing the second page" is something a person can see on the panel rather than
/// only a type name in the tree.
/// </summary>
/// <param name="Key">The word a feature file uses for the page - "first", "second", "third".</param>
/// <param name="PanelName">The name the scenario refers to the page's panel by.</param>
/// <param name="Color">The colour the panel is painted.</param>
public sealed record PageDescription(string Key, string PanelName, Color Color);

/// <summary>
/// The pages a Frame navigates between. A Frame navigates to a TYPE and builds the page
/// itself, so the pages have to exist as types before the scenario runs; what each one shows
/// is described by the scenario and looked up by the page as it is built.
/// <para>
/// Three slots are enough for every requirement about a back stack that is worth stating, and
/// three named types keep the whole thing built in C# with no XAML anywhere.
/// </para>
/// </summary>
public static class NavigationPages
{
	private static readonly Dictionary<string, PageDescription> Descriptions = new(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, Type> Slots = new(StringComparer.OrdinalIgnoreCase)
	{
		["first"] = typeof(FirstTestPage),
		["second"] = typeof(SecondTestPage),
		["third"] = typeof(ThirdTestPage),
	};

	/// <summary>The page words a feature file may use.</summary>
	public static IReadOnlyCollection<string> Keys => Slots.Keys.ToArray();

	/// <summary>Says what a page shows. The scenario does this before it navigates.</summary>
	/// <param name="key">The page word - "first", "second" or "third".</param>
	/// <param name="panelName">The name the scenario refers to the page's panel by.</param>
	/// <param name="color">The colour the panel is painted.</param>
	/// <exception cref="NotSupportedException">There is no page slot of that name.</exception>
	public static void Describe(string key, string panelName, Color color)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);
		ArgumentException.ThrowIfNullOrEmpty(panelName);

		if (!Slots.ContainsKey(key))
		{
			throw new NotSupportedException(
				$"There is no page called \"{key}\". The harness has: {string.Join(", ", Keys)}.");
		}

		Descriptions[key] = new PageDescription(key, panelName, color);
	}

	/// <summary>Forgets every page description. The steps class does this per scenario.</summary>
	public static void Clear() => Descriptions.Clear();

	/// <summary>The type a Frame navigates to for a page word.</summary>
	/// <param name="key">The page word.</param>
	/// <returns>The page type.</returns>
	/// <exception cref="NotSupportedException">The scenario never said what that page shows.</exception>
	public static Type TypeOf(string key)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);

		if (!Slots.TryGetValue(key, out var type))
		{
			throw new NotSupportedException(
				$"There is no page called \"{key}\". The harness has: {string.Join(", ", Keys)}.");
		}

		if (!Descriptions.ContainsKey(key))
		{
			throw new InvalidOperationException(
				$"The scenario has not said what the page \"{key}\" shows. A scenario describes a page "
				+ "with \"the page \"<key>\" shows a panel named \"<name>\" painted \"<colour>\"\".");
		}

		return type;
	}

	/// <summary>The page word a page type stands for.</summary>
	/// <param name="type">The page type a Frame reported.</param>
	/// <returns>The page word, or the type's name when it is not one of the slots.</returns>
	public static string KeyOf(Type? type)
	{
		if (type is null)
		{
			return "nothing";
		}

		foreach (var slot in Slots)
		{
			if (slot.Value == type)
			{
				return slot.Key;
			}
		}

		return type.Name;
	}

	/// <summary>Builds a page's content from what the scenario said it shows.</summary>
	/// <param name="page">The page being built.</param>
	/// <param name="key">The page word it stands for.</param>
	public static void Build(Page page, string key)
	{
		ArgumentNullException.ThrowIfNull(page);

		if (!Descriptions.TryGetValue(key, out var description))
		{
			throw new InvalidOperationException(
				$"The page \"{key}\" was navigated to before the scenario said what it shows.");
		}

		var panel = new Border
		{
			Name = description.PanelName,
			Background = new SolidColorBrush(description.Color),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		ElementRegistry.Register(description.PanelName, panel);
		page.Content = panel;
	}
}

/// <summary>The page a scenario calls "first".</summary>
public sealed partial class FirstTestPage : Page
{
	/// <summary>Builds the page from what the scenario said it shows.</summary>
	public FirstTestPage() => NavigationPages.Build(this, "first");
}

/// <summary>The page a scenario calls "second".</summary>
public sealed partial class SecondTestPage : Page
{
	/// <summary>Builds the page from what the scenario said it shows.</summary>
	public SecondTestPage() => NavigationPages.Build(this, "second");
}

/// <summary>The page a scenario calls "third".</summary>
public sealed partial class ThirdTestPage : Page
{
	/// <summary>Builds the page from what the scenario said it shows.</summary>
	public ThirdTestPage() => NavigationPages.Build(this, "third");
}
