using System;
using System.Collections.Generic;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// One issue or pull request returned by a search, flattened into the values the
/// application shows in a result row.
/// </summary>
public sealed class IssueItem
{
    /// <summary>The GitHub identifier of the item.</summary>
    public long Id { get; set; }

    /// <summary>The number shown after the hash in the repository, for example 1234.</summary>
    public int Number { get; set; }

    /// <summary>The title text.</summary>
    public string Title { get; set; }

    /// <summary>The address of the item on the GitHub website.</summary>
    public string HtmlUrl { get; set; }

    /// <summary>The owning repository in "owner/name" form.</summary>
    public string RepositoryFullName { get; set; }

    /// <summary>The address of the owning repository on the GitHub website.</summary>
    public string RepositoryHtmlUrl { get; set; }

    /// <summary>Whether the item is an issue or a pull request.</summary>
    public IssueKind Kind { get; set; }

    /// <summary>The display state of the item.</summary>
    public IssueState State { get; set; }

    /// <summary>The login of the account that opened the item.</summary>
    public string AuthorLogin { get; set; }

    /// <summary>When the item was opened.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the item last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the item was closed, or null while it is open.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>How many comments the item has.</summary>
    public int CommentCount { get; set; }

    /// <summary>The logins of every account assigned to the item; empty when nobody is assigned.</summary>
    public IReadOnlyList<string> AssigneeLogins { get; set; }

    /// <summary>The milestone title, or null when the item has no milestone.</summary>
    public string MilestoneTitle { get; set; }

    /// <summary>The labels attached to the item; empty when it has none.</summary>
    public IReadOnlyList<IssueLabel> Labels { get; set; }
}
