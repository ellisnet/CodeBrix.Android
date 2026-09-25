// HistoryRowFactory.cs
//
// Builds the history pad's rows. Upstream showed each item's icon and text and
// dimmed the entries that have been undone but not yet discarded, so the pad
// doubles as a preview of what Redo will do.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pinta.Brix.Engine;

namespace Pinta.Brix.Controls;

/// <summary>Creates the rows shown in the history pad.</summary>
public static class HistoryRowFactory
{
	/// <summary>The icon size history rows are rendered at.</summary>
	public const int IconSize = 16;

	/// <summary>
	/// Creates a row for a history item.
	/// </summary>
	/// <param name="item">The history item the row represents.</param>
	/// <param name="undone">
	/// True when the item sits after the history pointer - undone, and
	/// reachable again with Redo.
	/// </param>
	/// <returns>The row.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
	public static UIElement Create (BaseHistoryItem item, bool undone)
	{
		ArgumentNullException.ThrowIfNull (item);

		StackPanel row = new () {
			Orientation = Orientation.Horizontal,
			Spacing = 6,
			// Dimming is what tells a user the entry is "ahead" of where the
			// document currently is.
			Opacity = undone ? 0.45 : 1.0,
		};

		if (!string.IsNullOrEmpty (item.Icon)) {

			var source = IconImageSource.Create (item.Icon, IconSize);

			if (source is not null) {
				row.Children.Add (new Image {
					Width = IconSize,
					Height = IconSize,
					Source = source,
					VerticalAlignment = VerticalAlignment.Center,
				});
			}
		}

		row.Children.Add (new TextBlock {
			Text = item.Text ?? string.Empty,
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center,
		});

		return row;
	}
}
