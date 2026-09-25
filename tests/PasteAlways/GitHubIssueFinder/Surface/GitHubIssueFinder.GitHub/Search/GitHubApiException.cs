using System;
using System.Net;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// The one exception type the library raises for a call GitHub refused or answered with
/// something the library could not use. It carries enough to write a human sentence on the
/// application's status line without reaching for a stack trace.
/// </summary>
public sealed class GitHubApiException : Exception
{
    /// <summary>Initializes a new instance with a message only.</summary>
    /// <param name="message">The human sentence describing the failure.</param>
    public GitHubApiException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and the error that caused it.</summary>
    /// <param name="message">The human sentence describing the failure.</param>
    /// <param name="innerException">The error that caused this one.</param>
    public GitHubApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance describing a refused or unusable response.</summary>
    /// <param name="message">The human sentence describing the failure.</param>
    /// <param name="statusCode">The status code GitHub answered with.</param>
    /// <param name="requestUrl">The address that was called.</param>
    /// <param name="gitHubMessage">The message member of the error body, when there was one.</param>
    /// <param name="rateLimitResetAt">When the exhausted rate-limit pool refills, when the refusal was a rate limit.</param>
    public GitHubApiException(string message, HttpStatusCode statusCode, string requestUrl,
        string gitHubMessage, DateTimeOffset? rateLimitResetAt = null)
        : base(message)
    {
        StatusCode = statusCode;
        RequestUrl = requestUrl;
        GitHubMessage = gitHubMessage;
        RateLimitResetAt = rateLimitResetAt;
    }

    /// <summary>Initializes a new instance describing a refused or unusable response, with a cause.</summary>
    /// <param name="message">The human sentence describing the failure.</param>
    /// <param name="statusCode">The status code GitHub answered with.</param>
    /// <param name="requestUrl">The address that was called.</param>
    /// <param name="gitHubMessage">The message member of the error body, when there was one.</param>
    /// <param name="rateLimitResetAt">When the exhausted rate-limit pool refills, when the refusal was a rate limit.</param>
    /// <param name="innerException">The error that caused this one.</param>
    public GitHubApiException(string message, HttpStatusCode statusCode, string requestUrl,
        string gitHubMessage, DateTimeOffset? rateLimitResetAt, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RequestUrl = requestUrl;
        GitHubMessage = gitHubMessage;
        RateLimitResetAt = rateLimitResetAt;
    }

    /// <summary>The status code GitHub answered with; zero when the call never got that far.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The address that was called, or null when it is not known.</summary>
    public string RequestUrl { get; }

    /// <summary>The message member of GitHub's error body, or null when there was none.</summary>
    public string GitHubMessage { get; }

    /// <summary>When the exhausted rate-limit pool refills, or null when this was not a rate-limit refusal.</summary>
    public DateTimeOffset? RateLimitResetAt { get; }
}
