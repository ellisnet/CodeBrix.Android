// ToolCursor.cs
//
// Pinta.Brix replacement for the toolkit cursor objects the upstream Pinta
// code used. The engine describes the cursor it wants (a named icon with a
// hotspot, a custom-drawn image such as a brush outline, or a standard
// pointer shape); the UI layer realizes it with platform cursor APIs.

using Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Engine;

public enum StandardCursor
{
	Default,
	Crosshair,
	Hand,
	Move,
	IBeam,
	NotAllowed,
	SizeNWSE,
	SizeNESW,
	SizeNS,
	SizeWE,
}

public sealed record ToolCursor
{
	/// <summary>Icon resource name to render as the cursor, when set.</summary>
	public string? IconName { get; init; }

	/// <summary>Custom-drawn cursor image (e.g. a brush-size outline), when set.</summary>
	public ImageSurface? Image { get; init; }

	/// <summary>Hotspot within the icon or image.</summary>
	public PointI Hotspot { get; init; }

	/// <summary>Standard pointer shape used when no icon or image is set.</summary>
	public StandardCursor Shape { get; init; } = StandardCursor.Default;

	public static ToolCursor FromIcon (string iconName, int hotspotX, int hotspotY)
		=> new () { IconName = iconName, Hotspot = new PointI (hotspotX, hotspotY) };

	public static ToolCursor FromImage (ImageSurface image, int hotspotX, int hotspotY)
		=> new () { Image = image, Hotspot = new PointI (hotspotX, hotspotY) };

	public static ToolCursor FromShape (StandardCursor shape)
		=> new () { Shape = shape };
}
