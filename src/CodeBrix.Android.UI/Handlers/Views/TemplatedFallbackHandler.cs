using CodeBrix.Android.UI.Platform;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of every element no registration applies to - an app-defined Control, a control
/// whose native handler has not been written yet: capabilities None, so Core keeps expanding
/// the element's ControlTemplate, and the element's view (a <see cref="CodeBrixContentViewGroup"/>)
/// MIRRORS that Core-expanded template: its children are the template parts' own native views
/// (borders, panels, presenters, text), laid out where Core put them.
/// </summary>
internal sealed class TemplatedFallbackHandler : ViewGroupHandler<UIElement, CodeBrixContentViewGroup>
{
    /// <summary>The fallback's mapper: the UIElement / FrameworkElement mapper only.</summary>
    public static readonly PropertyMapper<UIElement, TemplatedFallbackHandler> Mapper = new(ViewMappers.ViewMapper);

    /// <summary>Creates the handler.</summary>
    public TemplatedFallbackHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);
}
