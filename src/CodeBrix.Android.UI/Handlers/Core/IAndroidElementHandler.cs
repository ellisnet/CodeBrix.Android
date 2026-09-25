using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// A CodeBrix.Android element handler: the Core seam's per-element contract
/// (<see cref="IElementHandler"/>: connect, property changes, visual children, native measure,
/// arrange, hit test) plus what the Android handler infrastructure needs to know about it.
/// </summary>
internal interface IAndroidElementHandler : IElementHandler
{
    /// <summary>The connected element (null while disconnected).</summary>
    UIElement Element { get; }

    /// <summary>The handler's lifecycle state.</summary>
    ElementHandlerState State { get; }

    /// <summary>
    /// The element's rectangle from Core's last arrange, relative to its visual parent, in
    /// DIPs (margins, alignment and rounding applied): what the native view is laid out to.
    /// </summary>
    Rect ArrangedRect { get; }

    /// <summary>True once Core has arranged the element at least once since it connected.</summary>
    bool HasArranged { get; }

    /// <summary>
    /// False once the platform view can no longer be used (disposed native peer); mappers do
    /// not run then (the MAUI CanInvokeMappers rule).
    /// </summary>
    bool CanInvokeMappers();
}
