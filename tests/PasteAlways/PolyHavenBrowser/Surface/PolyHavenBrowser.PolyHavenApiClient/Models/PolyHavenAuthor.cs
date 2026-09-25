using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Information about a Poly Haven asset author, as returned by the <c>/author/{id}</c> endpoint.
/// </summary>
public class PolyHavenAuthor
{
    /// <summary>The author's display name.</summary>
    public string? Name { get; set; }

    /// <summary>A link to the author's website or portfolio, when available.</summary>
    public string? Link { get; set; }

    /// <summary>The author's public email address, when available.</summary>
    public string? Email { get; set; }

    /// <summary>A donation link or identifier for the author, when available.</summary>
    public string? Donate { get; set; }

    /// <summary>
    /// Any additional response fields that do not map to a typed property
    /// (for example <c>encryptedEmail</c>), preserved as raw JSON.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
