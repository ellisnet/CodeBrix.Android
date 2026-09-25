using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubIssueFinder.GitHub;

//One GitHub rate-limit pool, and the promise that the application will not empty it.
//
//Two things hold a caller back. The response headers are the authority: when GitHub says
//one call is left, the throttle waits for the pool to refill rather than spend it. Before
//the first response there are no headers, so a sliding count of the calls this instance has
//made in the last window stands in for them, capped at a ceiling one below what GitHub
//allows. Waits are measured on the supplied clock, so a test can run a full hour in a
//millisecond, and are announced through a callback once a second so the application can say
//what it is waiting for.
internal sealed class RateLimitThrottle
{
    internal const string LimitHeaderName = "x-ratelimit-limit";
    internal const string RemainingHeaderName = "x-ratelimit-remaining";
    internal const string ResetHeaderName = "x-ratelimit-reset";
    internal const string RetryAfterHeaderName = "retry-after";

    //The pool is left alone for one extra second after its reset moment, because the clock
    //here and the clock at GitHub are never exactly the same.
    internal static readonly TimeSpan ResetGrace = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(1);

    private readonly Queue<DateTimeOffset> _issued = new Queue<DateTimeOffset>();
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
    private readonly TimeProvider _timeProvider;

    internal RateLimitThrottle(string resource, int ceiling, TimeSpan window, TimeProvider timeProvider)
    {
        Resource = string.IsNullOrWhiteSpace(resource) ? "api" : resource;
        Ceiling = ceiling < 1 ? 1 : ceiling;
        Window = window <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : window;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    //The name GitHub gives this pool: "search" or "core".
    internal string Resource { get; }

    //The most calls this instance will make in one window.
    internal int Ceiling { get; }

    //How long the window is: a minute for search, an hour for core.
    internal TimeSpan Window { get; }

    //What the last response said about the pool, exactly as GitHub worded it, or null before
    //the first response. The waits are decided from this, because it is GitHub's own number.
    internal RateLimitSnapshot Reported { get; private set; }

    //What the pool looks like to the application, or null before the first response. It is the
    //smaller of what GitHub says is left and what this instance's own ceiling still allows, so a
    //display built from it counts down the budget the application actually keeps to and reaches
    //zero at the moment the throttle starts waiting. It is worked out on every read rather than
    //stored, so it climbs back by itself as the local window slides and as GitHub's own pool
    //passes its reset moment.
    internal RateLimitSnapshot Snapshot
    {
        get
        {
            var reported = Reported;
            if (reported == null) { return null; }

            //Past the reset moment GitHub's pool has refilled, so the number the last response
            //carried is stale and the pool is full again.
            var reportedLeft = _timeProvider.GetUtcNow() >= reported.ResetAt
                ? reported.Limit
                : reported.Remaining;

            var left = Ceiling - IssuedInWindow;
            if (left < 0) { left = 0; }
            if (reportedLeft < left) { left = reportedLeft; }
            if (left < 0) { left = 0; }

            return new RateLimitSnapshot(reported.Limit, left, Ceiling, reported.ResetAt);
        }
    }

    //How many calls are counted inside the current window; the tests read it, and nothing
    //else needs it.
    internal int IssuedInWindow
    {
        get
        {
            lock (_issued)
            {
                Trim(_timeProvider.GetUtcNow());
                return _issued.Count;
            }
        }
    }

    //Waits until a call may be made, then counts it. Both waits report themselves once a
    //second through reportWait, which may be null when nobody is listening.
    internal async Task AcquireAsync(Action<TimeSpan, DateTimeOffset> reportWait,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            //The headers win: GitHub knows about every other caller on this address.
            var snapshot = Reported;
            if (snapshot != null && snapshot.Remaining <= 1
                && _timeProvider.GetUtcNow() < snapshot.ResetAt)
            {
                await DelayUntilAsync(snapshot.ResetAt + ResetGrace, reportWait, cancellationToken)
                    .ConfigureAwait(false);
            }

            //Then the local count, which is all there is to go on before the first response.
            while (true)
            {
                DateTimeOffset oldest;
                lock (_issued)
                {
                    Trim(_timeProvider.GetUtcNow());
                    if (_issued.Count < Ceiling) { break; }
                    oldest = _issued.Peek();
                }

                await DelayUntilAsync(oldest + Window, reportWait, cancellationToken).ConfigureAwait(false);
            }

            RecordIssued();
        }
        finally
        {
            _gate.Release();
        }
    }

