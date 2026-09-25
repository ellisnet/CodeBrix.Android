using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace HelloPaste.SelfCheck;

/// <summary>
/// The on-device self-check of HelloPaste (the device tests of AP1): every CodeBrix.Android
/// platform contract through public APIs (<see cref="ContractChecks"/>) and the pasted page
/// itself - App.xaml resources, Frame navigation, bindings, commands, the startup dialog and
/// the element handlers' native views (<see cref="PageChecks"/>). Each check writes one
/// logcat line "SELFCHECK &lt;name&gt; PASS|FAIL &lt;detail&gt;"; the last line is
/// "SELFCHECK SUMMARY pass=N fail=M", which build/test-scripts/device-smoke.sh waits for.
/// </summary>
internal static class Runner
{
    private static ILogger _log;
    private static int _pass;
    private static int _fail;
    private static int _unhandled;
    private static string _firstUnhandled;

    /// <summary>The activity under test.</summary>
    internal static CodeBrixActivity Activity { get; private set; }

    /// <summary>The UI thread's dispatcher queue.</summary>
    internal static DispatcherQueue Queue { get; private set; }

    /// <summary>Schedules the self-check (once) 1.5 s after the first resume.</summary>
    public static void Schedule(CodeBrixActivity activity)
    {
        Activity = activity;
        _log = LogExtensionPoint.AmbientLoggerFactory.CreateLogger("HelloPaste.SelfCheck");
        Queue = DispatcherQueue.GetForCurrentThread();

        AppDomain.CurrentDomain.UnhandledException += (_, e) => RecordUnhandled(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => RecordUnhandled(e.Exception);
        if (Application.Current != null)
        {
            Application.Current.UnhandledException += (_, e) => RecordUnhandled(e.Exception);
        }

        var timer = Queue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(1500);
        timer.IsRepeating = false;
        timer.Tick += async (_, _) => await RunAsync();
        timer.Start();
        _log.LogInformation("SELFCHECK scheduled (start page '{Page}', DispatcherQueueTimer 1500 ms)", StartPages.Requested);
    }

    private static async Task RunAsync()
    {
        try
        {
            await ContractChecks.RunAsync();
            await PageChecks.RunAsync();
        }
        catch (Exception e)
        {
            Report("exception", false, e.ToString());
        }

        Report("exceptions.unhandled", _unhandled == 0, _unhandled == 0 ? "no unhandled exception" : $"{_unhandled}: {_firstUnhandled}");
        _log.LogInformation("SELFCHECK SUMMARY pass={Pass} fail={Fail}", _pass, _fail);
    }

    private static void RecordUnhandled(Exception e)
    {
        _unhandled++;
        _firstUnhandled ??= e?.ToString() ?? "(null)";
    }

    /// <summary>Logs one check result.</summary>
    internal static void Report(string name, bool pass, string detail)
    {
        if (pass)
        {
            _pass++;
        }
        else
        {
            _fail++;
        }

        _log.LogInformation("SELFCHECK {Name} {Result} {Detail}", name, pass ? "PASS" : "FAIL", detail);
    }

    /// <summary>Logs an informational line (not a check).</summary>
    internal static void Info(string message) => _log.LogInformation("SELFCHECK-INFO {Message}", message);

    /// <summary>Completes after everything queued on the UI thread so far (and one layout tick) has run.</summary>
    internal static async Task SettleAsync(int extraMilliseconds = 250)
    {
        var done = new TaskCompletionSource<bool>();
        Queue.TryEnqueue(DispatcherQueuePriority.Low, () => done.TrySetResult(true));
        await done.Task;
        await Task.Delay(extraMilliseconds);
        var again = new TaskCompletionSource<bool>();
        Queue.TryEnqueue(DispatcherQueuePriority.Low, () => again.TrySetResult(true));
        await again.Task;
    }

    /// <summary>Waits (polling on the UI thread) until <paramref name="condition"/> holds or the timeout expires.</summary>
    internal static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMilliseconds)
    {
        var waited = 0;
        while (!condition())
        {
            if (waited >= timeoutMilliseconds)
            {
                return false;
            }

            await Task.Delay(100);
            waited += 100;
        }

        return true;
    }

    /// <summary>Depth-first descendants of an element (the element first).</summary>
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        if (root == null)
        {
            yield break;
        }

        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = count - 1; i >= 0; i--)
            {
                stack.Push(VisualTreeHelper.GetChild(current, i));
            }
        }
    }
}
