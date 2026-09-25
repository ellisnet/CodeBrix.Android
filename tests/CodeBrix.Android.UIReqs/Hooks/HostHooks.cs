using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Hosting;
using CodeBrix.Android.UIReqs.Protocol;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using Reqnroll;
using Reqnroll.UnitTestProvider;
using Xunit;

namespace CodeBrix.Android.UIReqs.Hooks;

/// <summary>
/// The host's hooks: connect to the device app and start the run there; per scenario, the
/// frame archives (host side), the pending list, and the device's own scenario hooks (which
/// skip scenarios of the other orientation and reset the panel afterwards); on failure, the
/// failure frame's path and the canvas report go into the test output, as on Platform.
/// </summary>
[Binding]
public sealed class HostHooks
{
    private static readonly List<PendingEntry> _pending = new();
    private static string? _unreachable;
    private readonly ScenarioContext _scenarioContext;
    private readonly FeatureContext _featureContext;
    private readonly IReqnrollOutputHelper _output;
    private readonly IUnitTestRuntimeProvider _runtime;

    /// <summary>Creates the hooks.</summary>
    public HostHooks(ScenarioContext scenarioContext, FeatureContext featureContext, IReqnrollOutputHelper output, IUnitTestRuntimeProvider runtime)
    {
        _scenarioContext = scenarioContext;
        _featureContext = featureContext;
        _output = output;
        _runtime = runtime;
    }

    /// <summary>Connects to the device app, reads the pending list and starts the frame archive.</summary>
    [BeforeTestRun(Order = 0)]
    public static async Task Connect_to_the_scenario_app()
    {
        LoadPending();
        _unreachable = null;

        // A plain `dotnet test` of the solution (the runner script is not driving this run): run only
        // when a scenario app answers; otherwise skip every scenario with the reason.
        var driven = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UIREQS_SERIAL"));
        if (!driven && !await DeviceSession.IsReachableAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false))
        {
            _unreachable = "no Android device or emulator reachable (no UIReqs scenario app answers on 127.0.0.1:"
                + DeviceSession.HostPort + "; run build/test-scripts/android-uireqs-run.sh)";
            Console.Out.WriteLine("UIReqs (Android): " + _unreachable + " - every scenario is skipped.");
            return;
        }

        var hello = await DeviceSession.ConnectAsync().ConfigureAwait(false);
        FrameReview.Initialize();
        Console.Out.WriteLine(
            $"UIReqs (Android): {DeviceSession.Serial}, {DeviceSession.Orientation} panel {DeviceSession.Panel.Width} x {DeviceSession.Panel.Height} "
            + $"(density {DeviceSession.Panel.Density}); {hello.Int("steps")} step definitions on the device; {_pending.Count} pending entries; {FrameReview.Describe()}.");
        foreach (var line in hello.Strings("output"))
        {
            Console.Out.WriteLine("device: " + line);
        }
    }

    /// <summary>Ends the run on the device.</summary>
    [AfterTestRun(Order = 0)]
    public static async Task Disconnect_from_the_scenario_app()
    {
        if (_unreachable == null)
        {
            await DeviceSession.DisconnectAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Starts the scenario on both halves (or skips it).</summary>
    [BeforeScenario(Order = 0)]
    public async Task Begin_scenario()
    {
        var feature = _featureContext.FeatureInfo;
        var scenario = _scenarioContext.ScenarioInfo;
        if (_unreachable != null)
        {
            _scenarioContext["uireqs.skipped"] = true;
            _runtime.TestIgnore(_unreachable);
            return;
        }

        DeviceSession.BeginScenario();
        FrameArchive.BeginScenario(feature.Title, scenario.Title);
        FrameReview.BeginScenario(_featureContext, _scenarioContext);

        var pending = _pending.FirstOrDefault(p => p.Matches(feature.Title, scenario.Title));
        if (pending != null)
        {
            _runtime.TestIgnore("PENDING (" + pending.Owner + "): " + pending.Reason);
            return;
        }

        var reply = await DeviceSession.RequestAsync(new JsonObject
        {
            ["op"] = "beginScenario",
            ["feature"] = feature.Title,
            ["folder"] = feature.FolderPath,
            ["featureTags"] = feature.Tags.ToJsonArray(),
            ["scenario"] = scenario.Title,
            ["tags"] = scenario.Tags.ToJsonArray(),
            ["combinedTags"] = scenario.CombinedTags.ToJsonArray(),
        }).ConfigureAwait(false);
        Write(reply);
        if (reply.Str("error") is { } error)
        {
            throw new InvalidOperationException("The device could not start the scenario: " + error);
        }

        if (reply.Str("skip") is { } skip)
        {
            _scenarioContext["uireqs.skipped"] = true;
            _runtime.TestIgnore(skip);
        }
    }

    /// <summary>Ends the scenario on the device (its reset) and reports a failure's frame and canvas report.</summary>
    [AfterScenario(Order = 0)]
    public async Task End_scenario()
    {
        if (_scenarioContext.ContainsKey("uireqs.skipped") || _pending.Any(p => p.Matches(_featureContext.FeatureInfo.Title, _scenarioContext.ScenarioInfo.Title)))
        {
            return;
        }

        var failed = _scenarioContext.ScenarioExecutionStatus != ScenarioExecutionStatus.OK;
        var reply = await DeviceSession.RequestAsync(new JsonObject { ["op"] = "endScenario", ["failed"] = failed }).ConfigureAwait(false);
        Write(reply);
        if (!failed)
        {
            return;
        }

        var path = FrameArchive.LastSavedPath ?? FrameArchive.TrySave(DeviceSession.LatestFrame);
        if (path != null && File.Exists(path))
        {
            _output.WriteLine($"screencap for \"{_scenarioContext.ScenarioInfo.Title}\" -> {path}");
            TestContext.Current.AddAttachment("frame.png", File.ReadAllBytes(path), "image/png");
            TestContext.Current.AddAttachment("frame.path", path);
        }

        if (reply.Str("canvasReport") is { Length: > 0 } report)
        {
            _output.WriteLine("canvas report:" + Environment.NewLine + report);
        }
    }

    private static void LoadPending()
    {
        _pending.Clear();
        var file = Path.Combine(AppContext.BaseDirectory, "uireqs-pending.txt");
        if (!File.Exists(file))
        {
            return;
        }

        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length == 4)
            {
                _pending.Add(new PendingEntry(parts[0], parts[1], parts[2], parts[3]));
            }
        }
    }

    private void Write(JsonObject reply)
    {
        foreach (var line in reply.Strings("output"))
        {
            _output.WriteLine(line);
        }
    }

    private sealed record PendingEntry(string Feature, string Scenario, string Owner, string Reason)
    {
        internal bool Matches(string feature, string scenario) =>
            string.Equals(Feature, feature, StringComparison.Ordinal) && (Scenario == "*" || string.Equals(Scenario, scenario, StringComparison.Ordinal));
    }
}
