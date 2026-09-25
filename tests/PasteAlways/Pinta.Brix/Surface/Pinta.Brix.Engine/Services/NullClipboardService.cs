// NullClipboardService.cs
//
// Stands in for the platform clipboard until the UI layer installs the real
// one, so engine and tool code can call PintaCore.Clipboard unconditionally
// (and so headless tests get a clipboard that does nothing rather than a
// null reference).

using System.Threading.Tasks;
using Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Engine;

/// <summary>A clipboard that holds nothing and accepts everything.</summary>
public sealed class NullClipboardService : IClipboardService
{
	/// <summary>Discards the text.</summary>
	public void SetText (string text) { }

	/// <summary>Always reports no text.</summary>
	public Task<string?> GetTextAsync () => Task.FromResult<string?> (null);

	/// <summary>Discards the image.</summary>
	public void SetImage (ImageSurface surface) { }

	/// <summary>Always reports no image.</summary>
	public Task<ImageSurface?> GetImageAsync () => Task.FromResult<ImageSurface?> (null);
}
