using System;
using Microsoft.UI.Xaml;

namespace PolyHavenBrowser.Display;

/// <summary>
/// Creates an <see cref="IModelRenderEngine"/> for one specific graphics backend. The app no
/// longer registers a single fixed factory in the container - the runtime choice between
/// backends is made through <see cref="IModelRenderEngineSelector"/>, which uses these
/// per-backend factories internally.
/// </summary>
public interface IModelRenderEngineFactory
{
    /// <summary>Creates a new, unused rendering engine. The caller owns it and disposes it.</summary>
    IModelRenderEngine Create();
}

/// <summary>
/// Creates the OpenGL engine, <see cref="OpenGlModelRenderEngine"/> - the default backend,
/// available on every head. Its Vulkan sibling is <see cref="VulkanModelRenderEngineFactory"/>.
/// </summary>
public sealed class OpenGlModelRenderEngineFactory : IModelRenderEngineFactory
{
    private readonly Func<XamlRoot> _getXamlRoot;

    /// <summary>
    /// Creates the factory. The <paramref name="getXamlRoot"/> accessor is passed to each engine
    /// and invoked lazily on the render thread to obtain the <see cref="XamlRoot"/> the offscreen
    /// GL context is created from.
    /// </summary>
    /// <param name="getXamlRoot">Returns the hosting page's <see cref="XamlRoot"/>.</param>
    public OpenGlModelRenderEngineFactory(Func<XamlRoot> getXamlRoot) =>
        _getXamlRoot = getXamlRoot ?? throw new ArgumentNullException(nameof(getXamlRoot));

    /// <inheritdoc />
    public IModelRenderEngine Create() => new OpenGlModelRenderEngine(_getXamlRoot);
}
