//
// EditActions.cs
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

/// <summary>The Edit menu's commands.</summary>
public sealed class EditActions
{
	/// <summary>Undoes the last history item.</summary>
	public Command Undo { get; }

	/// <summary>Redoes the next history item.</summary>
	public Command Redo { get; }

	/// <summary>Cuts the selection to the clipboard.</summary>
	public Command Cut { get; }

	/// <summary>Copies the selection to the clipboard.</summary>
	public Command Copy { get; }

	/// <summary>Copies the flattened selection to the clipboard.</summary>
	public Command CopyMerged { get; }

	/// <summary>Pastes the clipboard into the current layer.</summary>
	public Command Paste { get; }

	/// <summary>Pastes the clipboard into a new layer.</summary>
	public Command PasteIntoNewLayer { get; }

	/// <summary>Pastes the clipboard into a new image.</summary>
	public Command PasteIntoNewImage { get; }

	/// <summary>Erases the selected pixels.</summary>
	public Command EraseSelection { get; }

	/// <summary>Fills the selection with the primary colour.</summary>
	public Command FillSelection { get; }

	/// <summary>Inverts the selection.</summary>
	public Command InvertSelection { get; }

	/// <summary>Grows or shrinks the selection.</summary>
	public Command OffsetSelection { get; }

	/// <summary>Selects the whole canvas.</summary>
	public Command SelectAll { get; }

	/// <summary>Clears the selection.</summary>
	public Command Deselect { get; }

	/// <summary>Loads a palette from a file.</summary>
	public Command LoadPalette { get; }

	/// <summary>Saves the palette to a file.</summary>
	public Command SavePalette { get; }

	/// <summary>Restores the default palette.</summary>
	public Command ResetPalette { get; }

	/// <summary>Changes how many colours the palette holds.</summary>
	public Command ResizePalette { get; }

	/// <summary>Declares the commands.</summary>
	public EditActions ()
	{
		Undo = new Command (
			"undo",
			Translations.GetString ("Undo"),
			null,
			StandardIcons.EditUndo,
			shortcuts: ["<Primary>Z"]);

		Redo = new Command (
			"redo",
			Translations.GetString ("Redo"),
			null,
			StandardIcons.EditRedo,
			shortcuts: ["<Primary><Shift>Z", "<Ctrl>Y"]);

		Cut = new Command (
			"cut",
			Translations.GetString ("Cut"),
			null,
			StandardIcons.EditCut,
			shortcuts: ["<Primary>X"]);

		Copy = new Command (
			"copy",
			Translations.GetString ("Copy"),
			null,
			StandardIcons.EditCopy,
			shortcuts: ["<Primary>C"]);

		CopyMerged = new Command (
			"copymerged",
			Translations.GetString ("Copy Merged"),
			null,
			StandardIcons.EditCopy,
			shortcuts: ["<Primary><Shift>C"]);

		Paste = new Command (
			"paste",
			Translations.GetString ("Paste"),
			null,
			StandardIcons.EditPaste,
			shortcuts: ["<Primary>V"]);

		PasteIntoNewLayer = new Command (
			"pasteintonewlayer",
			Translations.GetString ("Paste Into New Layer"),
			null,
			StandardIcons.EditPaste,
			shortcuts: ["<Primary><Shift>V"]);

		PasteIntoNewImage = new Command (
			"pasteintonewimage",
			Translations.GetString ("Paste Into New Image"),
			null,
			StandardIcons.EditPaste,
			shortcuts: ["<Shift>V", "<Primary><Alt>V"]);

		EraseSelection = new Command (
			"eraseselection",
			Translations.GetString ("Erase Selection"),
			null,
			Icons.EditSelectionErase,
			shortcuts: ["Delete"]);

		FillSelection = new Command (
			"fillselection",
			Translations.GetString ("Fill Selection"),
			null,
			Icons.EditSelectionFill,
			shortcuts: ["BackSpace"]);

		InvertSelection = new Command (
			"invertselection",
			Translations.GetString ("Invert Selection"),
			null,
			Icons.EditSelectionInvert,
			shortcuts: ["<Primary>I"]);

		OffsetSelection = new Command (
			"offsetselection",
			Translations.GetString ("Offset Selection"),
			null,
			Icons.EditSelectionOffset,
			shortcuts: ["<Primary><Shift>O"]);

		SelectAll = new Command (
			"selectall",
			Translations.GetString ("Select All"),
			null,
			StandardIcons.EditSelectAll,
			shortcuts: ["<Primary>A"]);

		Deselect = new Command (
			"deselect",
			Translations.GetString ("Deselect All"),
			null,
			Icons.EditSelectionNone,
			shortcuts: ["<Primary><Shift>A", "<Ctrl>D"]);

		LoadPalette = new Command (
			"loadpalette",
			Translations.GetString ("Open..."),
			null,
			StandardIcons.DocumentOpen);

		SavePalette = new Command (
			"savepalette",
			Translations.GetString ("Save As..."),
			null,
			StandardIcons.DocumentSaveAs);

		ResetPalette = new Command (
			"resetpalette",
			Translations.GetString ("Reset to Default"),
			null,
			StandardIcons.DocumentRevert);

		ResizePalette = new Command (
			"resizepalette",
			Translations.GetString ("Set Number of Colors"),
			null,
			StandardIcons.ImageGeneric);
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return Undo;
		yield return Redo;
		yield return Cut;
		yield return Copy;
		yield return CopyMerged;
		yield return Paste;
		yield return PasteIntoNewLayer;
		yield return PasteIntoNewImage;
		yield return EraseSelection;
		yield return FillSelection;
		yield return InvertSelection;
		yield return OffsetSelection;
		yield return SelectAll;
		yield return Deselect;
		yield return LoadPalette;
		yield return SavePalette;
		yield return ResetPalette;
		yield return ResizePalette;
	}
}
