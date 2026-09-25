//
// AppActions.cs
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

/// <summary>Application-level commands.</summary>
public sealed class AppActions
{
	// Pinta.Brix note: upstream also declares Exit ("quit", <Primary>Q).
	// It is DELIBERATELY ABSENT here. Window lifecycle belongs to the OS
	// chrome, and the Frame Buffer head is chrome-less on purpose - shipping
	// Quit would mean detecting kiosk mode and suppressing it, and a missed
	// suppression is a kiosk escape. Closing the window remains the way out
	// on heads that have chrome; the unsaved-changes prompt runs there.

	/// <summary>Shows the about box.</summary>
	public Command About { get; }

	/// <summary>Shows the keyboard-shortcuts reference.</summary>
	public Command KeyboardShortcuts { get; }

	/// <summary>Declares the commands.</summary>
	public AppActions ()
	{
		About = new Command (
			"about",
			Translations.GetString ("About"),
			null,
			StandardIcons.HelpAbout);

		KeyboardShortcuts = new Command (
			"keyboardshortcuts",
			Translations.GetString ("Keyboard Shortcuts"),
			null,
			StandardIcons.KeyboardShortcuts,
			shortcuts: ["<Primary>comma"]);
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return About;
		yield return KeyboardShortcuts;
	}
}
