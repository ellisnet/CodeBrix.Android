using System;
using ParityFixture.Xaml;

// A miniature handler layer with the shapes of CodeBrix.Android.UI (mappers, chains, helpers, policy appends, registrations).
namespace ParityFixture.Handlers;

public interface IAndroidElementHandler
{
}

public interface IPropertyMapper
{
}

public interface IPropertyMapper<TElement, THandler> : IPropertyMapper
{
    void Add(DependencyProperty key, Action<THandler, TElement> action);
}

public class PropertyMapper<TElement, THandler> : IPropertyMapper<TElement, THandler>
{
    public PropertyMapper(params IPropertyMapper[] chained)
    {
    }

    public Action<THandler, TElement> this[DependencyProperty key]
    {
        get => null;
        set => Add(key, value);
    }

    public void Add(DependencyProperty key, Action<THandler, TElement> action)
    {
    }
}

public static class PropertyMapperExtensions
{
    public static void AppendToMapping<TElement, THandler>(this IPropertyMapper<TElement, THandler> mapper, DependencyProperty key, Action<THandler, TElement> method)
    {
    }
}

public abstract class ElementHandler : IAndroidElementHandler
{
    protected ElementHandler(IPropertyMapper mapper)
    {
    }
}

public sealed class TemplatedFallbackHandler : IAndroidElementHandler
{
}

public static class ViewMappers
{
    public static readonly PropertyMapper<UIElement, IAndroidElementHandler> ViewMapper = new()
    {
        [UIElement.OpacityProperty] = (h, e) => { },
    };
}

public static class MapperHelpers
{
    public static PropertyMapper<TElement, THandler> WithControlKeys<TElement, THandler>(this PropertyMapper<TElement, THandler> mapper)
    {
        mapper[Control.ForegroundProperty] = (h, e) => { };
        return mapper;
    }
}

public sealed class ButtonHandler : ElementHandler
{
    public static readonly PropertyMapper<Button, ButtonHandler> Mapper = new PropertyMapper<Button, ButtonHandler>(ViewMappers.ViewMapper)
    {
        [Button.ContentProperty] = (h, e) => { },
    }.WithControlKeys();

    public ButtonHandler()
        : base(Mapper)
    {
    }

    public static IAndroidElementHandler Create(UIElement element) => element is Button ? new ButtonHandler() : new TemplatedFallbackHandler();
}

public sealed class SliderHandler : ElementHandler
{
    public static readonly PropertyMapper<Slider, SliderHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Slider.ValueProperty] = MapValue,
    };

    public SliderHandler()
        : base(Mapper)
    {
    }

    public static void MapValue(SliderHandler handler, Slider slider)
    {
    }
}

public static class FixturePolicy
{
    public static void Install() => SliderHandler.Mapper.AppendToMapping(Control.ForegroundProperty, (h, e) => { });
}

public sealed class ElementHandlerRegistry
{
    public void Register<TElement>(Func<UIElement, IAndroidElementHandler> factory)
    {
    }
}

public static class CodeBrixHandlers
{
    public static ElementHandlerRegistry CreateDefaultRegistry()
    {
        var registry = new ElementHandlerRegistry();
        registry.Register<Button>(ButtonHandler.Create);
        registry.Register<Slider>(_ => new SliderHandler());
        registry.Register<Control>(_ => new TemplatedFallbackHandler());
        registry.Register<FrameworkElement>(_ => null);
        return registry;
    }
}
