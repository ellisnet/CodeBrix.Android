using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Hosting;
using CodeBrix.Android.UIReqs.Protocol;
using Reqnroll;
using Reqnroll.UnitTestProvider;
using Xunit;

namespace CodeBrix.Android.UIReqs.Steps;

/// <summary>
/// The host half's only step definitions: every step of the copied feature files is sent to
/// the device, where the copied step definitions run in the app (Runtime/StepServer); the
/// result comes back as passed / failed (with the message, canvas vocabulary included) /
/// pending / skipped. Appearance steps evaluate the screencaps this host took.
/// </summary>
[Binding]
public sealed class ForwardingSteps
{
    private readonly ScenarioContext _scenarioContext;
    private readonly IReqnrollOutputHelper _output;
    private readonly IUnitTestRuntimeProvider _runtime;

    /// <summary>Creates the steps.</summary>
    public ForwardingSteps(ScenarioContext scenarioContext, IReqnrollOutputHelper output, IUnitTestRuntimeProvider runtime)
    {
        _scenarioContext = scenarioContext;
        _output = output;
        _runtime = runtime;
    }

    /// <summary>Any Given.</summary>
    [Given(@"^(.*)$")]
    public Task Given(string text) => RunAsync("Given", text, null);

    /// <summary>Any Given with a table.</summary>
    [Given(@"^(.*)$")]
    public Task GivenTable(string text, DataTable table) => RunAsync("Given", text, table);

    /// <summary>Any When.</summary>
    [When(@"^(.*)$")]
    public Task When(string text) => RunAsync("When", text, null);

    /// <summary>Any When with a table.</summary>
    [When(@"^(.*)$")]
    public Task WhenTable(string text, DataTable table) => RunAsync("When", text, table);

    /// <summary>Any Then.</summary>
    [Then(@"^(.*)$")]
    public Task Then(string text) => RunAsync("Then", text, null);

    /// <summary>Any Then with a table.</summary>
    [Then(@"^(.*)$")]
    public Task ThenTable(string text, DataTable table) => RunAsync("Then", text, table);

    private async Task RunAsync(string keyword, string text, DataTable? table)
    {
        var request = new JsonObject { ["op"] = "step", ["keyword"] = keyword, ["text"] = text };
        if (table != null)
        {
            var rows = new JsonArray();
            foreach (var row in table.Rows)
            {
                rows.Add(row.Values.ToJsonArray());
            }

            request["table"] = new JsonObject { ["header"] = table.Header.ToJsonArray(), ["rows"] = rows };
        }

        var reply = await DeviceSession.RequestAsync(request).ConfigureAwait(false);
        foreach (var line in reply.Strings("output"))
        {
            _output.WriteLine(line);
        }

        var message = reply.Str("message") ?? reply.Str("error") ?? string.Empty;
        switch (reply.Str("status"))
        {
            case "passed":
                return;
            case "skipped":
                _runtime.TestIgnore(message);
                return;
            case "pending":
                throw new PendingStepException(message);
            case "undefined":
                Assert.Fail("Undefined on the device: " + message);
                return;
            default:
                _scenarioContext["uireqs.failed"] = true;
                Assert.Fail(message);
                return;
        }
    }
}
