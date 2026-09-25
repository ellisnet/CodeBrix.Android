// ThumbSplitter.cs
//
// A minimal thumb-drag splitter bar. The platform ships no GridSplitter, and
// upstream's pads sit in Gtk.Paned containers the user can resize - this
// control supplies that: a thin bar that reports drag deltas, leaving the
// actual resize policy (column width, row height) to its owner.

using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Pinta.Brix.Controls;

public sealed class ThumbSplitter : Border
{
	private bool dragging;
	private Windows.Foundation.Point last_position;

	/// <summary>
	/// Raised while dragging with the movement since the last report, in the
	/// axis the splitter resizes: X for a vertical bar, Y for a horizontal one.
	/// </summary>
	public event EventHandler<double>? DragDelta;

	public ThumbSplitter (Orientation orientation)
	{
		Orientation = orientation;

		Background = new SolidColorBrush (Windows.UI.Color.FromArgb (0x30, 0x80, 0x80, 0x80));

		if (orientation == Orientation.Vertical)
			Width = 6;
		else
			Height = 6;

		ProtectedCursor = InputSystemCursor.Create (
			orientation == Orientation.Vertical
			? InputSystemCursorShape.SizeWestEast
			: InputSystemCursorShape.SizeNorthSouth);

		PointerPressed += OnPointerPressedHandler;
		PointerMoved += OnPointerMovedHandler;
		PointerReleased += OnPointerReleasedHandler;
	}

	/// <summary>Vertical = a vertical bar that resizes horizontally.</summary>
	public Orientation Orientation { get; }

	private void OnPointerPressedHandler (object sender, PointerRoutedEventArgs e)
	{
		dragging = CapturePointer (e.Pointer);
		last_position = e.GetCurrentPoint (null).Position;
		e.Handled = true;
	}

	private void OnPointerMovedHandler (object sender, PointerRoutedEventArgs e)
	{
		if (!dragging)
			return;

		var position = e.GetCurrentPoint (null).Position;
		double delta = Orientation == Orientation.Vertical
			? position.X - last_position.X
			: position.Y - last_position.Y;
		last_position = position;

		if (delta != 0)
			DragDelta?.Invoke (this, delta);

		e.Handled = true;
	}

	private void OnPointerReleasedHandler (object sender, PointerRoutedEventArgs e)
	{
		dragging = false;
		ReleasePointerCapture (e.Pointer);
		e.Handled = true;
	}
}
