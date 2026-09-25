// ContentProgressDialog.cs
//
// The engine's progress dialog, backed by a ContentDialog. This replaces the
// no-op stand-in the port shipped with: a long effect now reports progress and
// can be cancelled, which is the behaviour LivePreviewManager already expects
// (it listens for Canceled and rolls the render back).

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pinta.Brix.Engine;

namespace Pinta.Brix.Controls;

/// <summary>A progress dialog over <see cref="ContentDialog"/>.</summary>
public sealed class ContentProgressDialog : IProgressDialog
{
	private readonly Func<XamlRoot?> xaml_root_getter;
	private readonly ProgressBar progress_bar;
	private readonly TextBlock text_block;
	private ContentDialog? dialog;
	private bool showing;

	/// <summary>
	/// Creates the dialog.
	/// </summary>
	/// <param name="xamlRootGetter">
	/// Supplies the XamlRoot to attach to. It is a callback rather than a value
	/// because the dialog is constructed before the page has a root.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="xamlRootGetter"/> is null.</exception>
	public ContentProgressDialog (Func<XamlRoot?> xamlRootGetter)
	{
		xaml_root_getter = xamlRootGetter ?? throw new ArgumentNullException (nameof (xamlRootGetter));

		progress_bar = new ProgressBar { Minimum = 0, Maximum = 100, Width = 320 };
		text_block = new TextBlock { TextWrapping = TextWrapping.Wrap };
	}

	/// <summary>The dialog's title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>The message shown above the progress bar.</summary>
	public string Text {
		get => text_block.Text;
		set => text_block.Text = value ?? string.Empty;
	}

	/// <summary>Progress, from 0 to 1.</summary>
	public double Progress {
		get => progress_bar.Value / 100.0;
		set => progress_bar.Value = Math.Clamp (value, 0, 1) * 100.0;
	}

	/// <summary>Raised when the user cancels.</summary>
	public event EventHandler? Canceled;

	/// <summary>Shows the dialog.</summary>
	public void Show ()
	{
		if (showing)
			return;

		XamlRoot? root = xaml_root_getter ();

		if (root is null)
			return; // No visual tree yet - degrade to no feedback rather than throwing.

		StackPanel panel = new () { Spacing = 12 };
		panel.Children.Add (text_block);
		panel.Children.Add (progress_bar);

		dialog = new ContentDialog {
			Title = Title,
			Content = panel,
			CloseButtonText = "Cancel",
			XamlRoot = root,
		};

		dialog.CloseButtonClick += (_, _) => Canceled?.Invoke (this, EventArgs.Empty);

		showing = true;

		// Deliberately not awaited: the caller is a synchronous engine loop that
		// keeps running while this is on screen, and it calls Hide when done.
		_ = dialog.ShowAsync ();
	}

	/// <summary>Hides the dialog.</summary>
	public void Hide ()
	{
		if (!showing)
			return;

		showing = false;
		dialog?.Hide ();
		dialog = null;
	}
}
