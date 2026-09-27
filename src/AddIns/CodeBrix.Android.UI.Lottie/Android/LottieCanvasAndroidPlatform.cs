using System;
using CodeBrix.Platform.UI.Lottie.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Lottie.Android;

/// <summary>
/// The Android canvas supply of the Lottie add-in (ILottieCanvasPlatform): one <see cref="LottieCanvasElement"/> per
/// animation, repainted once per engine tick. The source in the Core owns everything else - the decoded animation,
/// the frame clock and its timer, and the drawing of each frame.
/// </summary>
internal sealed class LottieCanvasAndroidPlatform : ILottieCanvasPlatform
{
    /// <inheritdoc />
    public UIElement CreateRenderSurface(Action<object, Size> render) => new LottieCanvasElement(render);

    /// <inheritdoc />
    public void Invalidate(UIElement renderSurface) => ((LottieCanvasElement)renderSurface).Invalidate();
}
