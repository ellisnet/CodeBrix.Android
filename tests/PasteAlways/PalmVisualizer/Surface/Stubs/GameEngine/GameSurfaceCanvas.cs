// STUB (paste always): CodeBrix.Platform.GameEngine.Host.Rendering.GameSurfaceCanvas, FirstStartedEventArgs and
// FirstStartedEventHandler (package CodeBrix.Platform.GameEngine.MitLicenseForever, assembly
// CodeBrix.Platform.GameEngine.Host); the package depends on CodeBrix.Platform repo packages, so a paste-always head may
// not take it, and GameEngine is D-O14 NotSupported on Android. Same namespace, type names, base class (SKXamlCanvas)
// and the event the page subscribes to.
using System;
using SkiaSharp.Views.Windows;

namespace CodeBrix.Platform.GameEngine.Host.Rendering;

/// <summary>Stand-in for the game engine's SkiaSharp canvas control (compile-only).</summary>
public class GameSurfaceCanvas : SKXamlCanvas
{
    /// <summary>Occurs once, the first time the canvas reaches a non-zero layout size (never, here).</summary>
    public event FirstStartedEventHandler FirstStarted { add { } remove { } }
}

/// <summary>Stand-in for the first-started event data (compile-only).</summary>
public sealed class FirstStartedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="FirstStartedEventArgs"/> class.</summary>
    public FirstStartedEventArgs(global::Windows.Foundation.Size newSize) => NewSize = newSize;

    /// <summary>Gets the canvas size, in pixels, at the first non-zero layout.</summary>
    public global::Windows.Foundation.Size NewSize { get; }
}

/// <summary>Represents the method that handles the <see cref="GameSurfaceCanvas.FirstStarted"/> event.</summary>
public delegate void FirstStartedEventHandler(object sender, FirstStartedEventArgs e);
