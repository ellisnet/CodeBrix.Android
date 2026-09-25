/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Krzysztof Marecki <marecki.krzysztof@gmail.com>         //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using Pinta.Brix.Engine.Drawing;
using Pinta.Brix.Engine;

//was previously: namespace Pinta.Effects;
namespace Pinta.Brix.Effects;

public sealed class LevelsEffect : BaseEffect
{
	public sealed override bool IsTileable => true;

	public override string Icon => Icons.AdjustmentsLevels;

	public override string Name => Translations.GetString ("Levels");

	public override bool IsConfigurable => true;

	public override string AdjustmentMenuKey => "L";

	public override string AdjustmentMenuKeyModifiers => "<Primary>";

	public LevelsData Data => (LevelsData) EffectData!;  // NRT - Set in constructor

	private readonly IChromeService chrome;
	private readonly IPaletteService palette;
	private readonly IWorkspaceService workspace;

	public LevelsEffect (IServiceProvider services)
	{
		chrome = services.GetService<IChromeService> ();
		palette = services.GetService<IPaletteService> ();
		workspace = services.GetService<IWorkspaceService> ();

		EffectData = new LevelsData ();
	}

	public override Task<bool> LaunchConfiguration ()
	{
		// Pinta.Brix note: upstream constructed the custom LevelsDialog
		// directly; this library stays UI-free, so the dialog request goes
		// through the chrome seam and the UI layer routes it to the ported
		// LevelsDialog by effect type.
		//was previously:
		//	// TODO: Delegate `EffectData` changes to event handlers or similar
		//	using LevelsDialog dialog = LevelsDialog.New (chrome, palette, workspace, Data);
		//	dialog.Title = Name;
		//	dialog.IconName = Icon;
		//
		//	Gtk.ResponseType response = await dialog.RunAsync ();
		//
		//	dialog.Destroy ();
		//
		//	return Gtk.ResponseType.Ok == response;
		return chrome.LaunchSimpleEffectDialog (this, workspace);
	}

	public override void Render (ImageSurface src, ImageSurface dest, ReadOnlySpan<RectangleI> rois)
		=> Data.Levels.Apply (dest, src, rois);
}

public sealed class LevelsData : EffectData
{
	public UnaryPixelOps.Level Levels { get; set; }

	public LevelsData ()
	{
		Levels = new UnaryPixelOps.Level ();
	}

	public override LevelsData Clone ()
		=> new () { Levels = (UnaryPixelOps.Level) Levels.Clone () };
}
