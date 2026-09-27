using System.Net;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Thrown when a Poly Haven API request fails, or when a response cannot be interpreted.
/// </summary>
public class PolyHavenApiException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public PolyHavenApiException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and inner exception.</summary>
    public PolyHavenApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with a message and HTTP response details.</summary>
    public PolyHavenApiException(string message, HttpStatusCode? statusCode, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP status code of the failed response, when the failure came from an HTTP response.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>The body of the failed response, when one could be read.</summary>
    public string? ResponseBody { get; }
}
