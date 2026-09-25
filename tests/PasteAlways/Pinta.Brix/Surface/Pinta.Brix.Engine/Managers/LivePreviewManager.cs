//
// LivePreviewManager.cs
//
// Author:
//       Greg Lowe <greg@vis.net.nz>
//
// Copyright (c) 2010 Greg Lowe
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

#if (!LIVE_PREVIEW_DEBUG && DEBUG)
#undef DEBUG
#endif

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Pinta.Brix.Engine.Drawing;
using Debug = System.Diagnostics.Debug;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public interface ILivePreview
{
	RectangleI RenderBounds { get; }
	bool IsEnabled { get; }
	ImageSurface LivePreviewSurface { get; }
}

public sealed class LivePreviewManager : ILivePreview
{
	private readonly WorkspaceManager workspace;
	private readonly ToolManager tools;
	private readonly SystemManager system;
	private readonly ChromeManager chrome;
	private readonly ITimerService timer;

	internal LivePreviewManager (
		WorkspaceManager workspaceManager,
		ToolManager toolManager,
		SystemManager systemManager,
		ChromeManager chromeManager,
		ITimerService timerService)
	{
		IsEnabled = false;

		workspace = workspaceManager;
		tools = toolManager;
		system = systemManager;
		chrome = chromeManager;
		timer = timerService;
	}

	public Drawing.ImageSurface LivePreviewSurface { get; private set; } = null!;
	public RectangleI RenderBounds { get; private set; }
	public bool IsEnabled { get; private set; }

	/// <summary>
	/// Starts a live preview of <paramref name="effect"/> and forgets the
	/// resulting task. The command model's Activated event is synchronous, so
	/// this is the entry point it calls; a failure inside the run is recorded
	/// rather than left to tear the process down on an unobserved task.
	/// </summary>
	public void Start (BaseEffect effect)
	{
		_ = RunAndObserve (StartAsync (effect), effect.Name);
	}

	private static async Task RunAndObserve (Task run, string effectName)
	{
		try {
			await run;
		} catch (Exception ex) {
			Debug.WriteLine ($"Live preview of '{effectName}' failed: {ex}");
		}
	}

