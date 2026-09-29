using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using CodeBrix.Android.UIReqs.Protocol;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using Reqnroll;
using AndroidLog = Android.Util.Log;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>
/// The device half of the UIReqs port: listens on localhost:<see cref="UIReqsChannel.DevicePort"/>
/// (the runner script forwards it with <c>adb forward</c>) and executes what the host-side
/// Reqnroll runner sends - test-run start, scenario begin/end (with the copied hooks) and each
/// step, matched against the copied step definitions and run IN the app, where the Core tree is.
/// </summary>
internal static class StepServer
{
    private const string Tag = "UIReqs";
    private static readonly Dictionary<string, FeatureContext> _features = new(StringComparer.Ordinal);
    private static CancellationTokenSource _session = new();
    private static StepCatalog? _catalog;
    private static bool _testRunStarted;
    private static BindingScope? _scope;
    private static int _started;

    /// <summary>Cancelled when the host session ends.</summary>
    internal static CancellationToken SessionToken => _session.Token;

    /// <summary>Starts listening (once per process).</summary>
    internal static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _catalog = StepCatalog.Build(typeof(StepServer).Assembly);
        AndroidLog.Info(Tag, $"step server: {_catalog.StepCount} step definitions in {_catalog.BindingTypes.Count} binding classes; listening on {UIReqsChannel.DevicePort}");
        _ = Task.Run(AcceptLoopAsync);
    }

    private static async Task AcceptLoopAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, UIReqsChannel.DevicePort);
        listener.Start();
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            client.NoDelay = true;
            _session = new CancellationTokenSource();
            using var channel = new UIReqsChannel(client.GetStream());
            HostChannel.Current = channel;
            AndroidLog.Info(Tag, "host runner connected");
            try
            {
                await ServeAsync(channel).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AndroidLog.Error(Tag, "host session ended with an error: " + exception);
            }
            finally
            {
                HostChannel.Current = null;
                _session.Cancel();
                AndroidLog.Info(Tag, "host runner disconnected");
            }
        }
    }

    private static async Task ServeAsync(UIReqsChannel channel)
    {
        while (await channel.ReceiveAsync().ConfigureAwait(false) is { } received)
        {
            var message = received.Message;
            JsonObject reply;
            try
            {
                reply = message.Str("op") switch
                {
                    "hello" => await HelloAsync(message).ConfigureAwait(false),
                    "beginScenario" => await BeginScenarioAsync(message).ConfigureAwait(false),
                    "step" => await StepAsync(message).ConfigureAwait(false),
                    "endScenario" => await EndScenarioAsync(message).ConfigureAwait(false),
                    "bye" => await ByeAsync().ConfigureAwait(false),
                    "ping" => new JsonObject { ["pong"] = true },
                    var op => new JsonObject { ["error"] = "unknown op " + op },
                };
            }
            catch (Exception exception)
            {
                reply = new JsonObject { ["error"] = exception.ToString() };
            }

            await channel.SendAsync(reply).ConfigureAwait(false);
            if (message.Str("op") == "bye")
            {
                return;
            }
        }
    }

    private static async Task<JsonObject> HelloAsync(JsonObject message)
    {
        AppHost.RequestedOrientation = Enum.Parse<TestDisplayOrientation>(message.Str("orientation") ?? "Portrait", ignoreCase: true);
        AppHost.PreserveDeviceConfiguration = message.Bool("preserveConfiguration");
        var output = new List<string>();
        if (!_testRunStarted)
        {
            _testRunStarted = true;
            var scope = new BindingScope(new FeatureContext(new FeatureInfo("(test run)", string.Empty, Array.Empty<string>())), new ScenarioContext(new ScenarioInfo("(test run)", Array.Empty<string>(), Array.Empty<string>())));
            await scope.RunHooksAsync(_catalog!.Hooks<BeforeTestRunAttribute>()).ConfigureAwait(false);
            output.AddRange(scope.TakeOutput());
        }

        var (width, height, density) = AppHost.Session.Panel();
        return new JsonObject
        {
            ["ok"] = true,
            ["width"] = width,
            ["height"] = height,
            ["density"] = density,
            ["orientation"] = AppHost.RequestedOrientation.ToString(),
            ["steps"] = _catalog!.StepCount,
            ["output"] = output.ToJsonArray(),
        };
    }

    /// <summary>The add-in group of a feature (its folder's last segment; AddInScope).</summary>
    private static string FeatureGroup(FeatureContext feature) => AddInScope.GroupOfFolder(feature.FeatureInfo.FolderPath);

    private static async Task<JsonObject> BeginScenarioAsync(JsonObject message)
    {
        var featureTitle = message.Str("feature") ?? string.Empty;
        if (!_features.TryGetValue(featureTitle, out var feature))
        {
            feature = new FeatureContext(new FeatureInfo(featureTitle, message.Str("folder") ?? string.Empty, message.Strings("featureTags")));
            _features[featureTitle] = feature;
        }

        var scenario = new ScenarioContext(new ScenarioInfo(message.Str("scenario") ?? string.Empty, message.Strings("tags"), message.Strings("combinedTags")));
        _scope = new BindingScope(feature, scenario);
        try
        {
            await _scope.RunHooksAsync(AddInScope.Filter(_catalog!.Hooks<BeforeScenarioAttribute>(), FeatureGroup(feature))).ConfigureAwait(false);
            return new JsonObject { ["ok"] = true, ["output"] = _scope.TakeOutput().ToJsonArray() };
        }
        catch (ScenarioSkippedException skipped)
        {
            scenario.ScenarioExecutionStatus = ScenarioExecutionStatus.Skipped;
            return new JsonObject { ["ok"] = true, ["skip"] = skipped.Message, ["output"] = _scope.TakeOutput().ToJsonArray() };
        }
    }

    private static async Task<JsonObject> StepAsync(JsonObject message)
    {
        var scope = _scope ?? throw new InvalidOperationException("A step arrived outside a scenario.");
        var keyword = Enum.Parse<StepKeyword>(message.Str("keyword") ?? "Given", ignoreCase: true);
        var text = message.Str("text") ?? string.Empty;
        DataTable? table = null;
        if (message["table"] is JsonObject tableNode)
        {
            var header = tableNode.Strings("header");
            var rows = ((JsonArray?)tableNode["rows"] ?? new JsonArray()).Select(r => (IReadOnlyList<string>)((JsonArray)r!).Select(c => c!.GetValue<string>()).ToList());
            table = new DataTable(header, rows);
        }

        // AP7: an add-in group's steps bind only its own features (AddInScope).
        var featureGroup = FeatureGroup(scope.FeatureContext);
        var matches = _catalog!.Match(keyword, text).Where(m => AddInScope.Applies(m.Definition.Method, featureGroup)).ToList();
        if (matches.Count == 0)
        {
            return Result("undefined", $"No {keyword} step definition matches \"{text}\" on the device.", scope);
        }

        // AP7-B AdvancedTextEdit: on the Platform an add-in group is its own test assembly and sees only its own steps
        // and the core harness's; here the Android-only groups' steps (AndroidSteps/) are global too. When a copied
        // add-in group's own step and an Android-only step share a text (the AdvancedTextEdit group's "the point x, y
        // inside ... is tapped" and AndroidShapes'), the feature's own group wins, as it does on the Platform.
        if (matches.Count > 1 && featureGroup.Length > 0)
        {
            var own = matches.Where(m => AddInScope.GroupOf(m.Definition.Method.DeclaringType!) == featureGroup).ToList();
            if (own.Count == 1)
            {
                matches = own;
            }
        }

        if (matches.Count > 1)
        {
            return Result("failed", $"Ambiguous step \"{text}\": " + string.Join(" | ", matches.Select(m => m.Definition.Method.DeclaringType!.Name + "." + m.Definition.Method.Name)), scope);
        }

        var (definition, raw) = matches[0];
        try
        {
            var parameters = definition.Method.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(DataTable))
                {
                    arguments[i] = table ?? throw new InvalidOperationException($"\"{text}\" needs a table.");
                }
                else
                {
                    arguments[i] = _catalog.Convert(raw[i], parameters[i].ParameterType, scope.Get);
                }
            }

            var target = definition.Method.IsStatic ? null : scope.Get(definition.Method.DeclaringType!);
            if (StepCatalog.Invoke(definition.Method, target, arguments) is Task task)
            {
                await task.ConfigureAwait(false);
            }

            return Result("passed", null, scope);
        }
        catch (ScenarioSkippedException skipped)
        {
            scope.ScenarioContext.ScenarioExecutionStatus = ScenarioExecutionStatus.Skipped;
            return Result("skipped", skipped.Message, scope);
        }
        catch (Exception exception) when (exception is StepPendingException or NotImplementedException)
        {
            scope.ScenarioContext.ScenarioExecutionStatus = ScenarioExecutionStatus.StepDefinitionPending;
            return Result("pending", exception.Message, scope);
        }
        catch (Exception exception)
        {
            scope.ScenarioContext.ScenarioExecutionStatus = ScenarioExecutionStatus.TestError;
            return Result("failed", exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace, scope);
        }
    }

    private static async Task<JsonObject> EndScenarioAsync(JsonObject message)
    {
        var scope = _scope;
        if (scope == null)
        {
            return new JsonObject { ["ok"] = true };
        }

        if (message.Bool("failed") && scope.ScenarioContext.ScenarioExecutionStatus == ScenarioExecutionStatus.OK)
        {
            scope.ScenarioContext.ScenarioExecutionStatus = ScenarioExecutionStatus.TestError;
        }

        string? error = null;
        try
        {
            await scope.RunHooksAsync(AddInScope.Filter(_catalog!.Hooks<AfterScenarioAttribute>(), FeatureGroup(scope.FeatureContext))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }

        _scope = null;
        return new JsonObject
        {
            ["ok"] = error == null,
            ["error"] = error,
            ["canvasReport"] = CanvasAssert.LastReport,
            ["output"] = scope.TakeOutput().ToJsonArray(),
        };
    }

    private static async Task<JsonObject> ByeAsync()
    {
        var scope = new BindingScope(new FeatureContext(new FeatureInfo("(test run)", string.Empty, Array.Empty<string>())), new ScenarioContext(new ScenarioInfo("(test run)", Array.Empty<string>(), Array.Empty<string>())));
        await scope.RunHooksAsync(_catalog!.Hooks<AfterTestRunAttribute>()).ConfigureAwait(false);
        return new JsonObject { ["ok"] = true };
    }

    private static JsonObject Result(string status, string? message, BindingScope scope)
    {
        var attachments = new JsonArray();
        foreach (var (name, _, mediaType) in Xunit.TestContext.Current.TakeAttachments())
        {
            attachments.Add(name + " (" + mediaType + ")");
        }

        return new JsonObject
        {
            ["status"] = status,
            ["message"] = message,
            ["output"] = scope.TakeOutput().ToJsonArray(),
            ["attachments"] = attachments,
        };
    }
}
