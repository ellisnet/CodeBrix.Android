// ChromeManager.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

// Pinta.Brix note: upstream held toolkit widget references installed by the
// GTK shell (window, toolbars, dock, menus). The port keeps the same manager
// role with engine-level abstractions: descriptor toolbars, command
// registration, and dialog handler delegates the UI layer installs.
public interface IChromeService
{
	Task<bool> LaunchSimpleEffectDialog (
		BaseEffect effect,
		IWorkspaceService workspace);
}

public sealed class ChromeManager : IChromeService
{
	private PointI last_canvas_cursor_point;
	private bool main_window_busy;

	// NRT - These are all initialized via the Initialize* functions
	// but it would be nice to rewrite it to provably non-null.
	private IProgressDialog progress_dialog = null!;
	private ErrorDialogHandler error_dialog_handler = null!;
	private MessageDialogHandler message_dialog_handler = null!;
	private SimpleEffectDialogHandler simple_effect_dialog_handler = null!;

	/// <summary>The tool-options toolbar the current tool populates.</summary>
	public ToolBar ToolToolBar { get; } = new ();

	public IProgressDialog ProgressDialog => progress_dialog;

	/// <summary>Application commands registered by engine components; the UI
	/// layer binds them to menus and keyboard accelerators.</summary>
	public IReadOnlyList<Command> RegisteredCommands => registered_commands;
	private readonly List<Command> registered_commands = [];

	public event EventHandler<CommandEventArgs>? CommandRegistered;

	public void RegisterCommand (Command command)
	{
		registered_commands.Add (command);
		CommandRegistered?.Invoke (this, new CommandEventArgs (command));
	}

	public PointI LastCanvasCursorPoint {
		get => last_canvas_cursor_point;
		set {
			if (last_canvas_cursor_point != value) {
				last_canvas_cursor_point = value;
				OnLastCanvasCursorPointChanged ();
			}
		}
	}

	public bool MainWindowBusy {
		get => main_window_busy;
		set {
			main_window_busy = value;
			MainWindowBusyChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	public void InitializeProgessDialog (IProgressDialog progressDialog)
	{
		progress_dialog = progressDialog;
	}

	public void InitializeErrorDialogHandler (ErrorDialogHandler handler)
	{
		error_dialog_handler = handler;
	}

	public void InitializeMessageDialog (MessageDialogHandler handler)
	{
		message_dialog_handler = handler;
	}

	public void InitializeSimpleEffectDialog (SimpleEffectDialogHandler handler)
	{
		simple_effect_dialog_handler = handler;
	}

	public async Task ShowErrorDialog (
		string message,
		string body,
		string details)
	{
		ErrorDialogResponse response = await error_dialog_handler (message, body, details);
		switch (response) {
			case ErrorDialogResponse.Bug:
				BugReportRequested?.Invoke (this, EventArgs.Empty);
				break;
		}
	}

	public Task ShowMessageDialog (string message, string body)
	{
		return message_dialog_handler (message, body);
	}

	public string MainWindowTitle { get; private set; } = PintaCore.ApplicationName;

	public event EventHandler? MainWindowTitleChanged;

	public void SetMainWindowTitle (string title)
	{
		if (MainWindowTitle == title)
			return;
		MainWindowTitle = title;
		MainWindowTitleChanged?.Invoke (this, EventArgs.Empty);
	}

	public void SetStatusBarText (string text)
	{
		OnStatusBarTextChanged (text);
	}

	public Task<bool> LaunchSimpleEffectDialog (
		BaseEffect effect,
		IWorkspaceService workspace)
	{
		return simple_effect_dialog_handler (effect, workspace);
	}

	private void OnLastCanvasCursorPointChanged ()
	{
		LastCanvasCursorPointChanged?.Invoke (this, EventArgs.Empty);
	}

	private void OnStatusBarTextChanged (string text)
	{
		StatusBarTextChanged?.Invoke (this, new TextChangedEventArgs (text));
	}

	public event EventHandler? LastCanvasCursorPointChanged;
	public event EventHandler<TextChangedEventArgs>? StatusBarTextChanged;
	public event EventHandler? MainWindowBusyChanged;
	public event EventHandler? BugReportRequested;
}

public sealed class CommandEventArgs : EventArgs
{
	public CommandEventArgs (Command command)
	{
		Command = command;
	}

	public Command Command { get; }
}

public interface IProgressDialog
{
	void Show ();
	void Hide ();
	string Title { get; set; }
	string Text { get; set; }
	double Progress { get; set; }
	event EventHandler Canceled;
}

public delegate Task<ErrorDialogResponse> ErrorDialogHandler (string message, string body, string details);
public delegate Task MessageDialogHandler (string message, string body);
public delegate Task<bool> SimpleEffectDialogHandler (BaseEffect effect, IWorkspaceService workspace);
