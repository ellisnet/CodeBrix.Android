using System;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public sealed class PaletteLoadException : Exception
{
	public string FileName { get; }
	internal PaletteLoadException (string fileName, string message)
		: base (message)
	{
		FileName = fileName;
	}
}