	/// <summary>
	/// Runs a live preview of <paramref name="effect"/> to completion: the
	/// preview surface, the worker-thread render, the configuration dialog and
	/// the history item the confirmed result is pushed as. Awaitable, so a
	/// caller that can wait sees the failure instead of losing it.
	/// </summary>
	public async Task StartAsync (BaseEffect effect)
	{
		if (IsEnabled)
			throw new InvalidOperationException ("LivePreviewManager.Start() called while live preview is already enabled.");

		tools.Commit ();

		Document doc = workspace.ActiveDocument;
		DocumentSelection selection = doc.Selection;

		IsEnabled = true;

		//TODO Use the current tool layer instead.
		LivePreviewSurface = CairoExtensions.CreateImageSurface (
			Drawing.Format.Argb32,
			workspace.ImageSize.Width,
			workspace.ImageSize.Height);

		RenderBounds = selection.Visible ? selection.GetBounds ().ToInt () : LivePreviewSurface.GetBounds ();
		RenderBounds = workspace.ClampToImageSize (RenderBounds);

		const uint UPDATE_MILLISECONDS = 100;

		AsyncEffectRenderer.Settings settings = new (
			threadCount: system.RenderThreads,
			renderBounds: RenderBounds,
			effectIsTileable: effect.IsTileable);

		int handlersInQueue = 0;
		Layer layer = doc.Layers.CurrentUserLayer;

		string effectName = effect.Name;

		SimpleHistoryItem historyItem = new (effect.Icon, effect.Name);
		historyItem.TakeSnapshotOfLayer (doc.Layers.CurrentUserLayerIndex);

		RenderHandle renderHandle = null!; // NRT: Assigned before first use

		IProgressDialog dialog = chrome.ProgressDialog;
		dialog.Title = Translations.GetString ("Rendering Effect");
		dialog.Text = effect.Name;
		dialog.Progress = 0;
		dialog.Canceled += HandleProgressDialogCancel;

		bool renderAlive = true;

		try {
			// Paint the pre-effect layer surface into into the working surface.
			using Drawing.Context ctx = new (LivePreviewSurface);
			layer.Draw (ctx, layer.Surface, 1);

			Debug.WriteLine (DateTime.Now.ToString ("HH:mm:ss:ffff") + "Start Live preview.");

			if (effect.EffectData != null)
				effect.EffectData.PropertyChanged += EffectData_PropertyChanged;

			renderHandle = AsyncEffectRenderer.Start (
				settings,
				effect,
				layer.Surface,
				LivePreviewSurface);

			using IDisposable _ = timer.Start (
				UPDATE_MILLISECONDS,
				() => {
					if (!renderAlive) return false;
					PollForUpdate (renderHandle);
					return true; // Keep ticking as long as the effect is active.
				}
			);

			bool userConfirmed = !effect.IsConfigurable || await effect.LaunchConfiguration ();

			chrome.MainWindowBusy = true;

			if (!userConfirmed) {
				Debug.WriteLine ("User decided not to proceed with the render");
				renderHandle.Cancel ();
				await renderHandle.Task;
				return;
			}

			// The user confirmed, so show progress dialog
			Debug.WriteLine (DateTime.Now.ToString ("HH:mm:ss:ffff") + "LivePreviewManager.Apply()");

			dialog.Show ();

			var result = await renderHandle.Task;

			// Final poll after the renderer finishes to ensure the last-rendered tiles are displayed.
			PollForUpdate (renderHandle);

			foreach (var ex in result.Errors)
				Debug.WriteLine ("AsyncEffectRenderer Error while rendering effect: " + effectName + " exception: " + ex.Message + "\n" + ex.StackTrace);

			if (result.WasCanceled) {
				Debug.WriteLine ("User decided to cancel the render");
				renderHandle.Cancel ();
				await renderHandle.Task;
				return;
			}

			// Was not canceled, so finally apply
			Debug.WriteLine ("Render completed without the user canceling");

			using Drawing.Context context = new (layer.Surface);

			context.Save ();
			workspace.ActiveDocument.Selection.Clip (context);

			layer.DrawWithOperator (context, LivePreviewSurface, Drawing.Operator.Source);
			context.Restore ();

			workspace.ActiveDocument.History.PushNewItem (historyItem);

		} finally {

			IsEnabled = false;
			LivePreviewSurface = null!;
			workspace.Invalidate ();

			if (effect.EffectData != null)
				effect.EffectData.PropertyChanged -= EffectData_PropertyChanged;

			chrome.MainWindowBusy = false;

			dialog.Canceled -= HandleProgressDialogCancel;

			dialog.Hide ();

			renderAlive = false;
		}

		// === Methods ===

		void HandleProgressDialogCancel (object? o, EventArgs e)
		{
			renderHandle.Cancel ();
		}

		// An event handler, so it cannot return a task; the body is wrapped
		// because an exception escaping an async void handler has nowhere to go
		// but the process.
		async void EffectData_PropertyChanged (object? sender, PropertyChangedEventArgs e)
		{
			try {
				// TODO: calculate bounds
				handlersInQueue++;
				renderHandle.Cancel ();
				await renderHandle.Task;
				handlersInQueue--;
				if (handlersInQueue > 0) return;
				renderHandle = AsyncEffectRenderer.Start (settings, effect, layer.Surface, LivePreviewSurface);
			} catch (Exception ex) {
				Debug.WriteLine ("Live preview restart failed: " + ex);
			}
		}

		// This method now polls the renderer for its state instead of being a passive event handler.
		void PollForUpdate (RenderHandle renderTask)
		{
			Debug.WriteLine (DateTime.Now.ToString ("HH:mm:ss:ffff") + " Polling for update.");

			chrome.ProgressDialog.Progress = renderTask.Progress;

			if (!renderTask.TryConsumeBounds (out RectangleI updatedBounds))
				return;

			double scale = workspace.Scale;

			// Transform bounds (Image -> Canvas -> Window)

			// Calculate canvas bounds.
			PointD bounds1 = new (
				X: updatedBounds.Left * scale,
				Y: updatedBounds.Top * scale);

			PointD bounds2 = new (
				X: (updatedBounds.Right + 1) * scale,
				Y: (updatedBounds.Bottom + 1) * scale);

			// TODO Figure out why when scale > 1 that I need add on an
			// extra pixel of padding.
			// I must being doing something wrong here.
			if (scale > 1.0) {
				//x1 = (bounds.Left-1) * scale;
				bounds1 = bounds1 with { Y = (updatedBounds.Top - 1) * scale };
				//x2 = (bounds.Right+1) * scale;
				//y2 = (bounds.Bottom+1) * scale;
			}

			// Convert to integer, carefully not to miss partially covered
			// pixels by rounding incorrectly.
			int x = (int) Math.Floor (bounds1.X);
			int y = (int) Math.Floor (bounds1.Y);
			RectangleI areaToInvalidate = new (
				X: x,
				Y: y,
				Width: (int) Math.Ceiling (bounds2.X) - x,
				Height: (int) Math.Ceiling (bounds2.Y) - y);

			// Tell GTK to expose the drawing area.
			workspace.ActiveWorkspace.InvalidateWindowRect (areaToInvalidate);
		}
	}
}
