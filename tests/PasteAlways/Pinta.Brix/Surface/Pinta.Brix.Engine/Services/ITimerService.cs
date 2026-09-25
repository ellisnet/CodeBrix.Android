// ITimerService.cs
//
// Pinta.Brix replacement for the main-loop timeout the upstream live-preview
// code used. The UI layer implements this with its dispatcher timer.

using System;

namespace Pinta.Brix.Engine;

public interface ITimerService
{
	/// <summary>
	/// Starts a repeating timer on the UI thread. The callback returns true
	/// to keep ticking or false to stop; disposing the returned handle also
	/// stops the timer.
	/// </summary>
	IDisposable Start (uint intervalMilliseconds, Func<bool> callback);
}
