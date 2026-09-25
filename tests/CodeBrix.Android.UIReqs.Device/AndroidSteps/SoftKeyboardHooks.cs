#nullable disable

using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Reqnroll;
using Xunit;
using AContext = Android.Content.Context;
using AHideSoftInputFlags = Android.Views.InputMethods.HideSoftInputFlags;
using AInputMethodManager = Android.Views.InputMethods.InputMethodManager;
using AWindowCompat = AndroidX.Core.View.WindowCompat;
using AWindowInsets = Android.Views.WindowInsets;
using AWindowInsetsCompat = AndroidX.Core.View.WindowInsetsCompat;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP7-M: every scenario starts with the soft keyboard down. A scenario that typed through a native text field (a real
/// keyboard, an AutoSuggestBox, a picker's text input) can leave the keyboard up when its content goes; run as one session
/// (android-uireqs-run.sh --group all), the next scenario's panel would then be partly covered by it. After every scenario
/// the keyboard is hidden and the hook waits until the window's insets say so (logcat "UIReqs.Keyboard" names the scenario
/// that left it up).
/// </summary>
[Binding]
public sealed class SoftKeyboardHooks
{
    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the hooks.</summary>
    public SoftKeyboardHooks(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    /// <summary>Hides a soft keyboard the scenario left up (before the panel is emptied).</summary>
    [AfterScenario(Order = -30)]
    public async Task Hide_the_soft_keyboard()
    {
        var shown = false;
        await TestTargetFixture.RunOnUIThreadAsync(() => shown = HideIfShown()).ConfigureAwait(false);
        if (!shown)
        {
            return;
        }

        global::Android.Util.Log.Info("UIReqs.Keyboard", $"the scenario \"{_scenarioContext.ScenarioInfo.Title}\" left the soft keyboard up; hidden");
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);
            var still = true;
            await TestTargetFixture.RunOnUIThreadAsync(() => still = IsShown()).ConfigureAwait(false);
            if (!still)
            {
                break;
            }

            if (i % 10 == 9)
            {
                await TestTargetFixture.RunOnUIThreadAsync(() => { HideIfShown(); }).ConfigureAwait(false);
            }
        }

        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static bool IsShown() =>
        AppHost.Activity?.Window?.DecorView?.RootWindowInsets is { } insets && insets.IsVisible(AWindowInsets.Type.Ime());

    private static bool HideIfShown()
    {
        var activity = AppHost.Activity;
        var decor = activity?.Window?.DecorView;
        if (decor == null || !IsShown())
        {
            return false;
        }

        if (decor.WindowToken is { } token && activity.GetSystemService(AContext.InputMethodService) is AInputMethodManager ime)
        {
            ime.HideSoftInputFromWindow(token, AHideSoftInputFlags.None);
        }

        AWindowCompat.GetInsetsController(activity.Window, decor)?.Hide(AWindowInsetsCompat.Type.Ime());
        return true;
    }
}
