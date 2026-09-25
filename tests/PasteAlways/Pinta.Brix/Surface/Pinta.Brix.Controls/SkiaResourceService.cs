// SkiaResourceService.cs
//
// Icon loader for Pinta.Brix: serves the upstream icon set (embedded in this
// assembly under Assets/icons) as engine ImageSurfaces. PNG sizes are used
// when available; scalable SVG icons are rendered with CodeBrix.SkiaSvg.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;
using CodeBrix.SkiaSvg;

namespace Pinta.Brix.Controls;

public sealed class SkiaResourceService : IResourceService
{
	private readonly Assembly assembly = typeof (SkiaResourceService).Assembly;
	/// <summary>
	/// Bounded so that a long session asking for many icon sizes cannot grow
	/// the cache without limit; every icon the application actually uses fits
	/// several times over.
	/// </summary>
	private const int CacheCapacity = 512;

	private readonly BoundedCache<(string, int), ImageSurface> cache = new (CacheCapacity);
	private readonly List<string> resource_names;

	public SkiaResourceService ()
	{
		resource_names = [.. assembly.GetManifestResourceNames ()];
	}

	public ImageSurface GetIcon (string name, int size = 16)
	{
		if (cache.TryGetValue ((name, size), out ImageSurface? cached))
			return cached;

		ImageSurface icon = LoadIcon (name, size) ?? new ImageSurface (Format.Argb32, size, size);
		cache.Set ((name, size), icon);
		return icon;
	}

	public bool HasIcon (string name)
	{
		if (string.IsNullOrEmpty (name))
			return false;

		return resource_names.Any (r =>
			r.EndsWith ($".{name}.png", StringComparison.Ordinal)
			|| r.EndsWith ($".{name}.svg", StringComparison.Ordinal));
	}

	private ImageSurface? LoadIcon (string name, int size)
	{
		// Embedded resource names look like:
		//   Pinta.Brix.Controls.Assets.icons.hicolor.16x16.actions.<name>.png
		//   Pinta.Brix.Controls.Assets.icons.hicolor.scalable.actions.<name>.svg
		string pngSuffix = $".{name}.png";
		string svgSuffix = $".{name}.svg";

		// Prefer an exact-size PNG, then the nearest larger size, then SVG.
		string? exact = resource_names.FirstOrDefault (r => r.Contains ($".{size}x{size}.") && r.EndsWith (pngSuffix, StringComparison.Ordinal));
		if (exact is not null)
			return DecodePng (exact, size);

		string? svg = resource_names.FirstOrDefault (r => r.Contains (".scalable.") && r.EndsWith (svgSuffix, StringComparison.Ordinal));
		if (svg is not null)
			return RenderSvg (svg, size);

		string? anyPng =
			resource_names
			.Where (r => r.EndsWith (pngSuffix, StringComparison.Ordinal))
			.OrderBy (r => r)
			.LastOrDefault ();
		if (anyPng is not null)
			return DecodePng (anyPng, size);

		return null;
	}

	private ImageSurface? DecodePng (string resourceName, int size)
	{
		using Stream? stream = assembly.GetManifestResourceStream (resourceName);
		if (stream is null)
			return null;

		using SKBitmap? decoded = SKBitmap.Decode (stream);
		if (decoded is null)
			return null;

		ImageSurface surface = new (Format.Argb32, size, size);
		using SKCanvas canvas = new (surface.Bitmap);
		SKRect dest = new (0, 0, size, size);
		using SKPaint paint = new () { IsAntialias = true };
		// SKSamplingOptions.Default is what the retired DrawBitmap overload used,
		// so the icon scaling stays pixel-for-pixel what it was.
		canvas.DrawBitmap (decoded, dest, SKSamplingOptions.Default, paint);
		canvas.Flush ();
		surface.MarkDirty ();
		return surface;
	}

	private ImageSurface? RenderSvg (string resourceName, int size)
	{
		using Stream? stream = assembly.GetManifestResourceStream (resourceName);
		if (stream is null)
			return null;

		using SKSvg svg = new ();
		SKPicture? picture = svg.Load (stream);
		if (picture is null)
			return null;

		ImageSurface surface = new (Format.Argb32, size, size);
		using SKCanvas canvas = new (surface.Bitmap);
		SKRect bounds = picture.CullRect;
		if (bounds.Width > 0 && bounds.Height > 0) {
			float scale = Math.Min (size / bounds.Width, size / bounds.Height);
			canvas.Scale (scale);
			canvas.Translate (-bounds.Left, -bounds.Top);
		}
		canvas.DrawPicture (picture);
		canvas.Flush ();
		surface.MarkDirty ();
		return surface;
	}
}
