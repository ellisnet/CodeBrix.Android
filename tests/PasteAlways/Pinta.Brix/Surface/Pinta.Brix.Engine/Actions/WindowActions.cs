//
// WindowActions.cs
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

/// <summary>The Window menu's commands.</summary>
public sealed class WindowActions
{
	/// <summary>Saves every open image.</summary>
	public Command SaveAll { get; }

	/// <summary>Closes every open image.</summary>
	public Command CloseAll { get; }

	/// <summary>Declares the commands.</summary>
	public WindowActions ()
	{
		SaveAll = new Command (
			"SaveAll",
			Translations.GetString ("Save All"),
			null,
			StandardIcons.DocumentSave,
			shortcuts: ["<Ctrl><Alt>A"]);

		CloseAll = new Command (
			"CloseAll",
			Translations.GetString ("Close All"),
			null,
			StandardIcons.WindowClose,
			shortcuts: ["<Primary><Shift>W"]);
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return SaveAll;
		yield return CloseAll;
	}
}
