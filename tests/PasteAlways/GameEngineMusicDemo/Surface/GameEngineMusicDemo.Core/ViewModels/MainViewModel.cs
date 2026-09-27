using CodeBrix.Platform.GameEngine.Host.Rendering;
using CodeBrix.Platform.Simple;
using GameEngineMusicDemo.Game;
using System.Diagnostics;

namespace GameEngineMusicDemo.ViewModels;

/// <summary>
/// Lets the page hand the view model the game canvas once it has a size, and read back the demo the
/// view model starts against it, so the page never names the view model's concrete type.
/// </summary>
public interface IManageGameCanvas
{
    /// <summary>
    /// The running demo, or null before the canvas has started. The page reads this rather than
    /// binding to it, so it needs no change notification.
    /// </summary>
    GameEngineMusicDemoGame Demo { get; }

    /// <summary>Called once, when the canvas has started for the first time.</summary>
    /// <param name="canvas">The started canvas.</param>
    void CanvasFirstStart(GameSurfaceCanvas canvas);
}

/// <summary>
/// The page's view model. It owns the <see cref="GameEngineMusicDemoGame"/> and exposes it to the
/// page, which drives the controls directly.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IManageGameCanvas
{
    /// <summary>Creates the view model.</summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        Debug.WriteLine("GameEngineMusicDemo view model startup.");
    }

    #region | Bindable properties |

    //No bound properties, deliberately: every control on the page drives the music API through the
    //  demo object rather than through bound state. An application that is not an API demonstration
    //  should hold that state in SetProperty-backed properties here instead.

    #endregion

    #region | IManageGameCanvas implementation |

    /// <inheritdoc/>
    public GameEngineMusicDemoGame Demo { get; private set; }

    /// <inheritdoc/>
    public void CanvasFirstStart(GameSurfaceCanvas canvas)
    {
        Demo = new GameEngineMusicDemoGame(canvas);
        Demo.Start();
    }

    #endregion

    #region | Commands and their implementations |

    //No commands, deliberately: the point of this sample is to show the music API being called,
    //  so every control on the page calls one method on the demo directly. An application that is
    //  not an API demonstration should put that behaviour in SimpleCommand commands here instead.

    #endregion
}
