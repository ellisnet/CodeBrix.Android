using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// The default <see cref="IPolyHavenApiClientFactory"/>. Create one factory for the lifetime
/// of the application, call <see cref="GetClient"/> whenever a client is needed, and dispose
/// each client when finished with it.
/// <para>
/// When constructed standalone, the factory owns a single shared
/// <see cref="SocketsHttpHandler"/> with connection pooling and periodic connection recycling
/// (so DNS changes are honored); disposing a client never tears down the shared pool.
/// When constructed with an <see cref="IHttpClientFactory"/> (as done by
/// <see cref="PolyHavenServiceCollectionExtensions.AddPolyHavenApiClient"/>), handler pooling
/// is delegated to it instead.
/// </para>
/// </summary>
public sealed class DefaultPolyHavenClientFactory : IPolyHavenApiClientFactory, IDisposable
{
    /// <summary>
    /// The named-<see cref="HttpClient"/> name used when the factory is backed by an
    /// <see cref="IHttpClientFactory"/>.
    /// </summary>
    public const string HttpClientName = "PolyHavenApiClient";

    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(15);

    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpMessageHandler? _explicitHandler;
    private readonly PolyHavenClientOptions _options;
    private readonly object _handlerLock = new();
    private SocketsHttpHandler? _sharedHandler;
    private bool _disposed;

    /// <summary>Creates a standalone factory with default options.</summary>
    public DefaultPolyHavenClientFactory()
        : this((PolyHavenClientOptions?)null)
    {
    }

    /// <summary>Creates a standalone factory with the given options.</summary>
    /// <param name="options">The client options, or <see langword="null"/> for defaults. The options are copied.</param>
    public DefaultPolyHavenClientFactory(PolyHavenClientOptions? options)
    {
        _options = options?.Clone() ?? new PolyHavenClientOptions();
    }

    /// <summary>
    /// Creates a factory that obtains its <see cref="HttpClient"/> instances from an
    /// <see cref="IHttpClientFactory"/>, using the client name <see cref="HttpClientName"/>.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory to obtain clients from.</param>
    /// <param name="options">The client options, or <see langword="null"/> for defaults. The options are copied.</param>
    public DefaultPolyHavenClientFactory(IHttpClientFactory httpClientFactory, PolyHavenClientOptions? options = null)
        : this(options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Creates a factory over an explicit message handler. The handler is not disposed by
    /// the factory or its clients. Intended for testing.
    /// </summary>
    internal DefaultPolyHavenClientFactory(HttpMessageHandler messageHandler, PolyHavenClientOptions? options = null)
        : this(options)
    {
        _explicitHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
    }

    /// <inheritdoc />
    public IPolyHavenApiClient GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var httpClient = _httpClientFactory?.CreateClient(HttpClientName)
            ?? new HttpClient(_explicitHandler ?? GetOrCreateSharedHandler(), disposeHandler: false);

        // Metadata calls enforce their own timeout; downloads are governed by the caller's
        // CancellationToken, so the HttpClient-level timeout must not cut long downloads short.
        httpClient.Timeout = Timeout.InfiniteTimeSpan;

        if (!string.IsNullOrWhiteSpace(_options.UserAgent)
            && !httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(_options.UserAgent))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        }

        return new RestPolyHavenApiClient(httpClient, _options.Clone());
    }

    private SocketsHttpHandler GetOrCreateSharedHandler()
    {
        lock (_handlerLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _sharedHandler ??= new SocketsHttpHandler
            {
                PooledConnectionLifetime = PooledConnectionLifetime,
                AutomaticDecompression = DecompressionMethods.All,
            };
        }
    }

    /// <summary>
    /// Disposes the factory's shared connection pool (standalone mode only). Clients already
    /// created remain usable only for requests whose connections are established; obtain all
    /// clients before disposing the factory.
    /// </summary>
    public void Dispose()
    {
        lock (_handlerLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sharedHandler?.Dispose();
            _sharedHandler = null;
        }
    }
}
