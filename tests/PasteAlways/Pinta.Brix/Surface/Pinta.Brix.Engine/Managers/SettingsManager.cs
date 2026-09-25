//
// SettingsManager.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Pinta.Brix.Settings;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public interface ISettingsService
{
	/// <summary>
	/// Retrieves stored setting with the specified key. The specified default value is
	/// returned if the setting cannot be found or contains an invalid value.
	/// </summary>
	T GetSetting<T> (string key, T defaultValue);

	/// <summary>
	/// Returns the user settings directory.
	/// </summary>
	string GetUserSettingsDirectory ();

	/// <summary>
	/// Stores a setting with specified key and value for future application launches.
	/// </summary>
	void PutSetting (string key, object value);

	/// <summary>
	/// An event fired when settings should be flushed to storage, giving
	/// subscribers a chance to call PutSetting to store their current values.
	/// </summary>
	event EventHandler? SaveSettingsBeforeQuit;
}

// Pinta.Brix note: upstream kept its settings in an in-memory dictionary that
// was serialised to settings.xml ONCE, on quit. This port stores everything in
// the single portable settings.sqlite instead (see Pinta.Brix.Settings), and
// every PutSetting WRITES THROUGH IMMEDIATELY - so nothing is lost when the
// application is closed from the window's own chrome, which is the only way it
// can be closed here (there is deliberately no File > Quit).
//
// The SaveSettingsBeforeQuit event is kept because the ported tools push their
// option values from inside it rather than as they change; it is now raised at
// natural flush points (tool change, document close) rather than only at exit.
/// <summary>
/// The application's settings, stored in settings.sqlite.
/// </summary>
public sealed class SettingsManager : ISettingsService
{
	/// <summary>Raised when settings should be flushed to storage.</summary>
	public event EventHandler? SaveSettingsBeforeQuit;

	/// <summary>The settings folder - see <see cref="SettingsService.DefaultDirectory"/>.</summary>
	public string GetUserSettingsDirectory () => SettingsService.DefaultDirectory;

	/// <summary>
	/// Reads a setting, returning the default when it is absent or cannot be
	/// read as the requested type.
	/// </summary>
	public T GetSetting<T> (string key, T defaultValue) => SettingsService.Get (key, defaultValue);

	/// <summary>
	/// Writes a setting through to settings.sqlite. A null value removes it.
	/// </summary>
	public void PutSetting (string key, object value) => SettingsService.Set (key, value);

	/// <summary>
	/// Asks every subscriber to push its current values, so anything held only
	/// in memory reaches settings.sqlite.
	/// </summary>
	/// <remarks>
	/// Safe and cheap to call often: each PutSetting is a single upsert, and the
	/// store does nothing at all when the value has not changed.
	/// </remarks>
	public void DoSaveSettingsBeforeQuit ()
	{
		try {
			SaveSettingsBeforeQuit?.Invoke (this, EventArgs.Empty);
		} catch (Exception ex) {
			// Flushing settings must never take the application down.
			LoggingService.LogError ("Settings could not be saved", ex);
		}
	}
}
