namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Configuration for <see cref="IPolyHavenApiClient"/> instances created by
/// <see cref="DefaultPolyHavenClientFactory"/>.
/// </summary>
public sealed class PolyHavenClientOptions
{
    /// <summary>The base address of the Poly Haven API. Defaults to <c>https://api.polyhaven.com/</c>.</summary>
    public string BaseAddress { get; set; } = "https://api.polyhaven.com/";

    /// <summary>
    /// The User-Agent header value sent with every request. Poly Haven asks API consumers
    /// to identify themselves; set this to your application name and version.
    /// </summary>
    public string UserAgent { get; set; } = "PolyHavenBrowser.PolyHavenApiClient/1.0";

    /// <summary>
    /// The timeout applied to metadata requests (asset lists, asset info, files, authors,
    /// categories, and types). Does not apply to file downloads or image fetches, which are
    /// governed solely by the caller's <see cref="CancellationToken"/>. Set to
    /// <see cref="TimeSpan.Zero"/> or a negative value to disable. Defaults to 100 seconds.
    /// </summary>
    public TimeSpan MetadataRequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Creates a copy of these options.</summary>
    public PolyHavenClientOptions Clone() => new()
    {
        BaseAddress = BaseAddress,
        UserAgent = UserAgent,
        MetadataRequestTimeout = MetadataRequestTimeout,
    };
}
