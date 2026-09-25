using System.Collections.Generic;

namespace GitHubIssueFinder.GitHub;

//The body of a search API response.
internal sealed class SearchResponseDto
{
    public int TotalCount { get; set; }

    public bool IncompleteResults { get; set; }

    public List<SearchItemDto> Items { get; set; }
}
