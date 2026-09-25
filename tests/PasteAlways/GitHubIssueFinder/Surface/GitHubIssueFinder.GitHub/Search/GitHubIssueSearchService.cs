using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// The search service: one instance owns one connection pool and one pair of rate-limit
/// throttles, so it is registered as a singleton and shared by everything that searches.
/// </summary>
public sealed class GitHubIssueSearchService : IGitHubIssueSearchService, IDisposable
{
    //GitHub returns at most a thousand results for one search, however many matches there
    //are. Past that the search has to be split by repository.
    internal const int SearchResultCap = 1000;

    private const int PageSize = IssueSearchQueryBuilder.PageSize;
    private const string AcceptMediaType = "application/vnd.github+json";
    private const string ApiVersionHeaderName = "X-GitHub-Api-Version";
    private const string ApiVersion = "2022-11-28";
    private const string UnknownOwnerMarker = "cannot be searched";

    private HttpClient _httpClient;
    private RateLimitThrottle _searchThrottle;
    private RateLimitThrottle _coreThrottle;

    /// <summary>
    /// Initializes a new instance that talks to the live GitHub API.
    /// </summary>
    /// <param name="options">How the service identifies itself and how it paces its calls.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public GitHubIssueSearchService(GitHubSearchOptions options)
    {
        if (options == null) { throw new ArgumentNullException(nameof(options)); }

        Options = options;
        TimeProvider = TimeProvider.System;
        OwnsHandler = true;
        Handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
        };

