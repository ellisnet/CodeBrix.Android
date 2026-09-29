using System;
using System.Reflection;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Platform.Animation.Portable;

/// <summary>
/// The per-frame decision of <c>CoreAnimationTicker</c> (AP5; fenced host-free in AP10-C): whether Core has
/// CompositionTarget.Rendering subscribers, and one raise of the event for one display frame. The Android ticker
/// calls <see cref="OnFrame"/> from a Choreographer frame callback and posts the next callback only when it returns
/// true, so the event is raised at most once per display frame while something is subscribed and the frame loop
/// stops by itself when the last subscriber goes (nothing runs while nothing animates). Pure C#, no Android types.
/// </summary>
internal static class RenderingFramePump
{
    private static readonly FieldInfo _renderingField = typeof(CompositionTarget).GetField("_rendering", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>
    /// True when this Core build exposes the subscriber list the pump reads (CompositionTarget's private static
    /// "_rendering" event field). Without it every frame is treated as needed (a degraded but working loop).
    /// </summary>
    internal static bool CanReadSubscribers => _renderingField != null;

    /// <summary>True when Core has CompositionTarget.Rendering subscribers (a frame is needed).</summary>
    /// <returns>True when a frame is needed.</returns>
    /// <exception cref="Exception">Rethrows what reading the field throws (the caller logs it once).</exception>
    internal static bool HasSubscribers() => _renderingField == null || _renderingField.GetValue(null) != null;

    /// <summary>
    /// One display frame: raises CompositionTarget.Rendering once when Core has subscribers, and tells the caller
    /// whether to ask for the next frame (true while subscribers remain after the raise).
    /// </summary>
    /// <param name="onHandlerError">Called with the exception a Rendering handler threw (the frame loop goes on).</param>
    /// <returns>True when the next display frame is needed.</returns>
    internal static bool OnFrame(Action<Exception> onHandlerError)
    {
        if (!HasSubscribers())
        {
            return false;
        }

        try
        {
            CompositionTarget.InvokeRendering();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            onHandlerError?.Invoke(exception);
        }

        return HasSubscribers();
    }
}
