//
// PaletteManager.cs
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pinta.Brix.Engine.Drawing;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public interface IPaletteService
{
	Color PrimaryColor { get; set; }
	Color SecondaryColor { get; set; }
	Palette CurrentPalette { get; }
	int MaxRecentlyUsedColor { get; }
	ReadOnlyCollection<Color> RecentlyUsedColors { get; }
	void SetColor (bool setPrimary, Color color, bool addToRecent = true);
	event EventHandler? PrimaryColorChanged;
	event EventHandler? SecondaryColorChanged;
	event EventHandler? RecentColorsChanged;
}

public sealed class PaletteManager : IPaletteService
{
	private Color primary;
	private Color secondary;

	private const int MAX_RECENT_COLORS = 10;
	// Pinta.Brix note: upstream kept the working palette in a palette.txt file
	// beside settings.xml. Everything persisted now lives in settings.sqlite, so
	// the palette is a setting like any other - stored as its list of colours.
	// (Edit > Palette > Save As still writes a real file, but only where the
	// user asks for one: that is an export, not application state.)
	private const string CURRENT_PALETTE_KEY = "current-palette";

	private readonly List<Color> recently_used;

	public Color PrimaryColor {
		get => primary;
		set => SetColor (true, value, true);
	}

	public int MaxRecentlyUsedColor => MAX_RECENT_COLORS;

	public ReadOnlyCollection<Color> RecentlyUsedColors { get; }

	public Color SecondaryColor {
		get => secondary;
		set => SetColor (false, value, true);
	}

	public Palette CurrentPalette { get; }

	private readonly ISettingsService settings;
	private readonly PaletteFormatManager palette_formats;
	public PaletteManager (
		ISettingsService settings,
		PaletteFormatManager paletteFormats)
	{
		List<Color> recentlyUsed = new (MAX_RECENT_COLORS);

		recently_used = recentlyUsed;
		RecentlyUsedColors = new ReadOnlyCollection<Color> (recentlyUsed);

		this.settings = settings;
		this.palette_formats = paletteFormats;

		CurrentPalette = PaletteHelper.CreateDefault ();

		// This depends on `palette_formats` and `CurrentPalette` having a value
		// Can this call be moved out of this constructor?
		PopulateSavedPalette (paletteFormats);

		PopulateRecentlyUsedColors ();

		// Colours write through as they change rather than waiting for a flush:
		// picking a colour has to survive the application closing, and the only
		// way to close it is the window's own chrome.
		PrimaryColorChanged += (_, _) => SaveColors ();
		SecondaryColorChanged += (_, _) => SaveColors ();
		RecentColorsChanged += (_, _) => SaveColors ();
		CurrentPalette.PaletteChanged += (_, _) => SaveCurrentPalette ();
	}

	public void SwapColors ()
	{
		(SecondaryColor, PrimaryColor) = (PrimaryColor, SecondaryColor);
	}

	// This allows callers to bypass affecting the recently used list
	public void SetColor (bool setPrimary, Color color, bool addToRecent = true)
	{
		if (setPrimary && !primary.Equals (color)) {
			primary = color;

			OnPrimaryColorChanged ();
		} else if (!setPrimary && !secondary.Equals (color)) {
			secondary = color;

			OnSecondaryColorChanged ();
		}

		if (addToRecent)
			AddRecentlyUsedColor (color);
	}

	// The most recently used color is at index 0.
	private void AddRecentlyUsedColor (Color color)
	{
		// The color is already in the recently used list
		if (recently_used.Contains (color)) {
			// If it's already at the back, nothing to do
			if (recently_used[0].Equals (color))
				return;

			// Move it to the front
			recently_used.Remove (color);
			recently_used.Insert (0, color);

			OnRecentColorsChanged ();
			return;
		}

		// Color needs to be added to the list
		if (recently_used.Count == MAX_RECENT_COLORS)
			recently_used.RemoveAt (MAX_RECENT_COLORS - 1);

		recently_used.Insert (0, color);

		OnRecentColorsChanged ();
	}

	private void PopulateSavedPalette (PaletteFormatManager paletteFormats)
	{
		string[] saved = settings.GetSetting (CURRENT_PALETTE_KEY, System.Array.Empty<string> ());

		if (saved.Length == 0)
			return;

		List<Color> colors = new (saved.Length);

		foreach (string hex in saved) {
			Color? color = Color.FromHex (hex);
			if (color is not null)
				colors.Add (color.Value);
		}

		if (colors.Count > 0)
			CurrentPalette.Load (colors);
	}

	private void PopulateRecentlyUsedColors ()
	{
		// Pinta.Brix note: upstream stored these as BGRA hex through an API that
		// is [Obsolete], which is why this method used to carry a CS0618
		// pragma. settings.sqlite serialises values as JSON, so the colours are
		// stored in the engine's own RGBA hex form and the pragma is gone.
		string primaryColor = settings.GetSetting (SettingNames.PRIMARY_COLOR, string.Empty);
		string secondaryColor = settings.GetSetting (SettingNames.SECONDARY_COLOR, string.Empty);

		SetColor (
			setPrimary: true,
			Color.FromHex (primaryColor) ?? Color.Black,
			addToRecent: false);

		SetColor (
			setPrimary: false,
			Color.FromHex (secondaryColor) ?? Color.White,
			addToRecent: false);

		// Recently used palette
		string[] savedColors = settings.GetSetting (SettingNames.RECENT_COLORS, System.Array.Empty<string> ());

		foreach (string hexColor in savedColors) {
			Color? color = Color.FromHex (hexColor);
			if (color is not null)
				recently_used.Add (color.Value);
		}

		// Fill in with default color if not enough saved
		int more_colors = MAX_RECENT_COLORS - recently_used.Count;

		if (more_colors > 0)
			recently_used.AddRange (Enumerable.Repeat (new Color (.9, .9, .9), more_colors));
	}

	private void SaveCurrentPalette () =>
		settings.PutSetting (CURRENT_PALETTE_KEY, CurrentPalette.Colors.Select (c => c.ToHex ()).ToArray ());

	private void SaveColors ()
	{
		// Primary / Secondary colors
		settings.PutSetting (SettingNames.PRIMARY_COLOR, PrimaryColor.ToHex ());
		settings.PutSetting (SettingNames.SECONDARY_COLOR, SecondaryColor.ToHex ());

		// Recently used palette
		settings.PutSetting (SettingNames.RECENT_COLORS, recently_used.Select (c => c.ToHex ()).ToArray ());
	}

	private void OnPrimaryColorChanged ()
	{
		PrimaryColorChanged?.Invoke (this, EventArgs.Empty);
	}

	private void OnRecentColorsChanged () => RecentColorsChanged?.Invoke (this, EventArgs.Empty);

	private void OnSecondaryColorChanged ()
	{
		SecondaryColorChanged?.Invoke (this, EventArgs.Empty);
	}

	public event EventHandler? PrimaryColorChanged;
	public event EventHandler? SecondaryColorChanged;
	public event EventHandler? RecentColorsChanged;
}
