// IClipboardService.cs
//
// Pinta.Brix replacement for the toolkit clipboard object the upstream Pinta
// code passed to tools and edit actions. The UI layer implements this over
// the platform clipboard.

using System.Threading.Tasks;
using Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Engine;

public interface IClipboardService
{
	void SetText (string text);

	Task<string?> GetTextAsync ();

	void SetImage (ImageSurface surface);

	Task<ImageSurface?> GetImageAsync ();
}
