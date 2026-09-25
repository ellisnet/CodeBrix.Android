using CodeBrix.Platform.Simple;
using GitHubIssueFinder.Theming;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace GitHubIssueFinder.ViewModels;

/// <summary>
/// One repository's block of results: a header carrying the repository name and how many rows are
/// under it, and the rows themselves. The group holds no reference to its owner; opening the
/// repository in the browser is a delegate the owner handed it.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class RepositoryGroupViewModel : SimpleViewModel
{
    private readonly Func<string, Task> _openUrlAsync;
    private SimpleCommand _openCommand;

    /// <summary>
    /// Builds the group for one repository.
    /// </summary>
    /// <param name="fullName">The repository's owner and name, for example "mono/SkiaSharp".</param>
    /// <param name="htmlUrl">The repository's page.</param>
    /// <param name="openUrlAsync">Opens a URL in the host's browser.</param>
    public RepositoryGroupViewModel(string fullName, string htmlUrl, Func<string, Task> openUrlAsync)
    {
        FullName = fullName ?? string.Empty;
        Url = htmlUrl ?? string.Empty;
        _openUrlAsync = openUrlAsync;
        Rows = new ObservableCollection<IssueRowViewModel>();
        CountText = "0";
    }

    /// <summary>The repository's owner and name.</summary>
    public string FullName { get; }

    /// <summary>The repository's page.</summary>
    public string Url { get; }

    /// <summary>The rows under this repository, in the order GitHub returned them.</summary>
    public ObservableCollection<IssueRowViewModel> Rows { get; }

    /// <summary>How many rows are under this repository, as the header's count pill shows it.</summary>
    public string CountText
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Opens this repository's page in the host's browser.</summary>
    public SimpleCommand OpenCommand => _openCommand ??=
        new SimpleCommand((Func<object, Task>)(_ => OpenAsync()));

    /// <summary>
    /// Adds a row to the end of this group and refreshes the header's count.
    /// </summary>
    /// <param name="row">The row to add.</param>
    public void Add(IssueRowViewModel row)
    {
        if (row == null) { return; }

        Rows.Add(row);
        CountText = Rows.Count.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Re-tints every row in this group for another scheme.
    /// </summary>
    /// <param name="palette">The scheme now in force.</param>
    public void ApplyPalette(ColorSchemePalette palette)
    {
        if (palette == null) { return; }

        foreach (var row in Rows)
        {
            row.ApplyPalette(palette);
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

            foreach (var row in Rows)
            {
                row.Dispose();
            }

            Rows.Clear();
        }

        base.Dispose(disposing);
    }

    private Task OpenAsync() => _openUrlAsync == null ? Task.CompletedTask : _openUrlAsync(Url);
}