    //Counts a call that was made outside AcquireAsync, which is what the one retry after a
    //refusal does: it has already waited for the pool by the time it is sent.
    internal void RecordIssued()
    {
        lock (_issued)
        {
            _issued.Enqueue(_timeProvider.GetUtcNow());
        }
    }

    //Waits until the given moment, announcing what is left once a second. Returns at once
    //when the moment has already passed.
    internal async Task DelayUntilAsync(DateTimeOffset until, Action<TimeSpan, DateTimeOffset> reportWait,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        while (now < until)
        {
            var remaining = until - now;
            if (reportWait != null) { reportWait(remaining, until); }

            var slice = remaining > ReportInterval ? ReportInterval : remaining;
            await Task.Delay(slice, _timeProvider, cancellationToken).ConfigureAwait(false);
            now = _timeProvider.GetUtcNow();
        }
    }

    //Replaces the reported snapshot from a response. A response that carries no rate-limit
    //headers leaves the old one in place rather than pretending the pool is unknown.
    internal void UpdateFrom(HttpResponseHeaders headers)
    {
        if (headers == null) { return; }
        if (!TryReadLong(headers, LimitHeaderName, out var limit)) { return; }
        if (!TryReadLong(headers, RemainingHeaderName, out var remaining)) { return; }
        if (!TryReadLong(headers, ResetHeaderName, out var reset)) { return; }

        Reported = new RateLimitSnapshot((int)limit, (int)remaining, Ceiling,
            DateTimeOffset.FromUnixTimeSeconds(reset));
    }

    //Reads the retry-after header, which GitHub sends with a secondary-limit refusal. Both
    //forms are accepted: a number of seconds, and an HTTP date.
    internal bool TryReadRetryAfter(HttpResponseHeaders headers, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;
        if (headers == null) { return false; }

        if (!TryGetHeader(headers, RetryAfterHeaderName, out var value)) { return false; }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            delay = TimeSpan.FromSeconds(seconds < 0 ? 0 : seconds);
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal, out var when))
        {
            var span = when - _timeProvider.GetUtcNow();
            delay = span < TimeSpan.Zero ? TimeSpan.Zero : span;
            return true;
        }

        return false;
    }

    //Reads the remaining count on its own, which is how a refusal is told apart from any
    //other failure.
    internal static bool TryReadRemaining(HttpResponseHeaders headers, out int remaining)
    {
        remaining = 0;
        if (!TryReadLong(headers, RemainingHeaderName, out var value)) { return false; }
        remaining = (int)value;
        return true;
    }

    private void Trim(DateTimeOffset now)
    {
        while (_issued.Count > 0 && _issued.Peek() + Window <= now)
        {
            _issued.Dequeue();
        }
    }

    private static bool TryReadLong(HttpResponseHeaders headers, string name, out long value)
    {
        value = 0L;
        return TryGetHeader(headers, name, out var text)
            && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetHeader(HttpResponseHeaders headers, string name, out string value)
    {
        value = null;
        if (headers == null) { return false; }
        if (!headers.TryGetValues(name, out var values)) { return false; }

        foreach (var candidate in values)
        {
            if (string.IsNullOrWhiteSpace(candidate)) { continue; }
            value = candidate.Trim();
            return true;
        }

        return false;
    }
}
