namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Progress information reported while downloading a file.
/// </summary>
/// <param name="BytesReceived">The number of bytes received so far.</param>
/// <param name="TotalBytes">The total number of bytes expected, when known (from the response's <c>Content-Length</c> header or the file's advertised size); otherwise <see langword="null"/>.</param>
public readonly record struct PolyHavenDownloadProgress(long BytesReceived, long? TotalBytes)
{
    /// <summary>The completion percentage (0–100), or <see langword="null"/> when the total size is unknown.</summary>
    public double? PercentComplete =>
        TotalBytes is > 0 ? BytesReceived * 100.0 / TotalBytes.Value : null;
}
