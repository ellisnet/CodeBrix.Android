using System;
using SkiaSharp.Views.Windows;
using SkiaSharp.Views.Windows.Contracts;
using Windows.Foundation;

namespace CodeBrix.Android.SkiaSharp.Views.Android;

/// <summary>
/// The Android implementation of <see cref="ISKSwapChainPanelPlatform"/>: the same contract as every
/// CodeBrix.Platform head - SKSwapChainPanel is a placeholder that lets GPU swap-chain code compile.
/// Building one is refused (NotSupportedException) unless the app opted out with
/// SKSwapChainPanel.RaiseOnUnsupported = false; then it is an empty element (no surface, no
/// graphics context, a PaintSurface handler that is never called). Use SKXamlCanvas to paint.
/// </summary>
internal sealed class SKSwapChainPanelAndroidPlatform : ISKSwapChainPanelPlatform
{
    /// <summary>The refusal's message.</summary>
    internal const string NotSupportedMessage = "SKSwapChainPanel is not supported on CodeBrix.Android (use SKXamlCanvas)";

    /// <inheritdoc />
    public void OnConstructed() => ThrowIfRefused();

    /// <inheritdoc />
    public Size GetCanvasSize()
    {
        ThrowIfRefused();
        return default;
    }

    /// <inheritdoc />
    public object GetGRContext()
    {
        ThrowIfRefused();
        return null;
    }

    /// <inheritdoc />
    public void Invalidate()
    {
    }

    private static void ThrowIfRefused()
    {
        if (SKSwapChainPanel.RaiseOnUnsupported)
        {
            throw new NotSupportedException(NotSupportedMessage);
        }
    }
}