        Prepare();
    }

    //The seam the tests use: a stub message handler in place of the network, and a fake
    //clock in place of the wall clock. The handler belongs to the caller and is never
    //disposed here.
    internal GitHubIssueSearchService(GitHubSearchOptions options, HttpMessageHandler handler,
        TimeProvider timeProvider)
    {
        if (options == null) { throw new ArgumentNullException(nameof(options)); }
        if (handler == null) { throw new ArgumentNullException(nameof(handler)); }

        Options = options;
        Handler = handler;
        TimeProvider = timeProvider ?? TimeProvider.System;
        OwnsHandler = false;

        Prepare();
    }

    //The settings this instance was built with.
    internal GitHubSearchOptions Options { get; }

    //The message handler the requests go through, or null until one is created.
    internal HttpMessageHandler Handler { get; set; }

    //The clock every wait and every rate-limit sum is measured against.
    internal TimeProvider TimeProvider { get; }

    //True when this instance created the handler and must therefore dispose it.
    internal bool OwnsHandler { get; }

    //True once Dispose has run.
    internal bool IsDisposed { get; private set; }

    //The two pools, so a test can watch the arithmetic without going through a search.
    internal RateLimitThrottle SearchThrottle => _searchThrottle;

    internal RateLimitThrottle CoreThrottle => _coreThrottle;

    /// <inheritdoc />
    public RateLimitSnapshot LastSearchRateLimit => _searchThrottle.Snapshot;

    /// <inheritdoc />
    public RateLimitSnapshot LastCoreRateLimit => _coreThrottle.Snapshot;

    /// <inheritdoc />
    //Deliberately not an iterator: an iterator would defer the argument checks until the
    //first MoveNextAsync, which hides a bad call from anything that only starts the search.
    public IAsyncEnumerable<IssueSearchPage> SearchAsync(IssueSearchRequest request,
        IProgress<SearchProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (request == null) { throw new ArgumentNullException(nameof(request)); }
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        //Copied so a caller that edits its request while the pages are still arriving
        //cannot change the query half way through.
        var snapshot = new IssueSearchRequest
        {
            Owner = request.Owner,
            Assignee = request.Assignee,
            IncludeClosed = request.IncludeClosed,
        };

        //Checks the owner and throws now rather than on the first page.
        IssueSearchQueryBuilder.BuildQuery(snapshot);

        return SearchInternalAsync(snapshot, progress, cancellationToken);
    }

    /// <summary>
    /// Releases the connection pool this instance created. A handler supplied through the
    /// internal constructor belongs to the caller and is left alone.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        if (_httpClient != null)
        {
            //Built with disposeHandler false, so this releases the client and nothing else.
            _httpClient.Dispose();
            _httpClient = null;
        }

        if (OwnsHandler && Handler != null)
        {
            Handler.Dispose();
            Handler = null;
        }
    }

    private void Prepare()
    {
        var baseAddress = Options.BaseAddress ?? new Uri("https://api.github.com/");
        if (!baseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            //A base address without a trailing slash loses its last segment when a relative
            //address is resolved against it.
            baseAddress = new Uri(baseAddress.AbsoluteUri + "/", UriKind.Absolute);
        }

        _httpClient = new HttpClient(Handler, disposeHandler: false)
        {
            BaseAddress = baseAddress,
            //Each request sets its own deadline from a linked token, so the client-wide
            //timeout must not cut anything short.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        var userAgent = string.IsNullOrWhiteSpace(Options.UserAgent)
            ? GitHubSearchOptions.FallbackUserAgent
            : Options.UserAgent;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent))
        {
            //GitHub refuses a request with no User-Agent, so an unparseable one still goes.
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        }

        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(AcceptMediaType);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(ApiVersionHeaderName, ApiVersion);

        _searchThrottle = new RateLimitThrottle("search", Options.SearchCeilingPerMinute,
            Options.SearchWindow, TimeProvider);
        _coreThrottle = new RateLimitThrottle("core", Options.CoreCeilingPerHour,
            Options.CoreWindow, TimeProvider);
    }

    private async IAsyncEnumerable<IssueSearchPage> SearchInternalAsync(IssueSearchRequest request,
        IProgress<SearchProgress> progress, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var state = new SearchState();
        Report(progress, SearchPhase.Starting, state);

        var firstUrl = IssueSearchQueryBuilder.BuildSearchUrl(request, 1);
        var first = await GetSearchAsync(firstUrl, request.Owner, state, progress, cancellationToken)
            .ConfigureAwait(false);
        state.Total = first.TotalCount;

        if (first.TotalCount > SearchResultCap)
        {
            //Past the cap the whole-owner search can never reach the end, so this first page
            //is thrown away and the work starts again one repository at a time. The total
            //stays as it was: it is still the number of matches the user is waiting for.
            await foreach (var page in SearchByRepositoryAsync(request, state, progress, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return page;
            }
        }
        else
        {
            await foreach (var page in WalkAsync(request, null, first, state, progress, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return page;
            }
        }

        Report(progress, SearchPhase.Completed, state);
    }

    //Walks one query to its end: the whole owner, or a single repository. The first page may
    //already have been read, in which case it is handed in rather than fetched again.
    private async IAsyncEnumerable<IssueSearchPage> WalkAsync(IssueSearchRequest request,
        string repositoryFullName, SearchResponseDto firstPage, SearchState state,
        IProgress<SearchProgress> progress, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = firstPage;
        var pageNumber = 0;
        var total = 0;

        while (true)
        {
            pageNumber++;

            if (response == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = IssueSearchQueryBuilder.BuildSearchUrl(request, pageNumber, repositoryFullName);
                response = await GetSearchAsync(url, request.Owner, state, progress, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (pageNumber == 1)
            {
                //However many matches there are, GitHub serves only the first thousand of
                //them, so the walk stops at the tenth page even when the total is larger.
                //A whole-owner search never gets here with a larger total, but one
                //repository inside the per-repository plan can.
                total = response.TotalCount > SearchResultCap ? SearchResultCap : response.TotalCount;
            }

            var items = IssueItemMapper.MapItems(response.Items);
            var page = new IssueSearchPage
            {
                Items = items,
                PageNumber = pageNumber,
                TotalCount = response.TotalCount,
                IncompleteResults = response.IncompleteResults,
                RepositoryFullName = repositoryFullName,
            };

            state.PagesFetched++;
            state.Fetched += items.Count;
            Report(progress, SearchPhase.Fetching, state);
            yield return page;

            if (items.Count == 0) { yield break; }
            if (pageNumber * PageSize >= total) { yield break; }

            response = null;
        }
    }

    //The plan for an owner with more matches than one search can return: list the owner's
    //repositories, drop the ones that cannot hold a match, and search what is left one at a
    //time. The core pool pays for the listing, the search pool for the searches.
    private async IAsyncEnumerable<IssueSearchPage> SearchByRepositoryAsync(IssueSearchRequest request,
        SearchState state, IProgress<SearchProgress> progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var repositories = await ListRepositoriesAsync(request, state, progress, cancellationToken)
            .ConfigureAwait(false);

        foreach (var repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await foreach (var page in WalkAsync(request, repository, null, state, progress, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return page;
            }
        }
    }

    private async Task<List<string>> ListRepositoriesAsync(IssueSearchRequest request, SearchState state,
        IProgress<SearchProgress> progress, CancellationToken cancellationToken)
    {
        var kept = new List<RepositoryDto>();
        var listPage = 0;

        while (true)
        {
            listPage++;
            cancellationToken.ThrowIfCancellationRequested();

            var url = IssueSearchQueryBuilder.BuildRepositoryListUrl(request.Owner, listPage);
            var body = await GetAsync(url, _coreThrottle, request.Owner, state, progress, cancellationToken)
                .ConfigureAwait(false);
            var repositories = ParseRepositories(body, url);

            foreach (var repository in repositories)
            {
                if (repository == null || string.IsNullOrWhiteSpace(repository.FullName)) { continue; }
                if (repository.Archived || !repository.HasIssues) { continue; }

                //A repository with nothing open cannot answer an open-items search; it can
                //still answer one that includes closed items.
                if (repository.OpenIssuesCount <= 0 && !request.IncludeClosed) { continue; }

                kept.Add(repository);
            }

            ReportListing(progress, state, listPage);

            if (repositories.Length < PageSize) { break; }
        }

        kept.Sort((left, right) =>
            string.Compare(left.FullName, right.FullName, StringComparison.OrdinalIgnoreCase));

        var names = new List<string>(kept.Count);
        foreach (var repository in kept) { names.Add(repository.FullName); }
        return names;
    }

    private async Task<SearchResponseDto> GetSearchAsync(string relativeUrl, string owner, SearchState state,
        IProgress<SearchProgress> progress, CancellationToken cancellationToken)
    {
        var body = await GetAsync(relativeUrl, _searchThrottle, owner, state, progress, cancellationToken)
            .ConfigureAwait(false);
        return ParseSearch(body, relativeUrl);
    }

    //One call, with everything the rate limits ask of it: wait for a slot, send, take the
    //new pool figures from the headers, and give a refusal exactly one second chance.
    private async Task<string> GetAsync(string relativeUrl, RateLimitThrottle throttle, string owner,
        SearchState state, IProgress<SearchProgress> progress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var reportWait = BuildWaitReporter(progress, state);
        var absoluteUrl = new Uri(_httpClient.BaseAddress, relativeUrl).AbsoluteUri;

        await throttle.AcquireAsync(reportWait, cancellationToken).ConfigureAwait(false);
        var attempt = await SendOnceAsync(relativeUrl, absoluteUrl, throttle, cancellationToken)
            .ConfigureAwait(false);
        if (attempt.IsSuccess) { return attempt.Body; }

        if (attempt.IsRateLimitRefusal)
        {
            var until = attempt.RetryAfter.HasValue
                ? TimeProvider.GetUtcNow() + attempt.RetryAfter.Value
                : ResetMoment(throttle);

            await throttle.DelayUntilAsync(until, reportWait, cancellationToken).ConfigureAwait(false);

            //The wait replaced the gate, so the retry only has to be counted.
            throttle.RecordIssued();
            attempt = await SendOnceAsync(relativeUrl, absoluteUrl, throttle, cancellationToken)
                .ConfigureAwait(false);
            if (attempt.IsSuccess) { return attempt.Body; }
        }

        throw BuildException(attempt, absoluteUrl, throttle, owner);
    }

    private async Task<HttpAttempt> SendOnceAsync(string relativeUrl, string absoluteUrl,
        RateLimitThrottle throttle, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (Options.RequestTimeout > TimeSpan.Zero)
        {
            timeoutSource.CancelAfter(Options.RequestTimeout);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(_httpClient.BaseAddress, relativeUrl));
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token)
                .ConfigureAwait(false);

            throttle.UpdateFrom(response.Headers);

            var body = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
            return BuildAttempt(response, body, throttle);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            //The caller did not cancel, so this was the per-request deadline.
            var seconds = Options.RequestTimeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);
            throw new GitHubApiException(
                $"GitHub did not answer {absoluteUrl} within {seconds} seconds.",
                default(HttpStatusCode), absoluteUrl, null, null, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubApiException(
                $"The call to {absoluteUrl} could not be made: {ex.Message}",
                default(HttpStatusCode), absoluteUrl, null, null, ex);
        }
    }

    private static HttpAttempt BuildAttempt(HttpResponseMessage response, string body,
        RateLimitThrottle throttle)
    {
        TimeSpan? retryAfter = null;
        if (throttle.TryReadRetryAfter(response.Headers, out var delay)) { retryAfter = delay; }

        var exhausted = RateLimitThrottle.TryReadRemaining(response.Headers, out var remaining)
            && remaining <= 0;

        var refusal = !response.IsSuccessStatusCode
            && (response.StatusCode == HttpStatusCode.Forbidden
                || response.StatusCode == HttpStatusCode.TooManyRequests)
            && (exhausted || retryAfter.HasValue);

        return new HttpAttempt
        {
            IsSuccess = response.IsSuccessStatusCode,
            StatusCode = response.StatusCode,
            Body = body,
            RetryAfter = retryAfter,
            IsRateLimitRefusal = refusal,
        };
    }

    private DateTimeOffset ResetMoment(RateLimitThrottle throttle)
    {
        var snapshot = throttle.Reported;
        if (snapshot != null) { return snapshot.ResetAt + RateLimitThrottle.ResetGrace; }

        //No headers to go on, so the whole window is waited out.
        return TimeProvider.GetUtcNow() + throttle.Window;
    }

    private GitHubApiException BuildException(HttpAttempt attempt, string absoluteUrl,
        RateLimitThrottle throttle, string owner)
    {
        var gitHubMessage = ReadErrorMessage(attempt.Body);

        if (attempt.IsRateLimitRefusal)
        {
            var snapshot = throttle.Reported;
            DateTimeOffset? resetAt = snapshot == null ? (DateTimeOffset?)null : snapshot.ResetAt;
            var whenText = resetAt.HasValue
                ? " It resets at " + resetAt.Value.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC."
                : string.Empty;

            return new GitHubApiException(
                $"GitHub's {throttle.Resource} rate limit is still exhausted after waiting for it to reset.{whenText}",
                attempt.StatusCode, absoluteUrl, gitHubMessage, resetAt);
        }

        //An owner GitHub has never heard of comes back as a rejected query, not a 404.
        if (attempt.StatusCode == HttpStatusCode.UnprocessableEntity
            && gitHubMessage != null
            && gitHubMessage.IndexOf(UnknownOwnerMarker, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var login = string.IsNullOrWhiteSpace(owner) ? "that owner" : "'" + owner.Trim() + "'";
            return new GitHubApiException(
                $"GitHub has no user or organization named {login}.",
                attempt.StatusCode, absoluteUrl, gitHubMessage);
        }

        var detail = string.IsNullOrWhiteSpace(gitHubMessage) ? string.Empty : " " + gitHubMessage;
        return new GitHubApiException(
            $"GitHub answered {(int)attempt.StatusCode} ({attempt.StatusCode}) for {absoluteUrl}.{detail}",
            attempt.StatusCode, absoluteUrl, gitHubMessage);
    }

    //The message member of the error body, with every sentence from its errors array added
    //after it, because GitHub puts the useful half of a rejected query in the array.
    internal static string ReadErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) { return null; }

        ErrorResponseDto error;
        try
        {
            error = JsonSerializer.Deserialize(body, GitHubJsonContext.Default.ErrorResponseDto);
        }
        catch (JsonException)
        {
            //A body that is not JSON at all tells the caller nothing it can use.
            return null;
        }

        if (error == null) { return null; }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(error.Message)) { parts.Add(error.Message.Trim()); }

        if (error.Errors != null)
        {
            foreach (var detail in error.Errors)
            {
                if (detail == null || string.IsNullOrWhiteSpace(detail.Message)) { continue; }
                parts.Add(detail.Message.Trim());
            }
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static SearchResponseDto ParseSearch(string body, string relativeUrl)
    {
        try
        {
            var response = JsonSerializer.Deserialize(body, GitHubJsonContext.Default.SearchResponseDto);
            if (response == null)
            {
                throw new GitHubApiException($"GitHub returned an empty search result for {relativeUrl}.");
            }

            return response;
        }
        catch (JsonException ex)
        {
            throw new GitHubApiException(
                $"GitHub returned a search result for {relativeUrl} that could not be read.", ex);
        }
    }

    private static RepositoryDto[] ParseRepositories(string body, string relativeUrl)
    {
        try
        {
            return JsonSerializer.Deserialize(body, GitHubJsonContext.Default.RepositoryDtoArray)
                ?? Array.Empty<RepositoryDto>();
        }
        catch (JsonException ex)
        {
            throw new GitHubApiException(
                $"GitHub returned a repository list for {relativeUrl} that could not be read.", ex);
        }
    }

    private Action<TimeSpan, DateTimeOffset> BuildWaitReporter(IProgress<SearchProgress> progress,
        SearchState state)
    {
        if (progress == null) { return null; }

        return (remaining, until) => progress.Report(new SearchProgress(SearchPhase.WaitingForQuota,
            state.Fetched, state.Total, state.PagesFetched, remaining, until,
            _searchThrottle.Snapshot, _coreThrottle.Snapshot));
    }

    private void Report(IProgress<SearchProgress> progress, SearchPhase phase, SearchState state)
    {
        if (progress == null) { return; }

        progress.Report(new SearchProgress(phase, state.Fetched, state.Total, state.PagesFetched,
            null, null, _searchThrottle.Snapshot, _coreThrottle.Snapshot));
    }

    //The listing counts its own pages, which are repository pages rather than result pages.
    private void ReportListing(IProgress<SearchProgress> progress, SearchState state, int listPages)
    {
        if (progress == null) { return; }

        progress.Report(new SearchProgress(SearchPhase.ListingRepositories, state.Fetched, state.Total,
            listPages, null, null, _searchThrottle.Snapshot, _coreThrottle.Snapshot));
    }

    //What one running search has counted so far, shared by every report it makes.
    private sealed class SearchState
    {
        internal int Fetched { get; set; }

        internal int? Total { get; set; }

        internal int PagesFetched { get; set; }
    }

    //One send and what came back of it, so the retry decision reads in one place.
    private sealed class HttpAttempt
    {
        internal bool IsSuccess { get; set; }

        internal HttpStatusCode StatusCode { get; set; }

        internal string Body { get; set; }

        internal TimeSpan? RetryAfter { get; set; }

        internal bool IsRateLimitRefusal { get; set; }
    }
}
