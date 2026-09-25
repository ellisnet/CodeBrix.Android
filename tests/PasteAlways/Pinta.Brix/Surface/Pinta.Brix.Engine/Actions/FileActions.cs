//
// FileActions.cs
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

/// <summary>The File menu's commands.</summary>
public sealed class FileActions
{
	/// <summary>Creates a new image.</summary>
	public Command New { get; }

	/// <summary>Creates a new image from a screenshot.</summary>
	public Command NewScreenshot { get; }

	/// <summary>Opens an existing image.</summary>
	public Command Open { get; }

	/// <summary>Closes the active image.</summary>
	public Command Close { get; }

	/// <summary>Saves the active image.</summary>
	public Command Save { get; }

	/// <summary>Saves the active image under a new name.</summary>
	public Command SaveAs { get; }

	/// <summary>Declares the commands.</summary>
	public FileActions ()
	{
		New = new Command (
			"new",
			Translations.GetString ("New..."),
			null,
			StandardIcons.DocumentNew,
			shortcuts: ["<Primary>N"]
		) { ShortLabel = Translations.GetString ("New") };

		NewScreenshot = new Command (
			"NewScreenshot",
			Translations.GetString ("New Screenshot..."),
			null,
			StandardIcons.ImageGeneric);

		Open = new Command (
			"open",
			Translations.GetString ("Open..."),
			null,
			StandardIcons.DocumentOpen,
			shortcuts: ["<Primary>O"]
		) { ShortLabel = Translations.GetString ("Open") };

		Close = new Command (
			"close",
			Translations.GetString ("Close"),
			null,
			StandardIcons.WindowClose,
			shortcuts: ["<Primary>W"]);

		Save = new Command (
			"save",
			Translations.GetString ("Save"),
			null,
			StandardIcons.DocumentSave,
			shortcuts: ["<Primary>S"]);

		SaveAs = new Command (
			"saveAs",
			Translations.GetString ("Save As..."),
			null,
			StandardIcons.DocumentSaveAs,
			shortcuts: ["<Primary><Shift>S"]);
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return New;
		yield return NewScreenshot;
		yield return Open;
		yield return Close;
		yield return Save;
		yield return SaveAs;
	}
}
