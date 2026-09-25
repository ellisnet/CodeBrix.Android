using Microsoft.UI.Xaml.Media;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>An element handler whose platform view is an Android view.</summary>
internal interface IViewHandler : IAndroidElementHandler
{
    /// <summary>The native view (null while disconnected).</summary>
    AView NativeView { get; }

    /// <summary>The context the native view was created with.</summary>
    AContext Context { get; }

    /// <summary>Physical pixels per DIP of the element's XamlRoot.</summary>
    double Density { get; }

    /// <summary>Asks Android to lay this view (and its parent) out again at the next pass.</summary>
    void InvalidateNativeLayout();

    /// <summary>Follows changes inside the element's render transform (null stops following).</summary>
    void WatchRenderTransform(Transform transform);
}
