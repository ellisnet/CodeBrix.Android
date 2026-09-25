//
// ActionManager.cs
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

// Pinta.Brix note: upstream's ActionManager also built the GTK menu and
// toolbar from these groups. Here it is a pure declaration model - the UI
// layer walks it and builds a MenuBar - so the groups take no services and
// can be constructed (and tested) headlessly.
/// <summary>
/// Every command the application exposes, grouped by the menu it belongs to.
/// </summary>
public sealed class ActionManager
{
	/// <summary>Application-level commands (About, Keyboard Shortcuts).</summary>
	public AppActions App { get; }

	/// <summary>The File menu.</summary>
	public FileActions File { get; }

	/// <summary>The Edit menu.</summary>
	public EditActions Edit { get; }

	/// <summary>The View menu.</summary>
	public ViewActions View { get; }

	/// <summary>The Image menu.</summary>
	public ImageActions Image { get; }

	/// <summary>The Layers menu.</summary>
	public LayerActions Layers { get; }

	/// <summary>The Window menu.</summary>
	public WindowActions Window { get; }

	/// <summary>The Help menu.</summary>
	public HelpActions Help { get; }

	/// <summary>Creates the action model.</summary>
	public ActionManager ()
	{
		App = new AppActions ();
		File = new FileActions ();
		Edit = new EditActions ();
		View = new ViewActions ();
		Image = new ImageActions ();
		Layers = new LayerActions ();
		Window = new WindowActions ();
		Help = new HelpActions ();
	}

	/// <summary>
	/// Every command in every group, for callers that need to walk the whole
	/// model - the keyboard-accelerator pass and the shortcuts dialog.
	/// </summary>
	public IEnumerable<Command> AllCommands ()
	{
		foreach (Command command in App.Commands ()) yield return command;
		foreach (Command command in File.Commands ()) yield return command;
		foreach (Command command in Edit.Commands ()) yield return command;
		foreach (Command command in View.Commands ()) yield return command;
		foreach (Command command in Image.Commands ()) yield return command;
		foreach (Command command in Layers.Commands ()) yield return command;
		foreach (Command command in Window.Commands ()) yield return command;
		foreach (Command command in Help.Commands ()) yield return command;
	}
}
