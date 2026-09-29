using System;
using System.Collections.Generic;
using CodeBrix.Plotter;

namespace CodeBrix.Platform.UI.AddIn.PlotterView.UIReqs.Support;

/// <summary>
/// The interaction sets a scenario can ask for by name, built as an application would build
/// them: a stock controller with ONE touch gesture rebound.
/// <para>
/// A chart on this panel is reached by finger only - there is no mouse and no wheel - and the
/// engine's controller holds a single touch-down slot, so a rebind replaces what was in it
/// rather than adding to it. That is exactly why these two exist: a plot that reports the
/// nearest data point under a finger and a plot that pans and pinches under one are two
/// different applications, and a scenario says which one it is about.
/// </para>
/// </summary>
public static class PlotterControllers
{
	/// <summary>A finger on the plot shows the tracker for the nearest data point.</summary>
	public const string TouchTracker = "touch-tracker";

	/// <summary>A finger on the plot pans it, and two fingers pinch it.</summary>
	public const string TouchPan = "touch-pan";

	/// <summary>The names a feature file may write, in the order they are declared.</summary>
	public static IReadOnlyCollection<string> Names => new[] { TouchTracker, TouchPan };

	/// <summary>Builds the controller a scenario named.</summary>
	/// <param name="name">The controller name, as a feature file writes it.</param>
	/// <returns>A new controller.</returns>
	/// <exception cref="NotSupportedException">No controller of that name is built here.</exception>
	public static IPlotController Build(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		var controller = new PlotController();

		if (string.Equals(name, TouchTracker, StringComparison.OrdinalIgnoreCase))
		{
			controller.BindTouchDown(PlotCommands.SnapTrackTouch);
			return controller;
		}

		if (string.Equals(name, TouchPan, StringComparison.OrdinalIgnoreCase))
		{
			controller.BindTouchDown(PlotCommands.PanZoomByTouch);
			return controller;
		}

		throw new NotSupportedException(
			$"There is no controller called \"{name}\". The controllers are: {string.Join(", ", Names)}.");
	}
}
