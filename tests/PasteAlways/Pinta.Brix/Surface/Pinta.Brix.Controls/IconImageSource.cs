// IconImageSource.cs
//
// Converts engine icon surfaces (premul BGRA32) into XAML image sources by
// copying pixels into a WriteableBitmap, using the platform's raw-buffer
// copy idiom (no per-icon stream wrapper).

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Controls;

public static class IconImageSource
{
	/// <summary>
	/// Every command, tool and pad button asks for an icon at one of a handful
	/// of sizes, so the working set is small and fixed; the bound is here so a
	/// caller that asks for arbitrary sizes cannot grow the cache forever.
	/// </summary>
	private const int CacheCapacity = 512;

	private static readonly BoundedCache<(string, int), ImageSource> cache = new (CacheCapacity);

	/// <summary>
	/// Renders the named icon at the given size as a XAML image source, or
	/// returns null when no icon of that name exists so a caller can fall back
	/// to a label rather than a blank square.
	/// </summary>
	/// <param name="iconName">The icon's name in the embedded icon set.</param>
	/// <param name="size">The width and height to render at, in pixels.</param>
	public static ImageSource? Create (string iconName, int size)
	{
		if (cache.TryGetValue ((iconName, size), out ImageSource? cached))
			return cached;

		// An unknown name must come back null so callers can fall back to a
		// label rather than rendering a blank square.
		if (!PintaCore.Resources.HasIcon (iconName))
			return null;

		ImageSurface surface = PintaCore.Resources.GetIcon (iconName, size);
		byte[] pixels = surface.GetData ().ToArray ();

		WriteableBitmap bitmap = new (surface.Width, surface.Height);
		pixels.CopyTo (bitmap.PixelBuffer);
		bitmap.Invalidate ();

		cache.Set ((iconName, size), bitmap);
		return bitmap;
	}
}
