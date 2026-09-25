// IResourceService.cs
//
// Pinta.Brix replacement for the upstream resource manager's icon service.
// The UI layer implements icon loading from the application's bundled assets;
// the engine only ever asks for a decoded pixel surface.

using Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Engine;

public interface IResourceService
{
	/// <summary>Loads a named icon at the requested pixel size.</summary>
	ImageSurface GetIcon (string name, int size = 16);

	/// <summary>
	/// Whether a named icon actually exists.
	/// </summary>
	/// <remarks>
	/// Pinta.Brix note: <see cref="GetIcon"/> never fails - it hands back a
	/// blank surface for an unknown name - so a caller that wants to fall back
	/// to a text label needs to ask first. Upstream had no equivalent because
	/// GTK resolved its standard icon names from the system icon theme; this
	/// port ships only Pinta's own icon set, so the standard names are absent.
	/// </remarks>
	bool HasIcon (string name) => true;
}
