using System.Net;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Thrown when the Poly Haven API returns 404 Not Found for a requested resource
/// (for example an unknown asset ID or author name).
/// </summary>
public class PolyHavenNotFoundException : PolyHavenApiException
{
    /// <summary>Creates the exception for the named resource.</summary>
    /// <param name="resource">A description of the resource that was not found (for example <c>asset 'foo'</c>).</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="responseBody">The body of the response, when one could be read.</param>
    public PolyHavenNotFoundException(string resource, HttpStatusCode? statusCode, string? responseBody)
        : base($"The Poly Haven API could not find {resource}.", statusCode, responseBody)
    {
        Resource = resource;
    }

    /// <summary>A description of the resource that was not found.</summary>
    public string Resource { get; }
}
