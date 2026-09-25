namespace GitHubIssueFinder.GitHub;

//One entry of the core API's repository list for an owner.
internal sealed class RepositoryDto
{
    public string Name { get; set; }

    public string FullName { get; set; }

    public string HtmlUrl { get; set; }

    public bool Fork { get; set; }

    public bool Archived { get; set; }

    public bool HasIssues { get; set; }

    public int OpenIssuesCount { get; set; }
}
