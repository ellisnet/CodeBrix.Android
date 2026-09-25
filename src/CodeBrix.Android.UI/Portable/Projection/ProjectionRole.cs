using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CodeBrix.Android.UI.Portable.Projection;

/// <summary>What the projection viewer shows for an element (see <see cref="ProjectionRoles"/>).</summary>
internal enum ProjectionRole
{
    /// <summary>A layout or infrastructure element: only its own background / border (if any) is drawn.</summary>
    Container,

    /// <summary>A TextBlock: a real native text view with the TextBlock's text, font, size, colour and alignment.</summary>
    Text,

    /// <summary>A FontIcon: a native text view with the glyph in the icon's font.</summary>
    Glyph,

    /// <summary>An Image: a labelled placeholder box (images are not decoded before AP3a).</summary>
    Image,

    /// <summary>Any other element (a control, a shape, a custom element): a labelled placeholder box.</summary>
    Control,
}

/// <summary>
/// Classifies element types for the projection viewer (pure: works on types, so it is
/// testable without a UI host).
/// </summary>
internal static class ProjectionRoles
{
    /// <summary>Returns the role of an element type.</summary>
    /// <param name="elementType">The element's runtime type.</param>
    internal static ProjectionRole Classify(Type elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);

        if (typeof(TextBlock).IsAssignableFrom(elementType))
        {
            return ProjectionRole.Text;
        }

        if (typeof(FontIcon).IsAssignableFrom(elementType))
        {
            return ProjectionRole.Glyph;
        }

        if (typeof(Image).IsAssignableFrom(elementType))
        {
            return ProjectionRole.Image;
        }

        // Infrastructure (window chrome, island and popup roots, dialog panels) is never public.
        if (!elementType.IsPublic && !elementType.IsNestedPublic)
        {
            return ProjectionRole.Container;
        }

        if (typeof(Panel).IsAssignableFrom(elementType)
            || typeof(Border).IsAssignableFrom(elementType)
            || typeof(ContentPresenter).IsAssignableFrom(elementType)
            || typeof(ItemsPresenter).IsAssignableFrom(elementType)
            || typeof(Viewbox).IsAssignableFrom(elementType)
            || typeof(Popup).IsAssignableFrom(elementType)
            || typeof(UserControl).IsAssignableFrom(elementType)
            || typeof(Frame).IsAssignableFrom(elementType)
            || typeof(ScrollViewer).IsAssignableFrom(elementType)
            || typeof(ContentDialog).IsAssignableFrom(elementType)
            || elementType == typeof(ContentControl))
        {
            return ProjectionRole.Container;
        }

        return ProjectionRole.Control;
    }

    /// <summary>
    /// Returns true when an element of this role gets a label (type name + key properties):
    /// controls and images that the page itself declares - not the parts of a control's
    /// template (those only draw their backgrounds, borders and texts).
    /// </summary>
    /// <param name="role">The element's role.</param>
    /// <param name="isTemplatePart">True when the element was created by a control template.</param>
    internal static bool IsLabelled(ProjectionRole role, bool isTemplatePart) =>
        !isTemplatePart && (role is ProjectionRole.Control or ProjectionRole.Image);
}
