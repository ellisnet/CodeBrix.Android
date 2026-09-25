//
// LayerActions.cs
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

/// <summary>The Layers menu's commands.</summary>
public sealed class LayerActions
{
	/// <summary>Adds an empty layer above the current one.</summary>
	public Command AddNewLayer { get; }

	/// <summary>Removes the current layer.</summary>
	public Command DeleteLayer { get; }

	/// <summary>Copies the current layer.</summary>
	public Command DuplicateLayer { get; }

	/// <summary>Merges the current layer into the one beneath it.</summary>
	public Command MergeLayerDown { get; }

	/// <summary>Adds a layer from an image file.</summary>
	public Command ImportFromFile { get; }

	/// <summary>Flips the current layer left-to-right.</summary>
	public Command FlipHorizontal { get; }

	/// <summary>Flips the current layer top-to-bottom.</summary>
	public Command FlipVertical { get; }

	/// <summary>Rotates and scales the current layer.</summary>
	public Command RotateZoom { get; }

	/// <summary>Moves the current layer up the stack.</summary>
	public Command MoveLayerUp { get; }

	/// <summary>Moves the current layer down the stack.</summary>
	public Command MoveLayerDown { get; }

	/// <summary>Edits the current layer's name, opacity, blend mode and visibility.</summary>
	public Command Properties { get; }

	/// <summary>Declares the commands.</summary>
	public LayerActions ()
	{
		AddNewLayer = new Command (
			"addnewlayer",
			Translations.GetString ("Add New Layer"),
			null,
			Icons.LayerNew,
			shortcuts: ["<Primary><Shift>N"]);

		DeleteLayer = new Command (
			"deletelayer",
			Translations.GetString ("Delete Layer"),
			null,
			Icons.LayerDelete,
			shortcuts: ["<Primary><Shift>Delete"]);

		DuplicateLayer = new Command (
			"duplicatelayer",
			Translations.GetString ("Duplicate Layer"),
			null,
			Icons.LayerDuplicate,
			shortcuts: ["<Primary><Shift>D"]);

		MergeLayerDown = new Command (
			"mergelayerdown",
			Translations.GetString ("Merge Layer Down"),
			null,
			Icons.LayerMergeDown,
			shortcuts: ["<Primary>M"]);

		ImportFromFile = new Command (
			"importfromfile",
			Translations.GetString ("Import from File..."),
			null,
			Icons.LayerImport);

		FlipHorizontal = new Command (
			"fliplayerhorizontal",
			Translations.GetString ("Flip Horizontal"),
			null,
			Icons.LayerFlipHorizontal);

		FlipVertical = new Command (
			"fliplayervertical",
			Translations.GetString ("Flip Vertical"),
			null,
			Icons.LayerFlipVertical);

		RotateZoom = new Command (
			"rotatezoom",
			Translations.GetString ("Rotate / Zoom Layer..."),
			null,
			Icons.LayerRotateZoom);

		MoveLayerUp = new Command (
			"movelayerup",
			Translations.GetString ("Move Layer Up"),
			null,
			StandardIcons.LayerMoveUp);

		MoveLayerDown = new Command (
			"movelayerdown",
			Translations.GetString ("Move Layer Down"),
			null,
			StandardIcons.LayerMoveDown);

		Properties = new Command (
			"properties",
			Translations.GetString ("Layer Properties..."),
			null,
			Icons.LayerProperties,
			shortcuts: ["F4"]);
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return AddNewLayer;
		yield return DeleteLayer;
		yield return DuplicateLayer;
		yield return MergeLayerDown;
		yield return ImportFromFile;
		yield return FlipHorizontal;
		yield return FlipVertical;
		yield return RotateZoom;
		yield return MoveLayerUp;
		yield return MoveLayerDown;
		yield return Properties;
	}
}
