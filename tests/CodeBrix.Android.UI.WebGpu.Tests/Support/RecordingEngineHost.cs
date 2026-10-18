// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/RecordingEngineHost.cs
// (branch main working tree of 2026-10-17 - round 2, UNCOMMITTED at the time of the port, on top of 7a64364e606320e6322bc598f74ec352e99be9db; the commit hash can be added later).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly; (2) no direct-frame source (the host interface has no PresentDirect on Android): PresentSurfaceFrame (the DirectSurface path) is counted instead.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.Platform.UI.WebGpu;
using SkiaSharp;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// The render engine's owner, as a test double: it records every callback (in order, with the thread it came on) and
/// keeps the latest presented picture, exactly as the view's surface would.
/// </summary>
internal sealed class RecordingEngineHost : IWebGpuRenderEngineHost
{
	private readonly object _gate = new();
	private readonly List<string> _events = new();
	private SKImage? _picture;
	private int _presented;
	private int _surfaceFrames;

	/// <summary>The callbacks so far, e.g. "DeviceReady", "DeviceLost:reason", "Path:None->Readback".</summary>
	internal IReadOnlyList<string> Events
	{
		get
		{
			lock (_gate)
			{
				return _events.ToArray();
			}
		}
	}

	/// <summary>The managed thread ids the callbacks ran on, in the same order.</summary>
	internal List<int> EventThreads { get; } = new();

	/// <summary>How many pictures were presented.</summary>
	internal int Presented => Volatile.Read(ref _presented);

	/// <summary>How many frames the DirectSurface path presented to its window surface.</summary>
	internal int SurfaceFrames => Volatile.Read(ref _surfaceFrames);

	/// <summary>The reason of the last DeviceLost, or null.</summary>
	internal string? LastLostReason { get; private set; }

	/// <summary>True to record every Readback present as "Present" in <see cref="Events"/> too (off by default, so the
	/// lifecycle sequences other tests assert stay as they are).</summary>
	internal bool RecordPresents { get; set; }

	/// <summary>Runs when DeviceReady is raised (the test's "renderer").</summary>
	internal System.Action? OnReady { get; set; }

	/// <summary>Runs when DeviceLost is raised.</summary>
	internal System.Action<string>? OnLost { get; set; }

	public void OnDeviceReady()
	{
		Record("DeviceReady");
		OnReady?.Invoke();
	}

	public void OnDeviceLost(string reason)
	{
		LastLostReason = reason;
		Record("DeviceLost:" + reason);
		OnLost?.Invoke(reason);
	}

	public void OnRenderPathChanged(WebGpuRenderPath oldPath, WebGpuRenderPath newPath) => Record($"Path:{oldPath}->{newPath}");

	public void Present(SKImage frame)
	{
		SKImage? previous;
		lock (_gate)
		{
			previous = _picture;
			_picture = frame;
		}

		previous?.Dispose();
		Interlocked.Increment(ref _presented);
		if (RecordPresents)
		{
			Record("Present");
		}
	}

	// ANDROID PORT (2): no PresentDirect / DirectSource (the desktop direct paths are not ported).
	public void PresentSurfaceFrame() => Interlocked.Increment(ref _surfaceFrames);

	public void ClearPresented()
	{
		SKImage? previous;
		lock (_gate)
		{
			previous = _picture;
			_picture = null;
		}

		previous?.Dispose();
		Record("ClearPresented");
	}

	/// <summary>The colour of one pixel of the latest picture (unpremultiplied), or null when there is none.</summary>
	/// <param name="x">The column.</param>
	/// <param name="y">The row.</param>
	/// <returns>The colour.</returns>
	internal SKColor? PixelAt(int x, int y)
	{
		lock (_gate)
		{
			if (_picture is not { } picture)
			{
				return null;
			}

			using var bitmap = new SKBitmap(new SKImageInfo(picture.Width, picture.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
			if (!picture.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
			{
				return null;
			}

			return bitmap.GetPixel(x, y);
		}
	}

	/// <summary>
	/// Records the latest picture into an SKPicture, as a desktop head records the window frame on the user-interface
	/// thread to replay it later on its render thread. Null when there is no picture.
	/// </summary>
	/// <returns>The recording (the caller disposes it).</returns>
	internal SKPicture? RecordLatestPicture()
	{
		lock (_gate)
		{
			if (_picture is not { } picture)
			{
				return null;
			}

			using var recorder = new SKPictureRecorder();
			var canvas = recorder.BeginRecording(new SKRect(0, 0, picture.Width, picture.Height));
			canvas.DrawImage(picture, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
			return recorder.EndRecording();
		}
	}

	/// <summary>The latest picture's size, or (0, 0).</summary>
	internal (int Width, int Height) PictureSize
	{
		get
		{
			lock (_gate)
			{
				return _picture is { } picture ? (picture.Width, picture.Height) : (0, 0);
			}
		}
	}

	private void Record(string entry)
	{
		lock (_gate)
		{
			_events.Add(entry);
			EventThreads.Add(System.Environment.CurrentManagedThreadId);
		}
	}
}
