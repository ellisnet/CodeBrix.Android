using System;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The platform's <see cref="IElementHandlerFactoryPlatform"/>: Core asks it for a handler once
/// per live Enter of every element; it answers from an <see cref="ElementHandlerRegistry"/>.
/// A handler that cannot be created is logged and the element stays on Core's own path (null).
/// </summary>
internal sealed class ElementHandlerFactory : IElementHandlerFactoryPlatform
{
    private readonly ILogger _log;

    /// <summary>Creates the factory over a registry.</summary>
    /// <param name="registry">The registrations.</param>
    /// <param name="log">Where handler-creation failures are logged (may be null).</param>
    internal ElementHandlerFactory(ElementHandlerRegistry registry, ILogger log = null)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _log = log;
    }

    /// <summary>The registrations this factory answers from.</summary>
    internal ElementHandlerRegistry Registry { get; }

    /// <summary>How many handlers this factory created (diagnostics).</summary>
    internal int CreatedCount { get; private set; }

    /// <inheritdoc />
    public IElementHandler CreateHandler(UIElement element)
    {
        if (element == null)
        {
            return null;
        }

        try
        {
            var handler = Registry.CreateHandler(element);
            if (handler != null)
            {
                CreatedCount++;
            }

            return handler;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _log?.LogError(exception, "Creating the element handler of {Element} failed; it stays on Core's own path.", element.GetType().FullName);
            return null;
        }
    }
}
