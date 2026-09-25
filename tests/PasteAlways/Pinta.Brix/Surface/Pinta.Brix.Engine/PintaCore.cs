//
// PintaCore.cs
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

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public static class PintaCore
{
	public static ActionManager Actions { get; }
	public static ChromeManager Chrome { get; }
	public static IClipboardService Clipboard { get; private set; }
	public static EffectsManager Effects { get; }
	public static ImageConverterManager ImageFormats { get; }
	public static IServiceManager Services { get; }
	public static LivePreviewManager LivePreview { get; }
	public static PaintBrushManager PaintBrushes { get; }
	public static PaletteFormatManager PaletteFormats { get; }
	public static PaletteManager Palette { get; }
	public static RecentFileManager RecentFiles { get; }
	public static IResourceService Resources { get; private set; }
	public static SettingsManager Settings { get; }
	public static SystemManager System { get; }
	public static ToolManager Tools { get; }
	public static WorkspaceManager Workspace { get; }
	public static CanvasGridManager CanvasGrid { get; }

	/// <summary>
	/// Unique identifier for the application.
	/// This is used for GApplication and also must match the .desktop file.
	/// </summary>
	public const string ApplicationId = "com.github.PintaProject.Pinta";

	/// <summary>
	/// The name this application calls itself, everywhere a user can see it.
	/// </summary>
	/// <remarks>
	/// Pinta.Brix note: the application must NEVER refer to itself as "Pinta"
	/// in the UI - that is the upstream application's name. Naming the upstream
	/// project is still correct where it is an attribution or a link to it
	/// (the about box, the Help menu's upstream URLs); calling OURSELVES that
	/// is not. Route every user-visible self-reference through this constant so
	/// the two cannot drift apart again.
	/// </remarks>
	public const string ApplicationName = "Pinta.Brix";

	/// <summary>
	/// The version of upstream Pinta this port tracks.
	/// </summary>
	public const string ApplicationVersion = "3.2";

	/// <summary>
	/// The oldest version of Pinta for which add-ins built against it will still
	/// run in the current version.
	/// This should be updated when there are ABI-breaking changes.
	/// </summary>
	public const string AddinCompatVersion = "3.1";

	/// <summary>Timer service proxy; the UI layer installs the real implementation at startup.</summary>
	public static TimerServiceProxy Timer { get; }

	static PintaCore ()
	{
		// --- Services that don't depend on other services

		// The UI layer installs real implementations of these at startup.
		NullResourceService resources = new ();
		TimerServiceProxy timer = new ();

		SystemManager system = new ();
		SettingsManager settings = new ();
		ChromeManager chrome = new ();
		PaintBrushManager paintBrushes = new ();
		PaletteFormatManager paletteFormats = new ();
		RecentFileManager recentFiles = new ();

		// --- Services that depend on other services

		ImageConverterManager imageFormats = new (settings);
		WorkspaceManager workspace = new (system, chrome, imageFormats);
		ToolManager tools = new (workspace, chrome);
		PaletteManager palette = new (settings, paletteFormats);
		LivePreviewManager livePreview = new (workspace, tools, system, chrome, timer);
		EffectsManager effects = new (chrome, livePreview);
		CanvasGridManager canvasGrid = new (workspace, settings);

		// --- Service manager

		ServiceManager services = new ();
		services.AddService<IResourceService> (resources);
		services.AddService<ITimerService> (timer);
		services.AddService<ISettingsService> (settings);
		services.AddService<IWorkspaceService> (workspace);
		services.AddService<IPaintBrushService> (paintBrushes);
		services.AddService<IToolService> (tools);
		services.AddService (imageFormats);
		services.AddService (paletteFormats);
		services.AddService (system);
		services.AddService (recentFiles);
		services.AddService<ILivePreview> (livePreview);
		services.AddService<IPaletteService> (palette);
		services.AddService<IChromeService> (chrome);
		services.AddService<ISystemService> (system);
		services.AddService (effects);
		services.AddService<ICanvasGridService> (canvasGrid);

		// --- References to expose

		Resources = resources;
		Timer = timer;
		System = system;
		Settings = settings;
		Workspace = workspace;
		PaintBrushes = paintBrushes;
		Tools = tools;
		ImageFormats = imageFormats;
		PaletteFormats = paletteFormats;
		RecentFiles = recentFiles;
		LivePreview = livePreview;
		Palette = palette;
		Chrome = chrome;
		Effects = effects;
		CanvasGrid = canvasGrid;

		// The action model is pure declaration - it takes no services, and the
		// UI layer attaches the handlers.
		Actions = new ActionManager ();

		// Replaced by the platform clipboard at startup.
		Clipboard = new NullClipboardService ();

		Services = services;
	}

	/// <summary>
	/// Installs the UI-layer resource loader (icons). Call once at startup.
	/// </summary>
	public static void InitializeResources (IResourceService resources)
	{
		Resources = resources;
	}

	/// <summary>
	/// Installs the UI-layer timer implementation. Call once at startup.
	/// </summary>
	public static void InitializeTimer (ITimerService timer)
	{
		Timer.Inner = timer;
	}

	/// <summary>
	/// Installs the UI-layer clipboard implementation. Call once at startup.
	/// </summary>
	/// <remarks>
	/// Until this is called the clipboard is a no-op that reports nothing
	/// available, so engine code can call it unconditionally.
	/// </remarks>
	public static void InitializeClipboard (IClipboardService clipboard)
	{
		Clipboard = clipboard;
	}
}
