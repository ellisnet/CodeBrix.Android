using System.Collections.Generic;

namespace GitHubIssueFinder.GitHub;

//The body GitHub returns with a refused call.
internal sealed class ErrorResponseDto
{
    public string Message { get; set; }

    public List<ErrorDetailDto> Errors { get; set; }

    public string DocumentationUrl { get; set; }
}
