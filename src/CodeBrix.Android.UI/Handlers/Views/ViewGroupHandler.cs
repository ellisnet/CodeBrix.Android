using System;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// A handler whose view shows the element's VISUAL CHILDREN: every child element's native
/// view becomes a child of this handler's <see cref="CodeBrixViewGroup"/>, in Core's child
/// order, kept in sync from the seam's child notifications (live adds, moves, removes) and from
/// each child's own connect (children that were already there when the subtree went live).
/// </summary>
/// <typeparam name="TElement">The Core element type.</typeparam>
/// <typeparam name="TView">The view group type.</typeparam>
internal abstract class ViewGroupHandler<TElement, TView> : ViewHandler<TElement, TView>, IViewGroupHandler
    where TElement : UIElement
    where TView : CodeBrixViewGroup
{
    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper.</param>
    /// <param name="commandMapper">The command mapper, or null.</param>
    protected ViewGroupHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
    }

    /// <inheritdoc />
    public CodeBrixViewGroup ViewGroup => base.PlatformView as CodeBrixViewGroup;

    /// <inheritdoc />
    public override void OnChildAdded(UIElement child, int index)
    {
        if (child?.Handler is IViewHandler { NativeView: { } view } && ViewGroup is { } group)
        {
            group.AddElementChild(child, view, index);
        }
    }

    /// <inheritdoc />
    public override void OnChildRemoved(UIElement child)
    {
        if (child != null)
        {
            ViewGroup?.RemoveElementChild(child);
        }
    }

    /// <inheritdoc />
    public override void OnChildMoved(int oldIndex, int newIndex) => ViewGroup?.MoveElementChild(oldIndex, newIndex);

    /// <inheritdoc />
    public void AttachChild(UIElement child, AView view)
    {
        if (ViewGroup is not { } group || Element is not { } element)
        {
            return;
        }

        var index = 0;
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            var sibling = VisualTreeHelper.GetChild(element, i);
            if (ReferenceEquals(sibling, child))
            {
                break;
            }

            if (sibling is UIElement shown && group.ViewOf(shown) != null)
            {
                index++;
            }
        }

        group.AddElementChild(child, view, index);
    }

    /// <inheritdoc />
    protected override void ConnectHandler(TView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ElementHandler = this;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TView platformView)
    {
        platformView.ElementHandler = null;
        base.DisconnectHandler(platformView);
    }
}

/// <summary>A handler whose native view holds the views of the element's visual children.</summary>
internal interface IViewGroupHandler : IViewHandler
{
    /// <summary>The view group (null while disconnected).</summary>
    CodeBrixViewGroup ViewGroup { get; }

    /// <summary>Shows a connected child's view at its Core child position.</summary>
    void AttachChild(UIElement child, AView view);
}

/// <summary>
/// Keeps each connected element's native view inside its visual parent's view group: called
/// when a handler connects (a subtree entering a live tree connects parent first, and the
/// seam's OnChildAdded only covers children added to an ALREADY live parent) and when it
/// disconnects.
/// </summary>
internal static class ViewTreeSync
{
    /// <summary>Puts the element's view into its visual parent's view group (no-op when that parent shows no children).</summary>
    internal static void AttachToParent(UIElement element, AView view)
    {
        if (element == null || view == null)
        {
            return;
        }

        if (VisualTreeHelper.GetParent(element) is UIElement parent && parent.Handler is IViewGroupHandler parentHandler)
        {
            parentHandler.AttachChild(element, view);
        }
    }

    /// <summary>Takes the element's view out of whatever view group shows it.</summary>
    internal static void DetachFromParent(UIElement element, AView view)
    {
        switch (view?.Parent)
        {
            case CodeBrixViewGroup group when element != null:
                group.RemoveElementChild(element);
                break;
            case AViewGroup other:
                other.RemoveView(view);
                break;
        }
    }
}
