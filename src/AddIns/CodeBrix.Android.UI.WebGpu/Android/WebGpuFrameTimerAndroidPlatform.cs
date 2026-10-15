// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Skia/WebGpuFrameTimerSkiaPlatform.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the class name and namespace (this assembly's Android/ folder); the DispatcherQueue is CodeBrix.Android.UI.Dispatching's (the Android main looper).

#nullable enable

using System;
using CodeBrix.Platform.UI.WebGpu.Contracts;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.WebGpu.Android; // ANDROID PORT (1)

/// <summary>
/// The Android implementation (ported from the CodeBrix.Platform one) of <see cref="IWebGpuFrameTimer"/>: a non-repeating DispatcherQueueTimer on the
/// element's dispatcher for timed ticks, and the dispatcher queue itself (at low priority, behind input and layout)
/// for "as soon as possible" ticks - the same user-interface-thread machinery the VideoPlayer driver posts its frames
/// through.
/// </summary>
/// <remarks>
/// The callbacks are cached delegates and the timer is created once, so neither method allocates per frame.
/// </remarks>
internal sealed class WebGpuFrameTimerAndroidPlatform : IWebGpuFrameTimer // ANDROID PORT (1)
{
	private readonly FrameworkElement _owner;
	private readonly DispatcherQueue? _creationQueue;
	private readonly DispatcherQueueHandler _postHandler;
	private DispatcherQueueTimer? _timer;
	private Action? _timedCallback;
	private volatile Action? _postedCallback;

	/// <summary>Creates the timer for one element (on the element's thread).</summary>
	/// <param name="owner">The element.</param>
	internal WebGpuFrameTimerAndroidPlatform(FrameworkElement owner)
	{
		_owner = owner;
		_creationQueue = DispatcherQueue.GetForCurrentThread();
		_postHandler = RunPosted;
	}

	private DispatcherQueue? Queue => _owner.DispatcherQueue ?? _creationQueue;

	/// <inheritdoc/>
	public void Schedule(TimeSpan delay, Action callback)
	{
		if (Queue is not { } queue)
		{
			return;
		}

		if (_timer is null)
		{
			_timer = queue.CreateTimer();
			_timer.IsRepeating = false;
			_timer.Tick += OnTimerTick;
		}

		_timedCallback = callback;
		_timer.Stop();
		_timer.Interval = delay;
		_timer.Start();
	}

	/// <inheritdoc/>
	public void Cancel() => _timer?.Stop();

	/// <inheritdoc/>
	public bool Post(Action callback)
	{
		_postedCallback = callback;
		return Queue is { } queue && queue.TryEnqueue(DispatcherQueuePriority.Low, _postHandler);
	}

	private void OnTimerTick(DispatcherQueueTimer sender, object args)
	{
		sender.Stop();
		_timedCallback?.Invoke();
	}

	private void RunPosted() => _postedCallback?.Invoke();
}
