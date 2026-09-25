// FloatingDialogHost.cs
//
// A modeless floating panel with a title bar, OK/Cancel buttons and a
// draggable header, shown in a non-dimming Popup. Upstream's effect and
// adjustment dialogs are small utility WINDOWS floating over the canvas, so
// the live preview stays fully visible and interactive; ContentDialog dims
// and blocks the whole window, which defeats the preview. Every effect
// configuration dialog in the port goes through this host.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Pinta.Brix.Controls;

public static class FloatingDialogHost
{
	public static async Task<bool> ShowAsync (string title, UIElement content, XamlRoot xamlRoot, double maxWidth = 460)
	{
		TaskCompletionSource<bool> completion = new (TaskCreationOptions.RunContinuationsAsynchronously);

		//The panel is deliberately OPAQUE: translucent surfaces over a white
		//canvas wash out to unreadable (the menu flyouts demonstrated it).
		Border root = new () {
			Background = new SolidColorBrush (Windows.UI.Color.FromArgb (0xFF, 0x2B, 0x2B, 0x2B)),
			BorderBrush = new SolidColorBrush (Windows.UI.Color.FromArgb (0xFF, 0x55, 0x55, 0x55)),
			BorderThickness = new Thickness (1),
			CornerRadius = new CornerRadius (8),
			Padding = new Thickness (16),
			MinWidth = 320,
			MaxWidth = maxWidth,
			RequestedTheme = ElementTheme.Dark,
		};

		Popup popup = new () {
			XamlRoot = xamlRoot,
			IsLightDismissEnabled = false,
			Child = root,
		};

		void Complete (bool result)
		{
			popup.IsOpen = false;
			completion.TrySetResult (result);
		}

		TextBlock titleBlock = new () {
			Text = title,
			FontSize = 16,
			FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness (0, 0, 0, 12),
		};

		Button okButton = new () {
			Content = "OK",
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		if (Application.Current.Resources.TryGetValue ("AccentButtonStyle", out object? accent) && accent is Style accentStyle)
			okButton.Style = accentStyle;
		Button cancelButton = new () {
			Content = "Cancel",
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		okButton.Click += (_, _) => Complete (true);
		cancelButton.Click += (_, _) => Complete (false);

		Grid buttons = new () { ColumnSpacing = 8, Margin = new Thickness (0, 16, 0, 0) };
		buttons.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		buttons.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		Grid.SetColumn (okButton, 0);
		Grid.SetColumn (cancelButton, 1);
		buttons.Children.Add (okButton);
		buttons.Children.Add (cancelButton);

		StackPanel column = new ();
		column.Children.Add (titleBlock);
		column.Children.Add (content);
		column.Children.Add (buttons);
		root.Child = column;

		//Dragging the title moves the panel, exactly like a utility window -
		//the panel may be sitting on the part of the preview the user wants
		//to see.
		bool dragging = false;
		Windows.Foundation.Point dragStart = default;
		(double h, double v) dragOrigin = default;
		titleBlock.PointerPressed += (_, e) => {
			dragging = titleBlock.CapturePointer (e.Pointer);
			dragStart = e.GetCurrentPoint (null).Position;
			dragOrigin = (popup.HorizontalOffset, popup.VerticalOffset);
		};
		titleBlock.PointerMoved += (_, e) => {
			if (!dragging)
				return;
			var position = e.GetCurrentPoint (null).Position;
			popup.HorizontalOffset = dragOrigin.h + (position.X - dragStart.X);
			popup.VerticalOffset = dragOrigin.v + (position.Y - dragStart.Y);
		};
		titleBlock.PointerReleased += (_, e) => {
			dragging = false;
			titleBlock.ReleasePointerCapture (e.Pointer);
		};

		root.KeyDown += (_, e) => {
			if (e.Key == Windows.System.VirtualKey.Escape) {
				e.Handled = true;
				Complete (false);
			}
		};

		//Centre horizontally below the toolbars; the canvas stays visible
		//beneath and beside the panel.
		root.Measure (new Windows.Foundation.Size (double.PositiveInfinity, double.PositiveInfinity));
		popup.HorizontalOffset = Math.Max (0, (xamlRoot.Size.Width - root.DesiredSize.Width) / 2);
		popup.VerticalOffset = 110;
		popup.IsOpen = true;

		return await completion.Task;
	}
}
