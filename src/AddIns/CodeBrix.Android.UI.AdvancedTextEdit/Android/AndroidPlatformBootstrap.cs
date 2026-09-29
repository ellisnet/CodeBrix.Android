using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.AdvancedTextEdit.Editing;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Input.TextInput;
using CodeBrix.Android.UI.Portable.TextInput;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.AdvancedTextEdit.Contracts;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;
using CanvasBootstrap = CodeBrix.Android.SkiaSharp.Views.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Android;

/// <summary>
/// Registers the Android side of the AdvancedTextEdit add-in: the canvas supply (IRenderCanvasPlatform,
/// <see cref="RenderCanvasAndroidPlatform"/>), the handler of its drawing surfaces (<see cref="EditorCanvasHandler"/>),
/// and the soft-keyboard session of the editor's TextArea - the "editor" profile and its text target
/// (<see cref="TextAreaInputTarget"/>) and its caret (<see cref="TextAreaCaret"/>: the soft keyboard's focus view sits on it,
/// so the window's pan brings the caret above the keyboard) - after the canvas add-in whose canvas-host factory the surfaces paint through.
/// Idempotent; runs as the module initializer (AdvancedTextEdit.Core loads this assembly by name the first time it
/// needs a canvas, and the CodeBrix.Android.UI bootstrap loads it at start-up).
/// </summary>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Gets a value indicating whether the registrations have run.</summary>
    internal static bool IsRegistered
    {
        get
        {
            lock (_gate)
            {
                return _registered;
            }
        }
    }

    /// <summary>The registered canvas supply (null until registered, or when another assembly registered one first).</summary>
    internal static RenderCanvasAndroidPlatform CanvasPlatform { get; private set; }

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        // The Skia canvas first: the drawing surfaces paint through its canvas-host factory.
        CanvasBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<IRenderCanvasPlatform>())
            {
                // One canvas supply for the process; it creates one element per drawing surface.
                var canvas = new RenderCanvasAndroidPlatform();
                CanvasPlatform = canvas;
                ApiExtensibility.Register(typeof(IRenderCanvasPlatform), _ => canvas);
            }

            CodeBrixHandlers.Register<EditorCanvasElement>(_ => new EditorCanvasHandler());
            CoreTextInput.RegisterProfile(typeof(TextArea), CoreTextInputProfile.Editor);
            CoreTextInput.RegisterTarget(typeof(TextArea), control => control is TextArea area ? new TextAreaInputTarget(area) : null);
            CoreTextInput.RegisterCaret(typeof(TextArea), control => control is TextArea area ? new TextAreaCaret(area) : null);
            _registered = true;
        }
    }
}
