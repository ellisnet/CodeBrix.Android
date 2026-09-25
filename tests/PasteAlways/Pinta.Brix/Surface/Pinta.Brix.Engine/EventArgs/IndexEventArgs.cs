/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public sealed class IndexEventArgs : EventArgs
{

	public int Index { get; }

	public IndexEventArgs (int i)
	{
		Index = i;
	}
}

