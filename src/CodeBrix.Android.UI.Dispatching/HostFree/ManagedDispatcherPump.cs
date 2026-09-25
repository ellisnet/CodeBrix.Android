using System;
using System.Collections.Generic;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Dispatching;
using CodeBrix.Platform.UI.Dispatching.Contracts;

namespace CodeBrix.Android.UI.Dispatching.HostFree;

/// <summary>
/// The host-free <see cref="IDispatcherPumpPlatform"/> (net10.0 flavor only): Core's dispatch
/// requests are queued and run when the host calls <see cref="RunPending"/> - a test drains the
/// UI work it caused, deterministically, on its own thread. Every thread counts as the UI thread
/// (host-free use is single-threaded by construction).
/// </summary>
internal sealed class ManagedDispatcherPump : IDispatcherPumpPlatform
{
    private static readonly object _gate = new();
    private static ManagedDispatcherPump _instance;
    private readonly Queue<Action> _pending = new();

    /// <summary>The registered pump (registers it on first use; idempotent).</summary>
    internal static ManagedDispatcherPump EnsureRegistered()
    {
        lock (_gate)
        {
            if (_instance == null)
            {
                var pump = new ManagedDispatcherPump();
                ApiExtensibility.Register(typeof(IDispatcherPumpPlatform), _ => pump);
                _instance = pump;
            }

            return _instance;
        }
    }

    /// <inheritdoc />
    public bool HasThreadAccess => true;

    /// <summary>The number of dispatches waiting to run.</summary>
    internal int PendingCount
    {
        get
        {
            lock (_pending)
            {
                return _pending.Count;
            }
        }
    }

    /// <inheritdoc />
    public void Schedule(Action dispatchCallback, NativeDispatcherPriority priority)
    {
        ArgumentNullException.ThrowIfNull(dispatchCallback);
        lock (_pending)
        {
            _pending.Enqueue(dispatchCallback);
        }
    }

    /// <summary>
    /// Runs the queued dispatches, and the ones they queue, until the queue is empty or
    /// <paramref name="maxDispatches"/> ran. Returns the number that ran.
    /// </summary>
    internal int RunPending(int maxDispatches = 1000)
    {
        var ran = 0;
        while (ran < maxDispatches)
        {
            Action next;
            lock (_pending)
            {
                if (_pending.Count == 0)
                {
                    break;
                }

                next = _pending.Dequeue();
            }

            next();
            ran++;
        }

        return ran;
    }
}
