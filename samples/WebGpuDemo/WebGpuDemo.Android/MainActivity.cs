using Android.App;
using Android.Content.PM;
using Android.OS;
using CodeBrix.Android.UI.Hosting;

namespace WebGpuDemo;

/// <summary>
/// The launcher activity. Reads the <c>page</c> intent extra (see <see cref="StartPages"/>) before the XAML application
/// starts, and starts the once-a-second frame-rate log (<see cref="FrameRateLog"/>). It is single-task, as a CodeBrix main
/// activity must be: a later launch (the launcher icon after <c>am start -n</c>, another <c>am start</c>) reaches the
/// running activity and <see cref="MainApplication"/>'s OnLaunchIntent logs it.
/// </summary>
[Activity(
    Name = "com.codebrix.webgpudemo.MainActivity",
    Label = "WebGpuDemo",
    MainLauncher = true,
    Theme = "@style/Theme.Material3.DayNight.NoActionBar",
    LaunchMode = MainActivity.MainLaunchMode,
    ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
public class MainActivity : CodeBrixActivity
{
    // Single-task (the requirement). -p:WebGpuDemoStandardLaunchMode=true builds the activity with the standard launch
    // mode instead - ONLY to check the host's safety net for apps that forgot it (a second instance forwards its launch
    // to the running activity and finishes; logcat shows the host's single-task warning).
#if WEBGPU_DEMO_STANDARD_LAUNCH_MODE
    internal const LaunchMode MainLaunchMode = LaunchMode.Multiple; // the manifest's launchMode="standard"
#else
    internal const LaunchMode MainLaunchMode = LaunchMode.SingleTask;
#endif

    /// <inheritdoc />
    protected override void OnCreate(Bundle savedInstanceState)
    {
        var page = Intent?.GetStringExtra("page");
        if (!string.IsNullOrEmpty(page))
        {
            StartPages.Requested = page;
        }

        base.OnCreate(savedInstanceState);
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        FrameRateLog.Start();
    }
}
