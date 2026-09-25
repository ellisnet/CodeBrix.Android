using System;

namespace GitHubIssueFinder.GitHub;

//The pull_request member. Its mere presence marks the item as a pull request; the merge
//time inside it separates a merged pull request from one that was closed unmerged.
internal sealed class PullRequestDto
{
    public DateTimeOffset? MergedAt { get; set; }
}
