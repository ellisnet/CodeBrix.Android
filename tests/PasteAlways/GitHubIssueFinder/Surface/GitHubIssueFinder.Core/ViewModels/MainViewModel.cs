using CodeBrix.Platform.Simple;
using GitHubIssueFinder.GitHub;
using GitHubIssueFinder.Settings;
using GitHubIssueFinder.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubIssueFinder.ViewModels;

/// <summary>
/// What the page can do for the view model that the view model cannot do for itself. The page
/// owns the resource dictionaries and the element tree, so painting a scheme is its job; the view
/// model decides which scheme, and calls this.
/// </summary>
public interface IColorSchemeApplier
{
    /// <summary>
    /// Paints a colour scheme over the whole application.
    /// </summary>
    /// <param name="palette">The colours to paint.</param>
    /// <param name="baseIsDark">True when the scheme sits on a dark ground.</param>
    /// <param name="followSystem">
    /// True when the user asked to follow the operating system, in which case the element theme is
    /// left on its default so the platform keeps deciding it.
    /// </param>
    void Apply(ColorSchemePalette palette, bool baseIsDark, bool followSystem);
}

/// <summary>
/// The other half of the same bridge: what the page tells the view model about color schemes.
/// The page hands over the thing that paints them and reports the operating system's preference,
/// and reaches the view model through this interface rather than through its concrete type.
/// </summary>
public interface IManageColorScheme
{
    /// <summary>
    /// Gives the view model the page that paints schemes, and the operating system's current
    /// preference, then paints the chosen scheme once.
    /// </summary>
    /// <param name="applier">The page.</param>
    /// <param name="osPrefersDark">True when the operating system prefers a dark appearance.</param>
    void AttachSchemeApplier(IColorSchemeApplier applier, bool osPrefersDark);

    /// <summary>
    /// Tells the view model that the operating system's light or dark preference changed. It only
    /// repaints when the user is following the operating system.
    /// </summary>
    /// <param name="osPrefersDark">True when the operating system now prefers a dark appearance.</param>
    void OnSystemThemeChanged(bool osPrefersDark);
}

