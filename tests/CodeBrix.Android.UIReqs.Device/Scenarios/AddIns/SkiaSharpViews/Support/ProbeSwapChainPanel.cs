using CodeBrix.Platform.UI.Core.UIReqs.Support;
using SkiaSharp.Views.Windows;

namespace CodeBrix.Platform.UI.AddIn.SkiaSharpViews.UIReqs.Support;

/// <summary>
/// An SKSwapChainPanel with a PaintSurface handler attached, so that a scenario can state that
/// the handler was never called. On this head the element is a placeholder that exists only so
/// that code written for a GPU swap chain compiles: its constructor refuses to run at all
/// unless the application has opted out, and once it has, the element paints nothing. Both of
/// those are requirements, so both are stated.
/// </summary>
public sealed class ProbeSwapChainPanel : SKSwapChainPanel
{
	/// <summary>The name a paint is recorded under, which is the event the element raises.</summary>
	public const string PaintSurfaceEventName = "PaintSurface";

	/// <summary>
	/// Builds the panel. This throws unless the scenario has already opted out of the refusal.
	/// </summary>
	public ProbeSwapChainPanel() => PaintSurface += OnFixturePaintSurface;

	private void OnFixturePaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
	{
		if (!string.IsNullOrEmpty(Name))
		{
			EventRecorder.Record(Name, PaintSurfaceEventName);
		}
	}
}
