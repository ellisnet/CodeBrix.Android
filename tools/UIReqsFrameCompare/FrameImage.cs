namespace UIReqsFrameCompare
{
	/// <summary>
	/// ANDROID PORT: a decoded frame (straight RGBA8888 rows), standing in for the SKBitmap the
	/// CodeBrix.Platform tool used - the Android port carries no SkiaSharp.
	/// </summary>
	public sealed class FrameImage : IDisposable
	{
		/// <summary>Creates an image over pixels.</summary>
		public FrameImage(byte[] rgba, int width, int height)
		{
			Rgba = rgba;
			Width = width;
			Height = height;
		}

		/// <summary>The pixels.</summary>
		public byte[] Rgba { get; }

		/// <summary>Width.</summary>
		public int Width { get; }

		/// <summary>Height.</summary>
		public int Height { get; }

		/// <summary>The pixels as a span (SKBitmap.GetPixelSpan's shape).</summary>
		public Span<byte> GetPixelSpan() => Rgba;

		/// <summary>SKBitmap's change notification: nothing to do for a plain buffer.</summary>
		public void NotifyPixelsChanged()
		{
		}

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}
}
