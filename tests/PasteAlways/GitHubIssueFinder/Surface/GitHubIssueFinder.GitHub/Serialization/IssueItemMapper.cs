using System;
using System.Collections.Generic;

namespace GitHubIssueFinder.GitHub;

//Turns the wire shape into the flattened item the application shows. Every member is
//optional on the wire, so every read here copes with a missing one.
internal static class IssueItemMapper
{
    internal const string RepositoryUrlPrefix = "https://github.com/";

    private const string ClosedState = "closed";
    private const string NotPlannedReason = "not_planned";

    internal static IReadOnlyList<IssueItem> MapItems(IReadOnlyList<SearchItemDto> items)
    {
        var mapped = new List<IssueItem>(items == null ? 0 : items.Count);
        if (items == null) { return mapped; }

        foreach (var dto in items)
        {
            if (dto == null) { continue; }
            mapped.Add(Map(dto));
        }

        return mapped;
    }

    internal static IssueItem Map(SearchItemDto dto)
    {
        if (dto == null) { return null; }

        var repositoryFullName = ExtractRepositoryFullName(dto.RepositoryUrl);
        var kind = dto.PullRequest == null ? IssueKind.Issue : IssueKind.PullRequest;

        return new IssueItem
        {
            Id = dto.Id,
            Number = dto.Number,
            Title = dto.Title,
            HtmlUrl = dto.HtmlUrl,
            RepositoryFullName = repositoryFullName,
            RepositoryHtmlUrl = repositoryFullName == null ? null : RepositoryUrlPrefix + repositoryFullName,
            Kind = kind,
            State = MapState(dto, kind),
            AuthorLogin = dto.User == null ? null : dto.User.Login,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            ClosedAt = dto.ClosedAt,
            CommentCount = dto.Comments,
            AssigneeLogins = MapAssignees(dto.Assignees),
            MilestoneTitle = dto.Milestone == null ? null : dto.Milestone.Title,
            Labels = MapLabels(dto.Labels),
        };
    }

    //An issue closed as "not planned" reads differently from one that was fixed, and a pull
    //request has three closed endings of its own, so the display state is worked out here
    //rather than left to the caller.
    internal static IssueState MapState(SearchItemDto dto, IssueKind kind)
    {
        if (dto == null) { return IssueState.Open; }

        var isClosed = string.Equals(dto.State, ClosedState, StringComparison.OrdinalIgnoreCase);

        if (kind == IssueKind.PullRequest)
        {
            if (!isClosed)
            {
                return dto.Draft.HasValue && dto.Draft.Value ? IssueState.Draft : IssueState.Open;
            }

            var merged = dto.PullRequest != null && dto.PullRequest.MergedAt.HasValue;
            return merged ? IssueState.Merged : IssueState.Closed;
        }

        if (!isClosed) { return IssueState.Open; }

        return string.Equals(dto.StateReason, NotPlannedReason, StringComparison.OrdinalIgnoreCase)
            ? IssueState.NotPlanned
            : IssueState.Closed;
    }

    //The search response never names the repository directly; it gives the API address of
    //the repository, whose last two segments are the owner and the name.
    internal static string ExtractRepositoryFullName(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl)) { return null; }

        var trimmed = repositoryUrl.Trim().TrimEnd('/');
        var segments = trimmed.Split('/');
        if (segments.Length < 2) { return null; }

        var owner = segments[segments.Length - 2];
        var name = segments[segments.Length - 1];
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(name)) { return null; }

        return owner + "/" + name;
    }

    private static IReadOnlyList<string> MapAssignees(List<UserDto> assignees)
    {
        var logins = new List<string>(assignees == null ? 0 : assignees.Count);
        if (assignees == null) { return logins; }

        foreach (var assignee in assignees)
        {
            if (assignee == null || string.IsNullOrEmpty(assignee.Login)) { continue; }
            logins.Add(assignee.Login);
        }

        return logins;
    }

    private static IReadOnlyList<IssueLabel> MapLabels(List<LabelDto> labels)
    {
        var mapped = new List<IssueLabel>(labels == null ? 0 : labels.Count);
        if (labels == null) { return mapped; }

        foreach (var label in labels)
        {
            if (label == null) { continue; }
            mapped.Add(new IssueLabel
            {
                Name = label.Name,
                //An unreadable colour is carried through as it arrived: the caller draws the
                //pill and is the only part that knows what to fall back to.
                ColorHex = label.Color,
                Description = label.Description,
            });
        }

        return mapped;
    }
}
