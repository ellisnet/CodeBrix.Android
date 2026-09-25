using System;
using System.Reflection;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// The settings a search service is built with: how it identifies itself, where it calls,
/// how hard it is willing to lean on the rate limits, and how long a single request may take.
/// </summary>
public sealed class GitHubSearchOptions
{
    /// <summary>The User-Agent used when the library's own version cannot be read.</summary>
    public const string FallbackUserAgent = "GitHubIssueFinder/1.0";

    /// <summary>
    /// The value sent as the User-Agent header. GitHub refuses requests that carry none.
    /// Defaults to "GitHubIssueFinder/" followed by this library's informational version.
    /// </summary>
    public string UserAgent { get; set; } = BuildDefaultUserAgent();

    /// <summary>The root address of the GitHub REST API.</summary>
    public Uri BaseAddress { get; set; } = new Uri("https://api.github.com/");

    /// <summary>
    /// The most search calls the library will make in any one minute. GitHub allows ten to an
    /// anonymous caller, so the default of nine leaves one for everything else on the address.
    /// </summary>
    public int SearchCeilingPerMinute { get; set; } = 9;

    /// <summary>
    /// The most core calls the library will make in any one hour. GitHub allows sixty to an
    /// anonymous caller, so the default of fifty-nine leaves one spare.
    /// </summary>
    public int CoreCeilingPerHour { get; set; } = 59;

    /// <summary>How long one request may take before it is abandoned.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    //The stretch of time the search ceiling is counted over. GitHub measures search calls
    //by the minute; a test shortens it so a run at the ceiling finishes in a moment.
    internal TimeSpan SearchWindow { get; set; } = TimeSpan.FromMinutes(1);

    //The stretch of time the core ceiling is counted over. GitHub measures core calls by
    //the hour.
    internal TimeSpan CoreWindow { get; set; } = TimeSpan.FromHours(1);

    private static string BuildDefaultUserAgent()
    {
        try
        {
            var attribute = typeof(GitHubSearchOptions).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var version = attribute == null ? null : attribute.InformationalVersion;
            if (string.IsNullOrWhiteSpace(version)) { return FallbackUserAgent; }

            //A build that records the source revision appends it after a plus sign;
            //the header carries the version only.
            var plus = version.IndexOf('+');
            if (plus >= 0) { version = version.Substring(0, plus); }

            return string.IsNullOrWhiteSpace(version)
                ? FallbackUserAgent
                : "GitHubIssueFinder/" + version;
        }
        catch (Exception)
        {
            //Reading an attribute must never stop the library being constructed.
            return FallbackUserAgent;
        }
    }
}
