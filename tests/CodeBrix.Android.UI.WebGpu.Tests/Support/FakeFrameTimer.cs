// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/FakeFrameTimer.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly.

#nullable enable

using System;
using System.Collections.Generic;
using CodeBrix.Platform.UI.WebGpu.Contracts;
using CodeBrix.Platform.UI.WebGpu;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// A frame timer the test drives by hand: it records what the scheduler asked for and runs it only when told, so the
/// scheduler's decisions can be asserted one tick at a time.
/// </summary>
internal sealed class FakeFrameTimer : IWebGpuFrameTimer
{
	private readonly Queue<Action> _posted = new();
	private Action? _timed;

	/// <summary>The delay of the pending timed callback, or null.</summary>
	internal TimeSpan? PendingDelay { get; private set; }

	/// <summary>How many callbacks are queued.</summary>
	internal int PostedCount => _posted.Count;

	/// <summary>How many times a timed callback was scheduled.</summary>
	internal int ScheduleCalls { get; private set; }

	/// <summary>How many times Cancel was called.</summary>
	internal int CancelCalls { get; private set; }

	/// <summary>False makes Post report that the thread is gone.</summary>
	internal bool AcceptPosts { get; set; } = true;

	public void Schedule(TimeSpan delay, Action callback)
	{
		ScheduleCalls++;
		PendingDelay = delay;
		_timed = callback;
	}

	public void Cancel()
	{
		CancelCalls++;
		PendingDelay = null;
		_timed = null;
	}

	public bool Post(Action callback)
	{
		if (!AcceptPosts)
		{
			return false;
		}

		_posted.Enqueue(callback);
		return true;
	}

	/// <summary>Runs the callbacks queued so far (one dispatcher turn); ones they queue wait for the next call.</summary>
	/// <returns>How many ran.</returns>
	internal int RunPosted()
	{
		var count = _posted.Count;
		for (var i = 0; i < count; i++)
		{
			_posted.Dequeue()();
		}

		return count;
	}

	/// <summary>Runs the pending timed callback, if any.</summary>
	/// <returns>True when one ran.</returns>
	internal bool RunTimed()
	{
		if (_timed is not { } timed)
		{
			return false;
		}

		_timed = null;
		PendingDelay = null;
		timed();
		return true;
	}
}
