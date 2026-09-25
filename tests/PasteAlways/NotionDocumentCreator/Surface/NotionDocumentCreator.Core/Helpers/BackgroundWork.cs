using System;
using System.Threading.Tasks;

namespace NotionDocumentCreator.Helpers;

/// <summary>
/// Starts asynchronous work from a place that cannot await it - a bound property's setter, a
/// selection change - and observes the result, so the gesture that started the work never sees
/// an exception and nothing is left as an unobserved task. Prefer awaiting the work from a
/// command; reach for this only where the caller really has no way to await.
/// </summary>
public static class BackgroundWork
{
    /// <summary>
    /// Starts <paramref name="work"/> and observes how it ends. A failure is handed to
    /// <paramref name="onError"/> when one is supplied, and swallowed otherwise; either way it
    /// never escapes to the caller. A null <paramref name="work"/> does nothing.
    /// </summary>
    /// <param name="work">The asynchronous work to start.</param>
    /// <param name="onError">Called with the failure when the work throws. Optional.</param>
    public static void StartAndObserve(Func<Task> work, Action<Exception> onError = null)
    {
        if (work is null) { return; }
        _ = ObserveAsync(work, onError);
    }

    private static async Task ObserveAsync(Func<Task> work, Action<Exception> onError)
    {
        try
        {
            await work();
        }
        catch (Exception e)
        {
            //Reporting the failure is the caller's business; observing it is this method's.
            try { onError?.Invoke(e); }
            catch (Exception) { } //A failing error handler must not replace one unobserved fault with another
        }
    }
}
