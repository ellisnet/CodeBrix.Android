using System;
using System.Collections.Generic;

namespace GitHubIssueFinder.GitHub;

//One entry of the search API's items array. Only the members the application shows are
//declared; every other member of the response is ignored.
internal sealed class SearchItemDto
{
    public long Id { get; set; }

    public int Number { get; set; }

    public string Title { get; set; }

    public string HtmlUrl { get; set; }

    public string RepositoryUrl { get; set; }

    public string State { get; set; }

    public string StateReason { get; set; }

    public UserDto User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public int Comments { get; set; }

    public List<UserDto> Assignees { get; set; }

    public MilestoneDto Milestone { get; set; }

    public List<LabelDto> Labels { get; set; }

    public PullRequestDto PullRequest { get; set; }

    public bool? Draft { get; set; }
}
