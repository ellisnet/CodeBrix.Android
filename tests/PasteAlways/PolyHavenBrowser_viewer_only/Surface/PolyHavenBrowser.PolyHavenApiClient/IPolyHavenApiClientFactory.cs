namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Creates <see cref="IPolyHavenApiClient"/> instances. Register a single factory for the
/// lifetime of the application and dispose each client obtained from
/// <see cref="GetClient"/> when finished with it; the factory manages the underlying HTTP
/// connection pool so that creating and disposing clients is cheap.
/// </summary>
public interface IPolyHavenApiClientFactory
{
    /// <summary>Creates a new Poly Haven API client. Dispose the client when finished.</summary>
    IPolyHavenApiClient GetClient();
}
