using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// Finding the parts of a control that a requirement talks about but no scenario ever built:
/// the scroll viewer inside a ListView, the header items of a Pivot, the chevron of a
/// TreeViewItem. Those live inside a control template, so the only way to reach one is to walk
/// the live visual tree from the control itself.
/// <para>
/// Every method here must be called on the UI thread, because that is the only thread that may
/// read the visual tree.
/// </para>
/// </summary>
public static class VisualTreeSearch
{
	/// <summary>Every descendant of an element, in visual-tree order, depth first.</summary>
	/// <param name="parent">The element to walk below.</param>
	/// <returns>The descendants.</returns>
	public static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
	{
		ArgumentNullException.ThrowIfNull(parent);

		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			yield return child;

			foreach (var descendant in Descendants(child))
			{
				yield return descendant;
			}
		}
	}

	/// <summary>The first descendant of a kind, or <c>null</c> when the tree holds none.</summary>
	/// <typeparam name="T">The kind of element to look for.</typeparam>
	/// <param name="parent">The element to walk below.</param>
	/// <returns>The descendant, or <c>null</c>.</returns>
	public static T? FindDescendant<T>(DependencyObject parent)
		where T : class
	{
		foreach (var child in Descendants(parent))
		{
			if (child is T found)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>The first descendant carrying a name, or <c>null</c> when the tree holds none.</summary>
	/// <param name="parent">The element to walk below.</param>
	/// <param name="name">The name to look for.</param>
	/// <returns>The descendant, or <c>null</c>.</returns>
	public static FrameworkElement? FindDescendantNamed(DependencyObject parent, string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		foreach (var child in Descendants(parent))
		{
			if (child is FrameworkElement element
				&& string.Equals(element.Name, name, StringComparison.Ordinal))
			{
				return element;
			}
		}

		return null;
	}

	/// <summary>Every descendant of a kind, in visual-tree order.</summary>
	/// <typeparam name="T">The kind of element to look for.</typeparam>
	/// <param name="parent">The element to walk below.</param>
	/// <returns>The descendants of that kind.</returns>
	public static IReadOnlyList<T> FindDescendants<T>(DependencyObject parent)
		where T : class
	{
		var found = new List<T>();
		foreach (var child in Descendants(parent))
		{
			if (child is T match)
			{
				found.Add(match);
			}
		}

		return found;
	}
}
