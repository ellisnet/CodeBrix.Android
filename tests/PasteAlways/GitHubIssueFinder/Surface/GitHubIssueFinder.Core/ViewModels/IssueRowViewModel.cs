using CodeBrix.Platform.Simple;
using GitHubIssueFinder.GitHub;
using GitHubIssueFinder.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace GitHubIssueFinder.ViewModels;

/// <summary>
/// One issue or pull request, formatted for its row. Everything the row draws is worked out once,
/// here, so the template binds to plain strings and the list stays cheap however long it grows.
/// The row holds no reference to its owner: opening its page in the browser is a delegate the
/// owner handed it.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class IssueRowViewModel : SimpleViewModel
{
    private readonly Func<string, Task> _openUrlAsync;
    private readonly ColorRole _stateRole;
    private SimpleCommand _openCommand;

    /// <summary>
    /// Builds the row for one issue or pull request.
    /// </summary>
    /// <param name="item">The issue or pull request as GitHub reported it.</param>
    /// <param name="palette">The scheme in force when the row was built.</param>
    /// <param name="showAssignees">
    /// True to name the assignees on the meta line, which is wanted only when the search asked
    /// for a particular assignee.
    /// </param>
    /// <param name="now">The moment the relative times are measured from.</param>
    /// <param name="openUrlAsync">Opens a URL in the host's browser.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public IssueRowViewModel(IssueItem item, ColorSchemePalette palette, bool showAssignees,
        DateTimeOffset now, Func<string, Task> openUrlAsync)
    {
        if (item == null) { throw new ArgumentNullException(nameof(item)); }

        _openUrlAsync = openUrlAsync;

        Url = item.HtmlUrl ?? string.Empty;
        Title = item.Title ?? string.Empty;
        IsPullRequest = item.Kind == IssueKind.PullRequest;
        PullRequestChipVisibility = GetVisibility(IsPullRequest);

        CommentCountText = item.CommentCount.ToString("N0", CultureInfo.InvariantCulture);
        CommentVisibility = GetVisibility(item.CommentCount > 0);

        MetaText = BuildMeta(item, showAssignees, now);
        MetaToolTip = BuildToolTip(item);

        (StateGlyph, _stateRole) = DescribeState(item);
        StateBrush = new SolidColorBrush();

        var labels = new List<IssueLabelViewModel>();
        if (item.Labels != null)
        {
            foreach (var label in item.Labels)
            {
                labels.Add(new IssueLabelViewModel(label, palette));
            }
        }

        Labels = labels;
        LabelsVisibility = GetVisibility(labels.Count > 0);

        ApplyPalette(palette);
    }

    /// <summary>The page this row opens.</summary>
    public string Url { get; }

    /// <summary>The issue or pull request title.</summary>
    public string Title { get; }

    /// <summary>True when this row is a pull request rather than an issue.</summary>
    public bool IsPullRequest { get; }

    /// <summary>Whether the "PR" chip is drawn.</summary>
    public Visibility PullRequestChipVisibility { get; }

    /// <summary>
    /// The line under the title, for example
    /// "#1234 opened 3 days ago by octocat - updated 2 hours ago - milestone: v2.0".
    /// </summary>
    public string MetaText { get; }

    /// <summary>The absolute dates, shown when the pointer rests on the row.</summary>
    public string MetaToolTip { get; }

    /// <summary>The number of comments, as text.</summary>
    public string CommentCountText { get; }

    /// <summary>Whether the comment count is drawn at all.</summary>
    public Visibility CommentVisibility { get; }

    /// <summary>The state glyph: an open ring, a check, a slash or a cross.</summary>
    public string StateGlyph { get; }

    /// <summary>The colour of the state glyph, which follows the scheme.</summary>
    public Brush StateBrush { get; }

    /// <summary>The label pills, in the order GitHub returned them.</summary>
    public IReadOnlyList<IssueLabelViewModel> Labels { get; }

    /// <summary>Whether there are any labels to draw.</summary>
    public Visibility LabelsVisibility { get; }

    /// <summary>
    /// Opens this row's page in the host's browser. Living on the row keeps the template's
    /// binding a plain one, because a template binds to its own item.
    /// </summary>
    public SimpleCommand OpenCommand => _openCommand ??=
        new SimpleCommand((Func<object, Task>)(_ => OpenAsync()));

    /// <summary>
    /// Re-tints the state glyph and every label pill for another scheme.
    /// </summary>
    /// <param name="palette">The scheme now in force.</param>
    public void ApplyPalette(ColorSchemePalette palette)
    {
        if (palette == null) { return; }

        PaletteBrushes.Repoint(StateBrush as SolidColorBrush, palette[_stateRole]);
        foreach (var label in Labels)
        {
            label.ApplyPalette(palette);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var command = _openCommand;
            _openCommand = null;
            command?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Task OpenAsync() => _openUrlAsync == null ? Task.CompletedTask : _openUrlAsync(Url);

    //The glyph and the colour role for one row's state. A pull request has more states than an
    //issue does, and the two share the open ring.
    private static (string Glyph, ColorRole Role) DescribeState(IssueItem item)
    {
        if (item.Kind == IssueKind.PullRequest)
        {
            return item.State switch
            {
                IssueState.Draft => (Glyphs.DraftPullRequest, ColorRole.Neutral),
                IssueState.Merged => (Glyphs.MergedPullRequest, ColorRole.Done),
                IssueState.Closed => (Glyphs.ClosedPullRequest, ColorRole.Danger),
                IssueState.NotPlanned => (Glyphs.ClosedPullRequest, ColorRole.Neutral),
                _ => (Glyphs.OpenIssue, ColorRole.Success),
            };
        }

        return item.State switch
        {
            IssueState.Closed => (Glyphs.ClosedIssue, ColorRole.Done),
            IssueState.NotPlanned => (Glyphs.NotPlanned, ColorRole.Neutral),
            IssueState.Merged => (Glyphs.ClosedIssue, ColorRole.Done),
            _ => (Glyphs.OpenIssue, ColorRole.Success),
        };
    }

    private static string BuildMeta(IssueItem item, bool showAssignees, DateTimeOffset now)
    {
        var text = new StringBuilder();
        text.Append('#').Append(item.Number.ToString(CultureInfo.InvariantCulture));
        text.Append(" opened ").Append(RelativeTime.Describe(item.CreatedAt, now));

        if (!string.IsNullOrWhiteSpace(item.AuthorLogin))
        {
            text.Append(" by ").Append(item.AuthorLogin);
        }

        text.Append(" · updated ").Append(RelativeTime.Describe(item.UpdatedAt, now));

        if (!string.IsNullOrWhiteSpace(item.MilestoneTitle))
        {
            text.Append(" · milestone: ").Append(item.MilestoneTitle);
        }

        if (showAssignees && item.AssigneeLogins != null && item.AssigneeLogins.Count > 0)
        {
            text.Append(" · assigned to ").Append(string.Join(", ", item.AssigneeLogins));
        }

        return text.ToString();
    }

    private static string BuildToolTip(IssueItem item)
    {
        var text = new StringBuilder();
        text.Append("Opened ").Append(item.CreatedAt.ToLocalTime().ToString("f", CultureInfo.CurrentCulture));
        text.Append("\nUpdated ").Append(item.UpdatedAt.ToLocalTime().ToString("f", CultureInfo.CurrentCulture));

        if (item.ClosedAt.HasValue)
        {
            text.Append("\nClosed ")
                .Append(item.ClosedAt.Value.ToLocalTime().ToString("f", CultureInfo.CurrentCulture));
        }

        return text.ToString();
    }
}
