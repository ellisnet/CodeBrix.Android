// ICanvasView.cs
//
// Pinta.Brix replacement for the toolkit canvas widget references the engine
// held (the upstream workspace exposed raw widgets set by the shell). The UI
// layer's canvas control implements this so the engine can set cursors and
// request redraws without referencing any UI framework.

namespace Pinta.Brix.Engine;

public interface ICanvasView
{
	ToolCursor? Cursor { get; set; }

	/// <summary>Request a full repaint of the canvas view.</summary>
	void QueueDraw ();

	/// <summary>Move keyboard focus to the canvas.</summary>
	void GrabFocus ();
}

/// <summary>
/// The scrollable view hosting a document's canvas: viewport metrics and the
/// scroll position, in view (zoomed) coordinates.
/// </summary>
public interface ICanvasScrollView
{
	Size ViewportSize { get; }

	PointD ScrollOffset { get; set; }

	/// <summary>
	/// Ensures scroll extents reflect the current view size before scroll
	/// offsets are adjusted (replaces the upstream main-loop pump).
	/// </summary>
	void UpdateLayout ();

	/// <summary>Move keyboard focus to the canvas view. Returns whether focus was gained.</summary>
	bool GrabFocus ();
}
