// NullServices.cs
//
// Startup defaults for the services the UI layer installs at launch: the
// engine can be constructed (and unit-tested) headlessly before a resource
// loader or dispatcher timer exists.

using System;
using Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Engine;

/// <summary>Returns transparent placeholder icons until the UI layer installs a real loader.</summary>
public sealed class NullResourceService : IResourceService
{
	public ImageSurface GetIcon (string name, int size = 16)
		=> new (Format.Argb32, size, size);
}

/// <summary>
/// Forwards to the timer service the UI layer installs; before that, started
/// timers never tick.
/// </summary>
public sealed class TimerServiceProxy : ITimerService
{
	private sealed class NullHandle : IDisposable
	{
		public void Dispose ()
		{
		}
	}

	public ITimerService? Inner { get; set; }

	public IDisposable Start (uint intervalMilliseconds, Func<bool> callback)
		=> Inner?.Start (intervalMilliseconds, callback) ?? new NullHandle ();
}
