using System;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

// STUB (paste always): the CodeBrix.Platform Lottie add-in (CodeBrix.Platform.UI.Lottie, Skottie
// on SkiaSharp) has no Android flavor yet (plan AP7). The JustBetweenUs page names
// <lottie:LottieVisualSource UriSource="..."/>; this type has the public surface the page uses
// (the same namespace, type name and UriSource property) and plays nothing: the
// AnimatedVisualPlayer shows an empty box. Remove it when the Android Lottie add-in exists.

namespace CommunityToolkit.WinUI.Lottie;

/// <summary>Stub of the Lottie animation source (plays nothing on Android yet).</summary>
public class LottieVisualSource : IAnimatedVisualSource
{
    /// <summary>The Lottie JSON the animation would play.</summary>
    public Uri UriSource { get; set; }

    /// <summary>Creates a source for a URI string (stub: records the URI only).</summary>
    public static LottieVisualSource CreateFromString(string uri) => new() { UriSource = new Uri(uri) };

    /// <summary>Sets the source URI (stub: records it; nothing is loaded).</summary>
    public Task SetSourceAsync(Uri sourceUri)
    {
        UriSource = sourceUri;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)
    {
        diagnostics = null;
        return null;
    }

    /// <inheritdoc />
    public void Update(AnimatedVisualPlayer player)
    {
    }

    /// <inheritdoc />
    public void Load()
    {
    }

    /// <inheritdoc />
    public void Unload()
    {
    }

    /// <inheritdoc />
    public void Play(double fromProgress, double toProgress, bool looped)
    {
    }

    /// <inheritdoc />
    public void Stop()
    {
    }

    /// <inheritdoc />
    public void Pause()
    {
    }

    /// <inheritdoc />
    public void Resume()
    {
    }

    /// <inheritdoc />
    public void SetProgress(double progress)
    {
    }

    /// <inheritdoc />
    public Size Measure(Size availableSize) => new(0, 0);
}
