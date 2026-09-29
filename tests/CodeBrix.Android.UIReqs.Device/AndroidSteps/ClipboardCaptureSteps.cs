using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Reqnroll;
using SilverAssertions;
using AClipboardManager = global::Android.Content.ClipboardManager;
using AClipData = global::Android.Content.ClipData;
using AContext = global::Android.Content.Context;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>Exercises the real clipboard notification and capture handshake on the device.</summary>
[Binding]
public sealed class ClipboardCaptureSteps
{
    private readonly Stopwatch _elapsed = new();

    /// <summary>Copies only harness-owned text; each copy triggers a fresh notification.</summary>
    [Given("the system clipboard receives fresh harness text")]
    public async Task CopyHarnessText()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            AppHost.Activity!.HasWindowFocus.Should().BeTrue("a system overlay must not obscure the test activity");
            var clipboard = (AClipboardManager)AppHost.Activity!.GetSystemService(AContext.ClipboardService)!;
            _elapsed.Restart();
            clipboard.PrimaryClip = AClipData.NewPlainText("UIReqs", "UIReqs capture " + Guid.NewGuid().ToString("N"));
        }).ConfigureAwait(false);
    }

    /// <summary>The capture must actually wait, rather than merely registering a clipboard listener.</summary>
    [Then("the clipboard capture waited at least {int} seconds")]
    public void CaptureWaited(int seconds) =>
        _elapsed.Elapsed.TotalSeconds.Should().BeGreaterThanOrEqualTo(seconds,
            "the capture must let the clipboard overlay time out before reading the screen");
}
