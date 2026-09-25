using System;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="IRenderingPlatform"/>. Core calls
/// <see cref="OnRenderFrameOpportunity"/> after each layout tick; the Android host of that
/// XamlRoot is told a new layout is available (it requests an Android layout pass and
/// raises <see cref="AndroidXamlRootHost.LayoutUpdated"/>, which diagnostics and the
/// projection viewer listen to). Composition targets are inert (nothing is recorded).
/// </summary>
internal sealed class RenderingAndroidPlatform : IRenderingPlatform
{
    private bool _warned;

    /// <inheritdoc />
    public void OnRenderFrameOpportunity(XamlRoot xamlRoot)
    {
        if (xamlRoot != null && XamlRootMap.GetHostForRoot(xamlRoot) is AndroidXamlRootHost host)
        {
            host.OnLayoutUpdated();
        }
        else if (!_warned)
        {
            _warned = true;
            HostLog.For("CodeBrix.Android.UI.Rendering").LogWarning("Render frame opportunity for a XamlRoot without an Android host.");
        }
    }

    /// <inheritdoc />
    public ICompositionTargetPlatform CreateCompositionTargetPlatform(CompositionTarget target) => CompositionTargetAndroidPlatform.Instance;
}

/// <summary>
/// The inert <see cref="ICompositionTargetPlatform"/>: Android never records a composition
/// frame (native views draw themselves).
/// </summary>
internal sealed class CompositionTargetAndroidPlatform : ICompositionTargetPlatform
{
    internal static readonly CompositionTargetAndroidPlatform Instance = new();

    /// <inheritdoc />
    public bool CanRecordFrame() => false;

    /// <inheritdoc />
    public void RecordFrame()
    {
    }

    /// <inheritdoc />
    public void UpdateNativeElementsOrder()
    {
    }
}
