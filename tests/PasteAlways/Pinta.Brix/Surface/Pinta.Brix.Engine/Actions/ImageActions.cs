//
// ImageActions.cs
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

/// <summary>The Image menu's commands.</summary>
public sealed class ImageActions
{
	/// <summary>Crops the image to the selection's bounds.</summary>
	public Command CropToSelection { get; }

	/// <summary>Crops away uniform borders.</summary>
	public Command AutoCrop { get; }

	/// <summary>Resizes the image, scaling its contents.</summary>
	public Command Resize { get; }

	/// <summary>Resizes the canvas without scaling its contents.</summary>
	public Command CanvasSize { get; }

	/// <summary>Flips the image left-to-right.</summary>
	public Command FlipHorizontal { get; }

	/// <summary>Flips the image top-to-bottom.</summary>
	public Command FlipVertical { get; }

	/// <summary>Rotates the image a quarter turn clockwise.</summary>
	public Command RotateCW { get; }

	/// <summary>Rotates the image a quarter turn counter-clockwise.</summary>
	public Command RotateCCW { get; }

	/// <summary>Rotates the image a half turn.</summary>
	public Command Rotate180 { get; }

	/// <summary>Merges every layer into one.</summary>
	public Command Flatten { get; }

	/// <summary>Declares the commands.</summary>
	public ImageActions ()
	{
		CropToSelection = new Command (
			"croptoselection",
			Translations.GetString ("Crop to Selection"),
			null,
			Icons.ImageCrop,
			shortcuts: ["<Primary><Shift>X"]);

		AutoCrop = new Command (
			"autocrop",
			Translations.GetString ("Auto Crop"),
			null,
			Icons.ImageCrop,
			shortcuts: ["<Ctrl><Alt>X"]);

		Resize = new Command (
			"resize",
			Translations.GetString ("Resize Image..."),
			null,
			Icons.ImageResize,
			shortcuts: ["<Primary>R"]);

		CanvasSize = new Command (
			"canvassize",
			Translations.GetString ("Resize Canvas..."),
			null,
			Icons.ImageResizeCanvas,
			shortcuts: ["<Primary><Shift>R"]);

		FlipHorizontal = new Command (
			"fliphorizontal",
			Translations.GetString ("Flip Horizontal"),
			null,
			Icons.ImageFlipHorizontal);

		FlipVertical = new Command (
			"flipvertical",
			Translations.GetString ("Flip Vertical"),
			null,
			Icons.ImageFlipVertical);

		RotateCW = new Command (
			"rotatecw",
			Translations.GetString ("Rotate 90° Clockwise"),
			null,
			Icons.ImageRotate90CW,
			shortcuts: ["<Primary>H"]);

		RotateCCW = new Command (
			"rotateccw",
			Translations.GetString ("Rotate 90° Counter-Clockwise"),
			null,
			Icons.ImageRotate90CCW,
			shortcuts: ["<Primary>G"]);

		Rotate180 = new Command (
			"rotate180",
			Translations.GetString ("Rotate 180°"),
			null,
			Icons.ImageRotate180,
			shortcuts: ["<Primary>J"]);

		Flatten = new Command (
			"flatten",
			Translations.GetString ("Flatten"),
			null,
			Icons.ImageFlatten,
			shortcuts: ["<Primary><Shift>F"]);
	}

	/// <summary>Every command in this group.</summary>
	public IEnumerable<Command> Commands ()
	{
		yield return CropToSelection;
		yield return AutoCrop;
		yield return Resize;
		yield return CanvasSize;
		yield return FlipHorizontal;
		yield return FlipVertical;
		yield return RotateCW;
		yield return RotateCCW;
		yield return Rotate180;
		yield return Flatten;
	}
}
