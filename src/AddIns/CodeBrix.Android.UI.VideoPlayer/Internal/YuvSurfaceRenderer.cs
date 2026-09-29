// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/YuvSurfaceRenderer.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim.

#nullable enable

// Ported from CodeBrix.VideoPlayback.Skia (commit a3f3051, MIT, same author) on 2026-08-30;
// compiled against the Platform family's SkiaSharp.

using System;
using System.Collections.Generic;
using CodeBrix.VideoPlayback;
using CodeBrix.VideoPlayback.Color.Luts;
using CodeBrix.VideoPlayback.Decoding;
using CodeBrix.VideoPlayback.Frames;
using CodeBrix.VideoPlayback.Rendering;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal; //was previously: CodeBrix.VideoPlayback.Skia.Internal;

/// <summary>
/// Draws one decoded frame onto a surface with the colour shader: three planes in, colour-converted
/// and effect-graded pixels out, in a single pass.
/// </summary>
/// <remarks>
/// <para>
/// It works with or without a graphics context. WITH one, the planes are uploaded as single-channel
/// textures and the shader runs on the device - the shipping arrangement. WITHOUT one, the planes
/// stay host images and the shader runs on Skia's raster backend, which is slower than the playback
/// engine's vector converter and is therefore never a render path - but it IS the same shader, the
/// same uniforms and the same bindings, which makes it exactly what a test needs on a machine with
/// no display.
/// </para>
/// <para>The compiled shaders are cached, so a steady stream of frames compiles nothing.</para>
/// <para>
/// The shader text, its child names and the colour numbers it is fed all come from the playback
/// engine (<see cref="YuvShaderSource"/> and <see cref="YuvShaderUniforms"/>), which has no drawing
/// dependency of its own - so this class is only the SkiaSharp binding of maths that lives
/// elsewhere.
/// </para>
/// </remarks>
internal sealed class YuvSurfaceRenderer : IDisposable
{
	private static readonly SKSamplingOptions LumaSampling =
		new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);

	private static readonly SKSamplingOptions SmoothSampling =
		new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

	private static readonly SKSamplingOptions ExactSampling =
		new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);

	private readonly List<SKImage> _images = new List<SKImage>(6);

	private SKRuntimeEffect? _plainEffect;
	private SKRuntimeEffect? _tetrahedralEffect;
	private SKRuntimeEffect? _trilinearEffect;

	/// <summary>Draws a frame onto a surface.</summary>
	/// <param name="frame">The frame to draw.</param>
	/// <param name="surface">The surface to draw onto, which must be the frame's coded size.</param>
	/// <param name="graphicsContext">
	/// The context whose textures the planes should be uploaded to, or null to run on the raster
	/// backend with host images.
	/// </param>
	/// <param name="lookupAtlas">The composed effect atlas, or null when there is no effect chain.</param>
	/// <param name="lookupSize">The number of nodes a side of the atlas, ignored when there is no atlas.</param>
	/// <param name="interpolation">
	/// How the atlas is read between its nodes, ignored when there is no atlas. Each way has its own
	/// compiled shader and its own atlas filter.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="frame"/> or <paramref name="surface"/> is null.
	/// </exception>
	/// <exception cref="VideoPlaybackException">
	/// A shader would not compile, a plane would not upload, or the backend refused the shader.
	/// </exception>
	internal void Render(
		VideoFrame frame,
		SKSurface surface,
		GRContext? graphicsContext,
		SKImage? lookupAtlas,
		int lookupSize,
		LutInterpolation interpolation)
	{
		if (frame is null)
		{
			throw new ArgumentNullException(nameof(frame));
		}

		if (surface is null)
		{
			throw new ArgumentNullException(nameof(surface));
		}

		_images.Clear();

		try
		{
			var monochrome = frame.Layout == VideoPixelLayout.Gray || frame.U.IsEmpty || frame.V.IsEmpty;
			var resolved = frame.Color.Resolve(frame.Height);
			var numbers = YuvShaderUniforms.Create(resolved, frame.BitDepth, frame.Layout, monochrome);

			var luma = PreparePlane(frame.Y, "luma", graphicsContext);
			var blueChroma = monochrome ? luma : PreparePlane(frame.U, "first chroma", graphicsContext);
			var redChroma = monochrome ? luma : PreparePlane(frame.V, "second chroma", graphicsContext);

			var useLookup = lookupAtlas is not null;
			var effect = useLookup ? EnsureLookupEffect(interpolation) : EnsurePlainEffect();

			using var lumaShader =
				luma.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, LumaSampling);
			using var blueShader =
				blueChroma.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SmoothSampling);
			using var redShader =
				redChroma.ToRawShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SmoothSampling);

			// The trilinear variant LEANS ON the sampler's filter; the tetrahedral one fetches node
			// values and a filter would blend them into something that is not a node at all.
			using var lookupShader = useLookup
				? lookupAtlas!.ToRawShader(
					SKShaderTileMode.Clamp,
					SKShaderTileMode.Clamp,
					YuvShaderSource.NeedsFilteredAtlas(interpolation) ? SmoothSampling : ExactSampling)
				: null;

			using var uniforms = new SKRuntimeEffectUniforms(effect);
			uniforms.Add("chromaShift", new[] { numbers.ChromaShiftX, numbers.ChromaShiftY });
			uniforms.Add("chromaCosited", new[] { numbers.ChromaCositedX, numbers.ChromaCositedY });
			uniforms.Add("planeMaximum", numbers.PlaneMaximum);
			uniforms.Add("sampleOffsets", new[] { numbers.LumaOffset, numbers.ChromaOffset, numbers.ChromaOffset });
			uniforms.Add("redRow", numbers.RedRow);
			uniforms.Add("greenRow", numbers.GreenRow);
			uniforms.Add("blueRow", numbers.BlueRow);

			if (useLookup)
			{
				uniforms.Add("lookupSize", (float)lookupSize);
			}

			using var children = new SKRuntimeEffectChildren(effect);
			children.Add(YuvShaderSource.LumaChild, new SKRuntimeEffectChild(lumaShader));
			children.Add(YuvShaderSource.ChromaBlueChild, new SKRuntimeEffectChild(blueShader));
			children.Add(YuvShaderSource.ChromaRedChild, new SKRuntimeEffectChild(redShader));

			if (useLookup)
			{
				children.Add(YuvShaderSource.LookupChild, new SKRuntimeEffectChild(lookupShader));
			}

			using var shader = effect.ToShader(uniforms, children);
			if (shader is null)
			{
				throw new VideoPlaybackException(
					"SkiaSharp would not build the colour shader from its uniforms and planes. The graphics " +
					"backend in use may not support runtime effects; set RenderPath to Cpu to use the " +
					"processor path instead.");
			}

			using var paint = new SKPaint
			{
				Shader = shader,
				IsAntialias = false,
				BlendMode = SKBlendMode.Src,
			};

			surface.Canvas.DrawRect(SKRect.Create(0f, 0f, frame.Width, frame.Height), paint);
			surface.Flush();

			if (graphicsContext is not null)
			{
				graphicsContext.Flush();
				graphicsContext.Submit(false);
			}
		}
		finally
		{
			for (var i = 0; i < _images.Count; i++)
			{
				_images[i].Dispose();
			}

			_images.Clear();
		}
	}

	/// <summary>Releases the compiled shaders.</summary>
	public void Dispose()
	{
		_plainEffect?.Dispose();
		_plainEffect = null;
		_tetrahedralEffect?.Dispose();
		_tetrahedralEffect = null;
		_trilinearEffect?.Dispose();
		_trilinearEffect = null;
	}

	private SKImage PreparePlane(in VideoFramePlane plane, string which, GRContext? graphicsContext)
	{
		var type = plane.BytesPerSample >= 2 ? SKColorType.R16Unorm : SKColorType.R8Unorm;
		var info = new SKImageInfo(plane.Width, plane.Height, type, SKAlphaType.Opaque);

		SKImage? host;
		using (var pixmap = new SKPixmap(info, plane.Data, plane.Stride))
		{
			// FromPixels BORROWS the memory rather than copying it: the samples the decoder wrote go
			// to the driver exactly where they already are, which is the whole point of the pinned
			// pool.
			host = SKImage.FromPixels(pixmap);
		}

		if (host is null)
		{
			throw new VideoPlaybackException(
				$"SkiaSharp would not wrap this frame's {which} plane ({plane.Width}x{plane.Height}, " +
				$"{plane.BytesPerSample * 8}-bit samples, stride {plane.Stride}) as an image, so it cannot " +
				"be drawn. Set RenderPath to Cpu to use the processor path instead.");
		}

		_images.Add(host);

		if (graphicsContext is null)
		{
			return host;
		}

		var texture = host.ToTextureImage(graphicsContext);
		if (texture is null)
		{
			throw new VideoPlaybackException(
				$"The graphics context would not accept this frame's {which} plane as a " +
				$"{(plane.BytesPerSample >= 2 ? "16" : "8")}-bit single-channel texture " +
				$"({plane.Width}x{plane.Height}). The backend may not support that texture format; set " +
				"RenderPath to Cpu to use the processor path instead.");
		}

		_images.Add(texture);
		return texture;
	}

	private SKRuntimeEffect EnsurePlainEffect() => _plainEffect ??= Compile(YuvShaderSource.Build(), null);

	private SKRuntimeEffect EnsureLookupEffect(LutInterpolation interpolation)
	{
		if (interpolation == LutInterpolation.Trilinear)
		{
			return _trilinearEffect ??= Compile(YuvShaderSource.Build(interpolation), interpolation);
		}

		return _tetrahedralEffect ??= Compile(YuvShaderSource.Build(interpolation), interpolation);
	}

	private static SKRuntimeEffect Compile(string source, LutInterpolation? interpolation)
	{
		var effect = SKRuntimeEffect.CreateShader(source, out var errors);

		if (effect is null)
		{
			throw new VideoPlaybackException(
				"SkiaSharp would not compile the colour shader" +
				(interpolation.HasValue
					? $" with its {interpolation.Value.ToString().ToLowerInvariant()} lookup-table stage"
					: string.Empty) +
				$": {errors}. Set RenderPath to Cpu to use the processor path instead.");
		}

		return effect;
	}
}
