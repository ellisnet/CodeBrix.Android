// LayerRowFactory.cs
//
// Builds the layers pad's rows. Upstream's LayersListViewItemWidget drew a
// visibility check box, an ellipsized name and a 60x40 live thumbnail; this is
// the same row assembled from platform controls.

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;

namespace Pinta.Brix.Controls;

/// <summary>Creates the rows shown in the layers pad.</summary>
public static class LayerRowFactory
{
	/// <summary>The thumbnail's width, matching upstream.</summary>
	public const int ThumbnailWidth = 60;

	/// <summary>The thumbnail's height, matching upstream.</summary>
	public const int ThumbnailHeight = 40;

	/// <summary>
	/// Creates a row for a layer.
	/// </summary>
	/// <param name="layer">The layer the row represents.</param>
	/// <returns>The row.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="layer"/> is null.</exception>
	public static UIElement Create (UserLayer layer)
	{
		ArgumentNullException.ThrowIfNull (layer);

		Grid row = new () { ColumnSpacing = 6 };
		row.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
		row.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
		row.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });

		CheckBox visible = new () {
			IsChecked = !layer.Hidden,
			MinWidth = 0,
			VerticalAlignment = VerticalAlignment.Center,
		};

		visible.Checked += (_, _) => SetHidden (layer, false);
		visible.Unchecked += (_, _) => SetHidden (layer, true);

		Grid.SetColumn (visible, 0);
		row.Children.Add (visible);

		Image thumbnail = new () {
			Width = ThumbnailWidth,
			Height = ThumbnailHeight,
			Source = CreateThumbnail (layer),
			VerticalAlignment = VerticalAlignment.Center,
		};

		Grid.SetColumn (thumbnail, 1);
		row.Children.Add (thumbnail);

		TextBlock name = new () {
			Text = layer.Name,
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center,
		};

		Grid.SetColumn (name, 2);
		row.Children.Add (name);

		return row;
	}

	private static void SetHidden (UserLayer layer, bool hidden)
	{
		if (layer.Hidden == hidden)
			return;

		layer.Hidden = hidden;

		if (PintaCore.Workspace.HasOpenDocuments)
			PintaCore.Workspace.ActiveWorkspace.Invalidate ();
	}

	private static WriteableBitmap? CreateThumbnail (UserLayer layer)
	{
		ImageSurface surface = layer.Surface;

		if (surface.Width <= 0 || surface.Height <= 0)
			return null;

		WriteableBitmap bitmap = new (ThumbnailWidth, ThumbnailHeight);

		SKImageInfo info = new (ThumbnailWidth, ThumbnailHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
		using SKBitmap scaled = new (info);

		using (SKCanvas canvas = new (scaled)) {

			canvas.Clear (SKColors.Transparent);

			// Letterbox rather than stretch, so a tall or wide layer still reads
			// as the right shape at thumbnail size.
			float scale = Math.Min (
				ThumbnailWidth / (float) surface.Width,
				ThumbnailHeight / (float) surface.Height);

			float width = surface.Width * scale;
			float height = surface.Height * scale;
			float x = (ThumbnailWidth - width) / 2f;
			float y = (ThumbnailHeight - height) / 2f;

			canvas.DrawBitmap (
				surface.Bitmap,
				new SKRect (x, y, x + width, y + height),
				SKSamplingOptions.Default);

			canvas.Flush ();
		}

		scaled.Bytes.CopyTo (bitmap.PixelBuffer);
		bitmap.Invalidate ();

		return bitmap;
	}
}
