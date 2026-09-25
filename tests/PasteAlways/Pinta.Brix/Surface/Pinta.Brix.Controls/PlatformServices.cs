// PlatformServices.cs
//
// CodeBrix.Platform implementations of the engine's UI-installed services:
// the dispatcher timer and the clipboard.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Pinta.Brix.Controls;

public sealed class DispatcherTimerService : ITimerService
{
	private readonly DispatcherQueue dispatcher;

	public DispatcherTimerService (DispatcherQueue dispatcher)
	{
		this.dispatcher = dispatcher;
	}

	private sealed class Handle : IDisposable
	{
		public DispatcherQueueTimer? Timer;

		public void Dispose ()
		{
			Timer?.Stop ();
			Timer = null;
		}
	}

	public IDisposable Start (uint intervalMilliseconds, Func<bool> callback)
	{
		Handle handle = new ();
		DispatcherQueueTimer timer = dispatcher.CreateTimer ();
		handle.Timer = timer;
		timer.Interval = TimeSpan.FromMilliseconds (intervalMilliseconds);
		timer.Tick += (_, _) => {
			if (!callback ())
				handle.Dispose ();
		};
		timer.Start ();
		return handle;
	}
}

public sealed class PlatformClipboardService : IClipboardService
{
	public void SetText (string text)
	{
		DataPackage package = new ();
		package.SetText (text);
		Clipboard.SetContent (package);
	}

	public async Task<string?> GetTextAsync ()
	{
		DataPackageView view = Clipboard.GetContent ();
		if (!view.Contains (StandardDataFormats.Text))
			return null;
		return await view.GetTextAsync ();
	}

	public void SetImage (ImageSurface surface)
	{
		// Encode as PNG and hand the platform a stream reference.
		// (Image WRITE is not yet supported by the X11 clipboard backend;
		// this degrades gracefully there.)
		using SKImage image = SKImage.FromBitmap (surface.Bitmap);
		using SKData data = image.Encode (SKEncodedImageFormat.Png, 100);

		InMemoryRandomAccessStream stream = new ();
		using (Stream outStream = stream.AsStreamForWrite ()) {
			data.SaveTo (outStream);
			outStream.Flush ();
		}
		stream.Seek (0);

		DataPackage package = new ();
		package.SetBitmap (RandomAccessStreamReference.CreateFromStream (stream));
		Clipboard.SetContent (package);
	}

	public async Task<ImageSurface?> GetImageAsync ()
	{
		DataPackageView view = Clipboard.GetContent ();
		if (!view.Contains (StandardDataFormats.Bitmap))
			return null;

		RandomAccessStreamReference reference = await view.GetBitmapAsync ();
		using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync ();
		using Stream readStream = stream.AsStreamForRead ();
		using SKBitmap? decoded = SKBitmap.Decode (readStream);
		if (decoded is null)
			return null;

		ImageSurface surface = new (Format.Argb32, decoded.Width, decoded.Height);
		using SKCanvas canvas = new (surface.Bitmap);
		canvas.DrawBitmap (decoded, 0, 0, SKSamplingOptions.Default, null);
		canvas.Flush ();
		surface.MarkDirty ();
		return surface;
	}
}