/// <summary>
/// The page's view model: the two logins and the closed-issues switch, the scheme picker, the
/// grouped results, and the search itself. Results arrive a page at a time while the search runs,
/// and every one of them is folded into the groups on the UI thread.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IManageColorScheme
{
    private const string StillLoading = " · still loading";
    private const string UnknownRepository = "(unknown repository)";

    private static readonly TimeSpan QuotaRecoveryInterval = TimeSpan.FromSeconds(1);

    private readonly IGitHubIssueSearchService _searchService;
    private readonly bool _ownsSearchService;
    private readonly Dictionary<string, RepositoryGroupViewModel> _groupsByRepository =
        new Dictionary<string, RepositoryGroupViewModel>(StringComparer.OrdinalIgnoreCase);

    private IColorSchemeApplier _schemeApplier;
    private ColorSchemeOptionViewModel _selectedScheme;
    private CancellationTokenSource _searchCts;
    private SimpleCommand _searchCommand;
    private SimpleCommand _cancelCommand;
    private Timer _quotaRecoveryTimer;
    private bool _isDisposed;

    private bool _includeClosed;
    private bool _osPrefersDark;
    private bool _hasSearched;
    private bool _searchFailed;
    private bool _showAssignees;
    private int _fetched;
    private int _openCount;
    private int _closedCount;
    private int _issueCount;
    private int _pullRequestCount;
    private int? _total;
    private DateTimeOffset _startedAt;
    private string _searchedOwner = string.Empty;
    private string _searchedAssignee = string.Empty;
    private bool _searchedIncludeClosed;

    /// <summary>
    /// Builds the page's view model: reads back what the user last typed, prepares the scheme
    /// picker and resolves the search service.
    /// </summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        _searchService = GetService<IGitHubIssueSearchService>();
        if (_searchService == null)
        {
            //Nothing registered the service, so the view model builds its own and is then the
            //thing that has to close its connection pool when it goes.
            _searchService = new GitHubIssueSearchService(new GitHubSearchOptions());
            _ownsSearchService = true;
        }

        Groups = new ObservableCollection<RepositoryGroupViewModel>();
        StatusBrush = new SolidColorBrush();
        SearchQuotaBackground = new SolidColorBrush();
        SearchQuotaBorderBrush = new SolidColorBrush();
        SearchQuotaForeground = new SolidColorBrush();

        Owner = SettingsService.Get(SettingKeys.Owner, string.Empty);
        Assignee = SettingsService.Get(SettingKeys.Assignee, string.Empty);
        _includeClosed = SettingsService.Get(SettingKeys.IncludeClosed, false);

        var stored = ColorSchemes.Parse(
            SettingsService.Get(SettingKeys.ColorScheme, nameof(ColorScheme.SystemDefault)));

        SchemeOptions = new ObservableCollection<ColorSchemeOptionViewModel>();
        foreach (var choice in ColorSchemes.Choices)
        {
            SchemeOptions.Add(new ColorSchemeOptionViewModel(choice, _osPrefersDark));
        }

        _selectedScheme = FindOption(stored);
        CurrentPalette = ColorSchemes.Get(ColorSchemes.Resolve(stored, _osPrefersDark));

        SearchQuotaText = "Search 9 of 9";
        CoreQuotaText = "Core 59 of 59";
        SetStatus("Ready.", SearchStatusKind.Idle);
        RepaintOwnBrushes();
    }

    #region | Bindable properties |

    /// <summary>The GitHub user or organisation whose repositories are searched.</summary>
    [AffectsCommands(nameof(SearchCommand))]
    [AffectsProperties(nameof(HelperText))]
    public string Owner
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The login the issues must be assigned to; empty means "assigned to nobody".</summary>
    [AffectsProperties(nameof(HelperText))]
    public string Assignee
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>Whether closed issues and pull requests are searched as well as open ones.</summary>
    [AffectsProperties(nameof(HelperText))]
    public bool IncludeClosed
    {
        get => _includeClosed;
        set
        {
            if (_includeClosed == value) { return; }

            _includeClosed = value;
            NotifyPropertyChanged(nameof(IncludeClosed));
            SettingsService.Set(SettingKeys.IncludeClosed, value);
        }
    }

    /// <summary>The five entries of the scheme picker.</summary>
    public ObservableCollection<ColorSchemeOptionViewModel> SchemeOptions { get; }

    /// <summary>The scheme the user picked. Choosing one paints it and remembers it.</summary>
    public ColorSchemeOptionViewModel SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (value == null || ReferenceEquals(_selectedScheme, value)) { return; }

            _selectedScheme = value;
            NotifyPropertyChanged(nameof(SelectedScheme));
            SettingsService.Set(SettingKeys.ColorScheme, value.Scheme);
            ApplyCurrentScheme();
        }
    }

    /// <summary>The colours currently painted, which new rows are built against.</summary>
    public ColorSchemePalette CurrentPalette { get; private set; }

    /// <summary>The repository groups, alphabetical, each holding its rows in arrival order.</summary>
    public ObservableCollection<RepositoryGroupViewModel> Groups { get; }

    /// <summary>The sentence on the status line.</summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>What the status line is saying, which decides its colour and glyph.</summary>
    public SearchStatusKind StatusKind
    {
        get;
        private set => SetEnumProperty(ref field, value);
    }

    /// <summary>The colour of the status line, which follows both the kind and the scheme.</summary>
    public Brush StatusBrush { get; }

    /// <summary>The glyph in front of the status line, empty when there is none.</summary>
    public string StatusGlyph
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Whether the status glyph is drawn.</summary>
    public Visibility StatusGlyphVisibility => GetVisibility(!string.IsNullOrEmpty(StatusGlyph));

    /// <summary>True while a search is running.</summary>
    [AffectsCommands(nameof(SearchCommand), nameof(CancelCommand))]
    [AffectsProperties(nameof(ProgressVisibility), nameof(WelcomeVisibility),
        nameof(EmptyVisibility), nameof(ResultsVisibility), nameof(SearchingVisibility),
        nameof(HeaderVisibility))]
    public bool IsSearching
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>True between asking a search to stop and it actually stopping.</summary>
    [AffectsCommands(nameof(SearchCommand), nameof(CancelCommand))]
    public bool IsCancelling
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>How far the search has got, from 0 to 100.</summary>
    public double ProgressValue
    {
        get => field;
        private set
        {
            //SetProperty has no double overload, so the comparison and the notification are
            //written out here.
            if (field.Equals(value)) { return; }
            field = value;
            NotifyPropertyChanged(nameof(ProgressValue));
        }
    }

    /// <summary>True while the total is unknown and the bar can only show that work is happening.</summary>
    public bool IsProgressIndeterminate
    {
        get;
        private set => SetProperty(ref field, value);
    } = true;

    /// <summary>Whether the progress bar is drawn at all.</summary>
    public Visibility ProgressVisibility => GetVisibility(IsSearching);

    /// <summary>
    /// Whether the "nothing searched yet" panel is drawn. It is also what a search that failed
    /// before it produced a single row falls back to, so the box is never an empty void: the
    /// failure itself is on the status line, where every failure in this application goes.
    /// </summary>
    public Visibility WelcomeVisibility =>
        GetVisibility(!_hasSearched || (_searchFailed && !IsSearching && Groups.Count == 0));

    /// <summary>Whether the "nothing found" panel is drawn.</summary>
    public Visibility EmptyVisibility =>
        GetVisibility(_hasSearched && !IsSearching && !_searchFailed && Groups.Count == 0);

    /// <summary>Whether the "contacting GitHub" panel is drawn.</summary>
    public Visibility SearchingVisibility => GetVisibility(IsSearching && Groups.Count == 0);

    /// <summary>Whether the grouped results are drawn.</summary>
    public Visibility ResultsVisibility => GetVisibility(Groups.Count > 0);

    /// <summary>
    /// Whether the counts strip at the top of the results box is drawn. Before the first search,
    /// and after one that failed with nothing to show for it, there is nothing to count.
    /// </summary>
    public Visibility HeaderVisibility =>
        GetVisibility(_hasSearched && !(_searchFailed && Groups.Count == 0));

    /// <summary>
    /// Whether the "widen the search" hint under the empty panel is drawn. There is nothing to
    /// widen when closed items are already being searched.
    /// </summary>
    public Visibility EmptyHintVisibility => GetVisibility(!_searchedIncludeClosed);

    /// <summary>The open count at the head of the results box, for example "42 open".</summary>
    public string HeaderOpenText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No results yet";

    /// <summary>
    /// The rest of the results-box header, for example " · 7 closed · 9 repositories · still
    /// loading". It is a second property so the open count can carry the weight and this part can
    /// stay quiet, which is how the design draws it.
    /// </summary>
    public string HeaderSummary
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The split at the right of the header, for example "Issues 31 · Pull requests 11".</summary>
    public string IssuePrSplit
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The sentence shown when a search found nothing.</summary>
    public string EmptyText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The search rate-limit pill, for example "Search 7 of 9".</summary>
    public string SearchQuotaText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The core rate-limit pill, for example "Core 58 of 59".</summary>
    public string CoreQuotaText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The face of the search quota pill, which warms up while the throttle is holding.</summary>
    public Brush SearchQuotaBackground { get; }

    /// <summary>The outline of the search quota pill.</summary>
    public Brush SearchQuotaBorderBrush { get; }

    /// <summary>The text of the search quota pill.</summary>
    public Brush SearchQuotaForeground { get; }

    /// <summary>The line under the search box that says, in words, what will be searched for.</summary>
    public string HelperText
    {
        get
        {
            var owner = (Owner ?? string.Empty).Trim();
            if (owner.Length == 0)
            {
                return "Type a GitHub user or organization to search their public repositories.";
            }

            var assignee = (Assignee ?? string.Empty).Trim();
            var scope = IncludeClosed ? "open and closed" : "open";
            var target = assignee.Length == 0 ? "no one" : assignee;
            return $"Searching {owner}'s public repositories for {scope} issues and pull requests "
                + $"assigned to {target}.";
        }
    }

    #endregion

    #region | Commands and their implementations |

    /// <summary>Runs the search.</summary>
    public SimpleCommand SearchCommand => _searchCommand ??=
        new SimpleCommand(CanSearch, (Func<object, Task>)(_ => DoSearch()));

    /// <summary>Stops the running search.</summary>
    public SimpleCommand CancelCommand => _cancelCommand ??=
        new SimpleCommand(CanCancel, DoCancel);

    private bool CanSearch() => !IsSearching && !string.IsNullOrWhiteSpace(Owner);

    private bool CanCancel() => IsSearching && !IsCancelling;

    private void DoCancel()
    {
        if (!CanCancel()) { return; }

        IsCancelling = true;
        SetStatus("Cancelling...", SearchStatusKind.Working);
        _searchCts?.Cancel();
    }

    private async Task DoSearch()
    {
        if (!CanSearch()) { return; }

        //Everything the run needs is copied out now, because the user is free to keep typing.
        var owner = (Owner ?? string.Empty).Trim();
        var assignee = (Assignee ?? string.Empty).Trim();
        var includeClosed = IncludeClosed;

        SettingsService.Set(SettingKeys.Owner, owner);
        SettingsService.Set(SettingKeys.Assignee, assignee);
        SettingsService.Set(SettingKeys.IncludeClosed, includeClosed);

        //Starting a search supersedes whatever was already running. The new token source is
        //published first, so the older run sees that it is no longer the current one and stays
        //quiet on its way out.
        var previous = _searchCts;
        var cancellation = new CancellationTokenSource();
        _searchCts = cancellation;
        previous?.Cancel();

        _searchedOwner = owner;
        _searchedAssignee = assignee;
        _searchedIncludeClosed = includeClosed;
        _showAssignees = assignee.Length > 0;
        _hasSearched = true;
        _searchFailed = false;
        _startedAt = DateTimeOffset.Now;
        ResetResults();

        IsSearching = true;
        IsCancelling = false;
        IsProgressIndeterminate = true;
        ProgressValue = 0d;
        SetStatus("Contacting GitHub...", SearchStatusKind.Working);

        //Created on the UI thread, so its callbacks already arrive there and must not be
        //marshalled a second time.
        var progress = new Progress<SearchProgress>(report =>
        {
            if (!ReferenceEquals(cancellation, _searchCts)) { return; }
            OnProgress(report);
        });

        var request = new IssueSearchRequest
        {
            Owner = owner,
            Assignee = assignee,
            IncludeClosed = includeClosed,
        };

        try
        {
            await foreach (var page in _searchService
                .SearchAsync(request, progress, cancellation.Token)
                .ConfigureAwait(false))
            {
                var arrived = page;
                InvokeOnMainThread(() =>
                {
                    if (!ReferenceEquals(cancellation, _searchCts)) { return; }
                    FoldPage(arrived);
                });
            }

            InvokeOnMainThread(() =>
            {
                if (!ReferenceEquals(cancellation, _searchCts)) { return; }
                CompleteSearch();
            });
        }
        catch (OperationCanceledException)
        {
            InvokeOnMainThread(() =>
            {
                if (!ReferenceEquals(cancellation, _searchCts)) { return; }
                SetStatus($"Cancelled after {CountPhrase()}.", SearchStatusKind.Cancelled);
            });
        }
        catch (GitHubApiException failure)
        {
            InvokeOnMainThread(() =>
            {
                if (!ReferenceEquals(cancellation, _searchCts)) { return; }
                _searchFailed = true;
                SetStatus(DescribeFailure(failure), SearchStatusKind.Failed);
                RefreshResultVisibility();
            });
        }
        catch (Exception failure)
        {
            InvokeOnMainThread(() =>
            {
                if (!ReferenceEquals(cancellation, _searchCts)) { return; }
                _searchFailed = true;
                SetStatus($"The search could not be completed: {failure.Message}",
                    SearchStatusKind.Failed);
                RefreshResultVisibility();
            });
        }
        finally
        {
            //Everything this run still has to say is already queued on the UI thread, so the
            //tidying up is queued behind it: clearing the field here would make the last page and
            //the closing sentence look as though a newer run had superseded them. Only the run
            //that is still the current one may turn the busy indicators off, and the token source
            //is disposed on the UI thread, after the field is cleared, so a cancel arriving in
            //the meantime can never reach a disposed one.
            InvokeOnMainThread(() =>
            {
                if (ReferenceEquals(cancellation, _searchCts))
                {
                    _searchCts = null;
                    IsSearching = false;
                    IsCancelling = false;
                    RefreshHeader();
                    RefreshResultVisibility();
                    StartQuotaRecovery();
                }

                cancellation.Dispose();
            });
        }
    }

    #endregion

    #region | Theming |

    /// <inheritdoc />
    public void AttachSchemeApplier(IColorSchemeApplier applier, bool osPrefersDark)
    {
        _schemeApplier = applier;
        _osPrefersDark = osPrefersDark;
        RefreshSchemeNames();
        ApplyCurrentScheme();
    }

    /// <inheritdoc />
    public void OnSystemThemeChanged(bool osPrefersDark)
    {
        if (_osPrefersDark == osPrefersDark) { return; }

        _osPrefersDark = osPrefersDark;
        RefreshSchemeNames();

        if (_selectedScheme != null && _selectedScheme.Scheme == ColorScheme.SystemDefault)
        {
            ApplyCurrentScheme();
        }
    }

    private void RefreshSchemeNames()
    {
        for (var index = 0; index < SchemeOptions.Count; index++)
        {
            var option = SchemeOptions[index];
            var wanted = ColorSchemes.DisplayName(option.Scheme, _osPrefersDark);
            if (string.Equals(option.DisplayName, wanted, StringComparison.Ordinal)) { continue; }

            //The entry is replaced rather than renamed, because the picker's closed face reads
            //its item once and would otherwise keep showing the old text.
            var replacement = new ColorSchemeOptionViewModel(option.Scheme, _osPrefersDark);
            var wasSelected = ReferenceEquals(_selectedScheme, option);
            SchemeOptions[index] = replacement;

            if (wasSelected)
            {
                _selectedScheme = replacement;
                NotifyPropertyChanged(nameof(SelectedScheme));
            }
        }
    }

    private void ApplyCurrentScheme()
    {
        var choice = _selectedScheme?.Scheme ?? ColorScheme.SystemDefault;
        var palette = ColorSchemes.Get(ColorSchemes.Resolve(choice, _osPrefersDark));
        CurrentPalette = palette;

        _schemeApplier?.Apply(palette, palette.BaseIsDark, choice == ColorScheme.SystemDefault);

        RepaintOwnBrushes();
        foreach (var group in Groups)
        {
            group.ApplyPalette(palette);
        }
    }

    //The brushes the view model owns rather than the resource dictionary: the status line, whose
    //colour depends on what it is saying, and the search quota pill, which warms up while the
    //throttle is holding.
    private void RepaintOwnBrushes()
    {
        var palette = CurrentPalette;
        if (palette == null) { return; }

        PaletteBrushes.Repoint(StatusBrush as SolidColorBrush, palette[StatusRole()]);

        var waiting = StatusKind == SearchStatusKind.Waiting;
        PaletteBrushes.Repoint(SearchQuotaBackground as SolidColorBrush,
            waiting ? palette.AttentionSubtle : palette.CanvasInset);
        PaletteBrushes.Repoint(SearchQuotaBorderBrush as SolidColorBrush,
            waiting ? palette.Attention : palette.Hairline);
        PaletteBrushes.Repoint(SearchQuotaForeground as SolidColorBrush,
            waiting ? palette.Attention : palette.TextSecondary);
    }

    private ColorRole StatusRole() => StatusKind switch
    {
        SearchStatusKind.Waiting => ColorRole.Attention,
        SearchStatusKind.Failed => ColorRole.Danger,
        _ => ColorRole.TextSecondary,
    };

    private ColorSchemeOptionViewModel FindOption(ColorScheme scheme)
    {
        foreach (var option in SchemeOptions)
        {
            if (option.Scheme == scheme) { return option; }
        }

        return SchemeOptions.Count > 0 ? SchemeOptions[0] : null;
    }

    #endregion

    #region | Building the results |

    private void ResetResults()
    {
        //The collection lives as long as the view model and is emptied in place rather than being
        //replaced, so the list is never handed a different collection to rebind to. Every row
        //goes, so the list returns to the top by itself, and nothing is left holding a row from
        //the run before.
        var previous = new List<RepositoryGroupViewModel>(Groups);
        _groupsByRepository.Clear();
        Groups.Clear();

        foreach (var group in previous)
        {
            group.Dispose();
        }

        _fetched = 0;
        _total = null;
        _openCount = 0;
        _closedCount = 0;
        _issueCount = 0;
        _pullRequestCount = 0;
        HeaderOpenText = "Searching...";
        HeaderSummary = string.Empty;
        IssuePrSplit = string.Empty;
        RefreshResultVisibility();
    }

    private void FoldPage(IssueSearchPage page)
    {
        if (page?.Items == null) { return; }

        var palette = CurrentPalette;
        var now = DateTimeOffset.Now;

        //The whole page is turned into rows and gathered by repository BEFORE anything reaches the
        //bound collections. A repository new to this search then arrives on screen with its rows
        //already in it: a group inserted empty and filled a moment later can be measured while it
        //is still empty and draws as a bare header until something else forces a fresh layout.
        var order = new List<string>();
        var rowsByRepository = new Dictionary<string, List<IssueRowViewModel>>(StringComparer.OrdinalIgnoreCase);
        var urlByRepository = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in page.Items)
        {
            var repository = string.IsNullOrWhiteSpace(item.RepositoryFullName)
                ? UnknownRepository
                : item.RepositoryFullName;

            if (!rowsByRepository.TryGetValue(repository, out var rows))
            {
                rows = new List<IssueRowViewModel>();
                rowsByRepository[repository] = rows;
                urlByRepository[repository] = item.RepositoryHtmlUrl;
                order.Add(repository);
            }

            rows.Add(new IssueRowViewModel(item, palette, _showAssignees, now, OpenUrlAsync));

            if (item.Kind == IssueKind.PullRequest) { _pullRequestCount++; } else { _issueCount++; }
            if (item.State == IssueState.Open || item.State == IssueState.Draft)
            {
                _openCount++;
            }
            else
            {
                _closedCount++;
            }
        }

        foreach (var repository in order)
        {
            var rows = rowsByRepository[repository];

            if (_groupsByRepository.TryGetValue(repository, out var existing))
            {
                foreach (var row in rows) { existing.Add(row); }
                continue;
            }

            var group = new RepositoryGroupViewModel(repository, urlByRepository[repository], OpenUrlAsync);
            foreach (var row in rows) { group.Add(row); }
            _groupsByRepository[repository] = group;
            InsertAlphabetically(group);
        }

        if (page.TotalCount.HasValue) { _total = page.TotalCount; }

        RefreshHeader();
        RefreshResultVisibility();
    }

    //Repositories read alphabetically however the pages arrive.
    private void InsertAlphabetically(RepositoryGroupViewModel group)
    {
        var index = 0;
        while (index < Groups.Count
            && string.Compare(Groups[index].FullName, group.FullName,
                StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }

        Groups.Insert(index, group);
    }

    private void CompleteSearch()
    {
        RefreshHeader();
        RefreshResultVisibility();

        var elapsed = DateTimeOffset.Now - _startedAt;
        var repositories = Groups.Count;
        SetStatus(
            $"Done: {Number(_fetched)} {(_fetched == 1 ? "item" : "items")} in {Number(repositories)} "
            + $"{(repositories == 1 ? "repository" : "repositories")} in {DescribeElapsed(elapsed)}.",
            SearchStatusKind.Done);
    }

    private void RefreshHeader()
    {
        if (Groups.Count == 0 && !IsSearching)
        {
            HeaderOpenText = "No results";
            HeaderSummary = string.Empty;
            IssuePrSplit = string.Empty;
            return;
        }

        HeaderOpenText = $"{Number(_openCount)} open";

        var summary = string.Empty;
        if (_searchedIncludeClosed) { summary += $" · {Number(_closedCount)} closed"; }
        summary += $" · {Number(Groups.Count)} {(Groups.Count == 1 ? "repository" : "repositories")}";
        if (IsSearching) { summary += StillLoading; }
        HeaderSummary = summary;

        IssuePrSplit = $"Issues {Number(_issueCount)} · Pull requests {Number(_pullRequestCount)}";
    }

    private void RefreshResultVisibility()
    {
        EmptyText = _searchedIncludeClosed
            ? $"No issues or pull requests {AssignedPhrase()} in {_searchedOwner}'s public repositories."
            : $"No open issues or pull requests {AssignedPhrase()} in {_searchedOwner}'s public repositories.";

        NotifyPropertyChanged(nameof(WelcomeVisibility));
        NotifyPropertyChanged(nameof(EmptyVisibility));
        NotifyPropertyChanged(nameof(ResultsVisibility));
        NotifyPropertyChanged(nameof(SearchingVisibility));
        NotifyPropertyChanged(nameof(HeaderVisibility));
        NotifyPropertyChanged(nameof(EmptyHintVisibility));
    }

    private string AssignedPhrase() =>
        _searchedAssignee.Length == 0 ? "without an assignee" : $"assigned to {_searchedAssignee}";

    #endregion

    #region | Progress, status and failures |

    private void OnProgress(SearchProgress report)
    {
        if (report == null) { return; }

        _fetched = report.Fetched;
        if (report.Total.HasValue) { _total = report.Total; }

        if (_total.HasValue && _total.Value > 0)
        {
            IsProgressIndeterminate = false;
            ProgressValue = Math.Min(100d, (report.Fetched * 100d) / _total.Value);
        }
        else
        {
            IsProgressIndeterminate = true;
        }

        UpdateQuotaText(report.Search, report.Core);

        switch (report.Phase)
        {
            case SearchPhase.WaitingForQuota:
                SetStatus(report.ToString(), SearchStatusKind.Waiting);
                break;

            case SearchPhase.Failed:
            case SearchPhase.Cancelled:
            case SearchPhase.Completed:
                //The view model writes its own sentence for these three, because it knows the
                //repository count and how long the run took.
                break;

            default:
                SetStatus(report.ToString(), SearchStatusKind.Working);
                break;
        }
    }

    private void UpdateQuotaText(RateLimitSnapshot search, RateLimitSnapshot core)
    {
        //What is left already counts down the ceiling this application holds itself to rather than
        //GitHub's larger allowance, so the pill reaches zero at the moment the search starts
        //waiting; the clamp is only a guard against a number outside that range. The core pill
        //keeps its full reading until a core request is actually made, because a whole-owner
        //search never touches that pool.
        if (search != null)
        {
            SearchQuotaText = $"Search {Number(Math.Clamp(search.Remaining, 0, search.Ceiling))} "
                + $"of {Number(search.Ceiling)}";
        }

        if (core != null)
        {
            CoreQuotaText = $"Core {Number(Math.Clamp(core.Remaining, 0, core.Ceiling))} "
                + $"of {Number(core.Ceiling)}";
        }
    }

    //While a search runs its own progress reports move the pills. When it stops they would
    //otherwise freeze on their last reading, so this refreshes them once a second until both
    //pools have climbed back to their ceilings: "Search 0 of 9" becomes "Search 9 of 9" again a
    //minute later without anyone having to start a search to find out.
    private void StartQuotaRecovery()
    {
        if (_isDisposed) { return; }

        _quotaRecoveryTimer ??= new Timer(_ => InvokeOnMainThread(RefreshQuotaFromService));
        _quotaRecoveryTimer.Change(QuotaRecoveryInterval, QuotaRecoveryInterval);
    }

    private void RefreshQuotaFromService()
    {
        if (_isDisposed) { return; }

        var search = _searchService?.LastSearchRateLimit;
        var core = _searchService?.LastCoreRateLimit;
        UpdateQuotaText(search, core);

        var searchIsFull = search == null || search.Remaining >= search.Ceiling;
        var coreIsFull = core == null || core.Remaining >= core.Ceiling;
        if (searchIsFull && coreIsFull && !IsSearching)
        {
            _quotaRecoveryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void SetStatus(string text, SearchStatusKind kind)
    {
        StatusText = text ?? string.Empty;
        StatusKind = kind;
        StatusGlyph = kind switch
        {
            SearchStatusKind.Waiting => Glyphs.Waiting,
            SearchStatusKind.Failed => Glyphs.Error,
            _ => string.Empty,
        };

        NotifyPropertyChanged(nameof(StatusGlyphVisibility));
        RepaintOwnBrushes();
    }

    //The library already phrases its failures for a person to read - an unknown owner arrives as
    //"GitHub has no user or organization named 'x'." - so the message is shown as it stands. The
    //one exception is a quota refusal, which the design words for itself and dates in local time.
    private static string DescribeFailure(GitHubApiException failure)
    {
        if (failure.RateLimitResetAt.HasValue)
        {
            var reset = failure.RateLimitResetAt.Value.ToLocalTime()
                .ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            return $"GitHub refused the request: search quota exhausted, resets at {reset}.";
        }

        return string.IsNullOrWhiteSpace(failure.Message)
            ? "GitHub refused the request."
            : failure.Message;
    }

    private string CountPhrase() =>
        _total.HasValue ? $"{Number(_fetched)} of {Number(_total.Value)}" : Number(_fetched);

    private static string DescribeElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) { elapsed = TimeSpan.Zero; }
        if (elapsed.TotalSeconds < 1d)
        {
            //Rounding this one to the nearest second would say a search took no time at all.
            return "under a second";
        }

        if (elapsed.TotalMinutes < 1d)
        {
            return $"{(int)Math.Round(elapsed.TotalSeconds)} s";
        }

        return $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:00} s";
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    #endregion

    #region | Opening pages in the browser |

    //One place in the whole application asks the host to open a URL. A refusal is a status line,
    //never an exception that reaches the user.
    private async Task OpenUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) { return; }

        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                SetStatus($"That link could not be read: {url}", SearchStatusKind.Failed);
                return;
            }

            var opened = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!opened)
            {
                SetStatus("No browser was available to open that page.", SearchStatusKind.Failed);
            }
        }
        catch (Exception failure)
        {
            SetStatus($"That page could not be opened: {failure.Message}", SearchStatusKind.Failed);
        }
    }

    #endregion

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isDisposed = true;
            _schemeApplier = null;

            var timer = _quotaRecoveryTimer;
            _quotaRecoveryTimer = null;
            timer?.Dispose();

            var cancellation = _searchCts;
            _searchCts = null;
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            var search = _searchCommand;
            _searchCommand = null;
            search?.Dispose();

            var cancel = _cancelCommand;
            _cancelCommand = null;
            cancel?.Dispose();

            if (Groups != null)
            {
                foreach (var group in Groups)
                {
                    group.Dispose();
                }

                Groups.Clear();
            }

            _groupsByRepository.Clear();

            //Only the instance this view model built itself; a registered one belongs to the
            //container that handed it over and is shared with whatever else resolved it.
            if (_ownsSearchService && _searchService is IDisposable ownService)
            {
                ownService.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
