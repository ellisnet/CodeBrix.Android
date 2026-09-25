using System.Text.Json.Serialization;

namespace GitHubIssueFinder.GitHub;

//Source-generated serialization for every shape the library reads. GitHub names its members
//in snake_case, so one naming policy covers the whole set and no member needs an attribute.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SearchResponseDto))]
[JsonSerializable(typeof(RepositoryDto[]))]
[JsonSerializable(typeof(ErrorResponseDto))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
