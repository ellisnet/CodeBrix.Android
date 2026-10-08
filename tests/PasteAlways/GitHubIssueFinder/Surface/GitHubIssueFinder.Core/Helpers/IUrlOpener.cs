using System;
using System.Threading.Tasks;

namespace GitHubIssueFinder.Helpers;

/// <summary>
/// Opens a page in the host's browser. Nothing in the application registers one, so the view
/// model falls back to the platform launcher; an alternate host (a test head, for example) can
/// register its own in the services to stand in for the browser.
/// </summary>
public interface IUrlOpener
{
    /// <summary>
    /// Opens <paramref name="uri"/> in the host's browser.
    /// </summary>
    /// <param name="uri">The absolute address to open.</param>
    /// <returns>True when the page was handed to a browser; false when none was available.</returns>
    Task<bool> OpenAsync(Uri uri);
}
