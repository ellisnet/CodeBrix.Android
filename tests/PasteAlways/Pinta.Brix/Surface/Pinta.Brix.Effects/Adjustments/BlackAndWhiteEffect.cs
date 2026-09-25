/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Pinta.Brix.Engine.Drawing;
using Pinta.Brix.Engine;

//was previously: namespace Pinta.Effects;
namespace Pinta.Brix.Effects;

public sealed class BlackAndWhiteEffect : BaseEffect
{
	static readonly UnaryPixelOp desaturate = new UnaryPixelOps.Desaturate ();

	public BlackAndWhiteEffect (IServiceProvider _) { }

	public sealed override bool IsTileable
		=> true;

	public override string Icon
		=> Icons.AdjustmentsBlackAndWhite;

	public override string Name
		=> Translations.GetString ("Black and White");

	public override string AdjustmentMenuKey
		=> "G";

	protected override void Render (ImageSurface source, ImageSurface destination, RectangleI roi)
		=> desaturate.Apply (destination, source, roi);
}
