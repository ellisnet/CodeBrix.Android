// CoreTools.cs
//
// Direct registration of the default tools and brushes, replacing the
// upstream add-in-based registration. Shape tools and the text tool are
// registered by a later port phase.

using System;
using Pinta.Brix.Engine;

namespace Pinta.Brix.Tools;

public static class CoreTools
{
	public static void Register (IServiceProvider services)
	{
		PintaCore.PaintBrushes.AddPaintBrush (new Brushes.CircleBrush ());
		PintaCore.PaintBrushes.AddPaintBrush (new Brushes.GridBrush ());
		PintaCore.PaintBrushes.AddPaintBrush (new Brushes.PlainBrush (PintaCore.Workspace));
		PintaCore.PaintBrushes.AddPaintBrush (new Brushes.SplatterBrush (services.GetService<ISettingsService> ()));
		PintaCore.PaintBrushes.AddPaintBrush (new Brushes.SlashBrush (services.GetService<ISettingsService> (), services.GetService<IWorkspaceService> ()));
		PintaCore.PaintBrushes.AddPaintBrush (new Brushes.SquaresBrush ());

		PintaCore.Tools.AddTool (new MoveSelectedTool (services));
		PintaCore.Tools.AddTool (new MoveSelectionTool (services));
		PintaCore.Tools.AddTool (new ZoomTool (services));
		PintaCore.Tools.AddTool (new PanTool (services));
		PintaCore.Tools.AddTool (new RectangleSelectTool (services));
		PintaCore.Tools.AddTool (new EllipseSelectTool (services));
		PintaCore.Tools.AddTool (new LassoSelectTool (services));
		PintaCore.Tools.AddTool (new MagicWandTool (services));
		PintaCore.Tools.AddTool (new PaintBrushTool (services));
		PintaCore.Tools.AddTool (new PencilTool (services));
		PintaCore.Tools.AddTool (new EraserTool (services));
		PintaCore.Tools.AddTool (new PaintBucketTool (services));
		PintaCore.Tools.AddTool (new GradientTool (services));
		PintaCore.Tools.AddTool (new ColorPickerTool (services));
		PintaCore.Tools.AddTool (new TextTool (services));
		PintaCore.Tools.AddTool (new LineCurveTool (services));
		PintaCore.Tools.AddTool (new RectangleTool (services));
		PintaCore.Tools.AddTool (new RoundedRectangleTool (services));
		PintaCore.Tools.AddTool (new EllipseTool (services));
		PintaCore.Tools.AddTool (new FreeformShapeTool (services));
		PintaCore.Tools.AddTool (new CloneStampTool (services));
		PintaCore.Tools.AddTool (new RecolorTool (services));
	}
}
