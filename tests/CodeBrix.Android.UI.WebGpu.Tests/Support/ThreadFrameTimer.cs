// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/ThreadFrameTimer.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using CodeBrix.Platform.UI.WebGpu.Contracts;
using CodeBrix.Platform.UI.WebGpu;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// A frame timer with a thread of its own standing in for a head's user-interface thread: queued callbacks run there
/// in order, and a timed callback is queued there when its delay ends. The real Skia timer does the same on the
/// dispatcher.
/// </summary>
internal sealed class ThreadFrameTimer : IWebGpuFrameTimer, IDisposable
{
	private readonly BlockingCollection<Action> _work = new();
	private readonly Thread _thread;
	private readonly Timer _timer;
	private Action? _timed;
	private int _generation;

	internal ThreadFrameTimer()
	{
		_timer = new Timer(_ => QueueTimed(), null, Timeout.Infinite, Timeout.Infinite);
		_thread = new Thread(Run) { IsBackground = true, Name = "WebGpu test owner thread" };
		_thread.Start();
	}

	/// <summary>The owner thread's managed id.</summary>
	internal int ThreadId => _thread.ManagedThreadId;

	public void Schedule(TimeSpan delay, Action callback)
	{
		Interlocked.Increment(ref _generation);
		Volatile.Write(ref _timed, callback);
		_timer.Change(delay < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : delay, Timeout.InfiniteTimeSpan);
	}

	public void Cancel()
	{
		Interlocked.Increment(ref _generation);
		_timer.Change(Timeout.Infinite, Timeout.Infinite);
	}

	public bool Post(Action callback)
	{
		if (_work.IsAddingCompleted)
		{
			return false;
		}

		_work.Add(callback);
		return true;
	}

	/// <summary>Runs <paramref name="action"/> on the owner thread and waits for it.</summary>
	/// <param name="action">What to run.</param>
	internal void Invoke(Action action)
	{
		using var done = new ManualResetEventSlim();
		Exception? failure = null;
		_work.Add(() =>
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				failure = e;
			}
			finally
			{
				done.Set();
			}
		});
		done.Wait(TimeSpan.FromSeconds(30));
		if (failure is not null)
		{
			throw new InvalidOperationException("The owner-thread action failed: " + failure.Message, failure);
		}
	}

	public void Dispose()
	{
		_timer.Dispose();
		_work.CompleteAdding();
		_thread.Join(TimeSpan.FromSeconds(10));
		_work.Dispose();
	}

	private void QueueTimed()
	{
		var generation = Volatile.Read(ref _generation);
		var callback = Volatile.Read(ref _timed);
		if (callback is null || _work.IsAddingCompleted)
		{
			return;
		}

		_work.Add(() =>
		{
			if (Volatile.Read(ref _generation) == generation)
			{
				callback();
			}
		});
	}

	private void Run()
	{
		foreach (var action in _work.GetConsumingEnumerable())
		{
			action();
		}
	}
}
