namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Thrown when a downloaded file's MD5 checksum does not match the checksum advertised
/// by the Poly Haven API, indicating a corrupted or incomplete download.
/// </summary>
public class PolyHavenIntegrityException : PolyHavenApiException
{
    /// <summary>Creates the exception for a checksum mismatch.</summary>
    /// <param name="expectedMd5">The MD5 checksum advertised by the API.</param>
    /// <param name="actualMd5">The MD5 checksum computed from the downloaded bytes.</param>
    /// <param name="url">The URL of the file that was downloaded.</param>
    public PolyHavenIntegrityException(string expectedMd5, string actualMd5, string url)
        : base($"MD5 checksum mismatch for downloaded file '{url}': expected {expectedMd5} but got {actualMd5}.")
    {
        ExpectedMd5 = expectedMd5;
        ActualMd5 = actualMd5;
        Url = url;
    }

    /// <summary>The MD5 checksum advertised by the API.</summary>
    public string ExpectedMd5 { get; }

    /// <summary>The MD5 checksum computed from the downloaded bytes.</summary>
    public string ActualMd5 { get; }

    /// <summary>The URL of the file that was downloaded.</summary>
    public string Url { get; }
}
