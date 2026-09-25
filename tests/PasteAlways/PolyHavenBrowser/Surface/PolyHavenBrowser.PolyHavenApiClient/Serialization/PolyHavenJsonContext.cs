using System.Text.Json.Serialization;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Source-generated JSON serialization context for Poly Haven API response types.
/// The API uses snake_case property names (e.g. <c>date_published</c>).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Dictionary<string, PolyHavenAsset>))]
[JsonSerializable(typeof(PolyHavenAsset))]
[JsonSerializable(typeof(PolyHavenAuthor))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, int>))]
internal sealed partial class PolyHavenJsonContext : JsonSerializerContext;
