// STUB (paste always, compile only; runtime: D-O14 NotSupported - GameEngine): CodeBrix.Platform.GameEngine.Host.Rendering.GameSurfaceCanvas,
// FirstStartedEventHandler and FirstStartedEventArgs (package CodeBrix.Platform.GameEngine.MitLicenseForever, assembly
// CodeBrix.Platform.GameEngine.Host); the package depends on CodeBrix.Platform repo packages, so an Android build cannot
// reference it. Same namespace and type names; the members GameEngineMusicDemo's MainPage and view model use (the real
// canvas derives from SKXamlCanvas; the page only names it, so a FrameworkElement stands in).
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Platform.GameEngine.Host.Rendering;

/// <summary>Stand-in for the game engine's canvas element (compile-only).</summary>
public class GameSurfaceCanvas : FrameworkElement
{
    /// <summary>Occurs once, the first time the canvas reaches a non-zero layout size.</summary>
    public event FirstStartedEventHandler FirstStarted;

    /// <summary>Raises <see cref="FirstStarted"/> (keeps the event used).</summary>
    protected void OnFirstStarted(Size size) => FirstStarted?.Invoke(this, new FirstStartedEventArgs(size));
}

/// <summary>Stand-in for the handler of <see cref="GameSurfaceCanvas.FirstStarted"/> (compile-only).</summary>
public delegate void FirstStartedEventHandler(object sender, FirstStartedEventArgs e);

/// <summary>Stand-in for the data of <see cref="GameSurfaceCanvas.FirstStarted"/> (compile-only).</summary>
public sealed class FirstStartedEventArgs : System.EventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FirstStartedEventArgs(Size newSize) => NewSize = newSize;

    /// <summary>The canvas's first non-zero layout size.</summary>
    public Size NewSize { get; }
}
