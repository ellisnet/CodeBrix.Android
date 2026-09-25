//
// ViewActions.cs
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

using System.Collections.Generic;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

/// <summary>The View menu's commands.</summary>
public sealed class ViewActions
{
	/// <summary>Zooms in one step.</summary>
	public Command ZoomIn { get; }

	/// <summary>Zooms out one step.</summary>
	public Command ZoomOut { get; }

	/// <summary>Zooms so the whole image fits the viewport.</summary>
	public Command ZoomToWindow { get; }

	/// <summary>Zooms so the selection fills the viewport.</summary>
	public Command ZoomToSelection { get; }

	/// <summary>Returns to 100% zoom.</summary>
	public Command ActualSize { get; }

	/// <summary>Shows or hides the in-app toolbar row.</summary>
	public ToggleCommand ToolBar { get; }

	/// <summary>Shows or hides the document tab strip.</summary>
	public ToggleCommand ImageTabs { get; }

	/// <summary>Shows or hides the docked pads.</summary>
	public ToggleCommand ToolWindows { get; }

	/// <summary>Edits the canvas grid's spacing.</summary>
	public Command EditCanvasGrid { get; }

	/// <summary>Shows or hides the menu bar.</summary>
	public ToggleCommand MenuBar { get; }

	/// <summary>Shows or hides the status bar.</summary>
	public ToggleCommand StatusBar { get; }

	/// <summary>Shows or hides the tool box.</summary>
	public ToggleCommand ToolBox { get; }

	/// <summary>Shows or hides the rulers.</summary>
	public ToggleCommand Rulers { get; }

	/// <summary>Fills the screen with the application.</summary>
	public Command Fullscreen { get; }

	/// <summary>Declares the commands.</summary>
	public ViewActions ()
	{
		ZoomIn = new Command (
			"ZoomIn",
			Translations.GetString ("Zoom In"),
			null,
			StandardIcons.ValueIncrease,
			shortcuts: ["<Primary>plus", "<Primary>equal", "equal", "<Primary>KP_Add", "KP_Add"]);

		ZoomOut = new Command (
			"ZoomOut",
			Translations.GetString ("Zoom Out"),
			null,
			StandardIcons.ValueDecrease,
			shortcuts: ["<Primary>minus", "<Primary>underscore", "minus", "<Primary>KP_Subtract", "KP_Subtract"]);

		ZoomToWindow = new Command (
			"ZoomToWindow",
			Translations.GetString ("Best Fit"),
			null,
			StandardIcons.ZoomFitBest,
			shortcuts: ["<Primary>B"]);

		ZoomToSelection = new Command (
			"ZoomToSelection",
			Translations.GetString ("Zoom to Selection"),
			null,
			Icons.ViewZoomSelection);

		ActualSize = new Command (
			"ActualSize",
			Translations.GetString ("Normal Size"),
			null,
			StandardIcons.ZoomOriginal,
			shortcuts: ["<Primary>0", "<Primary><Shift>A"]);

		ToolBar = new ToggleCommand (
			"Toolbar",
			Translations.GetString ("Toolbar"),
			null,
			null);

		ImageTabs = new ToggleCommand (
			"ImageTabs",
			Translations.GetString ("Image Tabs"),
			null,
			null);

		ToolWindows = new ToggleCommand (
			"ToolWindows",
			Translations.GetString ("Tool Windows"),
			null,
			null,
			shortcuts: ["F12"]);

		EditCanvasGrid = new Command (
			"EditCanvasGrid",
			Translations.GetString ("Canvas Grid..."),
			null,
			Icons.ViewGrid);

		MenuBar = new ToggleCommand (
			"MenuBar",
			Translations.GetString ("Menu Bar"),
			null,
			null);

		StatusBar = new ToggleCommand (
			"Statusbar",
			Translations.GetString ("Status Bar"),
			null,
			null);

		ToolBox = new ToggleCommand (
			"ToolBox",
			Translations.GetString ("Tool Box"),
			null,
			null);

		Rulers = new ToggleCommand (
			"Rulers",
			Translations.GetString ("Rulers"),
			null,
			Icons.ViewRulers);

		Fullscreen = new Command (
			"Fullscreen",
			Translations.GetString ("Fullscreen"),
			null,
			StandardIcons.ViewFullscreen,
			shortcuts: ["F11"]);

		// Upstream starts every one of these visible except the rulers.
		ToolBar.Value = true;
		ImageTabs.Value = true;
		ToolWindows.Value = true;
		MenuBar.Value = true;
		StatusBar.Value = true;
		ToolBox.Value = true;
		Rulers.Value = false;
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return ZoomIn;
		yield return ZoomOut;
		yield return ZoomToWindow;
		yield return ZoomToSelection;
		yield return ActualSize;
		yield return ToolBar;
		yield return ImageTabs;
		yield return ToolWindows;
		yield return EditCanvasGrid;
		yield return MenuBar;
		yield return StatusBar;
		yield return ToolBox;
		yield return Rulers;
		yield return Fullscreen;
	}
}
