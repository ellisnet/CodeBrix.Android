using Android.App;
using Android.OS;
using AndroidX.Core.View;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UIReqs.Device.Runtime;
using AChoreographer = Android.Views.Choreographer;

namespace CodeBrix.Android.UIReqs.Device;

/// <summary>
/// The scenario activity counts Android frames for the frame handshake and starts the step server.
/// Normal baseline runs hide system bars; existing-device preservation runs leave them visible to
/// avoid first-use immersive-mode tutorials without changing any device setting.
/// </summary>
[Activity(
    Name = "com.codebrix.uireqs.MainActivity",
    Label = "CodeBrix UIReqs",
    MainLauncher = true,
    Theme = "@style/Theme.Material3.Light.NoActionBar",
    ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
public class MainActivity : CodeBrixActivity
{
    private FrameCounter? _frames;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // An app orientation request affects this activity only; no system rotation setting is written.
        var orientation = Intent?.GetStringExtra("uireqsOrientation");
        if (orientation == "Portrait")
        {
            RequestedOrientation = global::Android.Content.PM.ScreenOrientation.Portrait;
        }
        else if (orientation == "Landscape")
        {
            RequestedOrientation = global::Android.Content.PM.ScreenOrientation.Landscape;
        }

        AppHost.Activity = this;
        ClipboardCaptureWait.Start(this);
        CodeBrix.Android.UIReqs.Device.TestTarget.TestTargetSession.InstallCaretHook(this);
        HideSystemBars();
        _frames ??= new FrameCounter();
        AChoreographer.Instance!.PostFrameCallback(_frames);
        StepServer.Start();
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        HideSystemBars();
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        ClipboardCaptureWait.Stop();
        base.OnDestroy();
    }

    private void HideSystemBars()
    {
        // Existing devices can show a first-use immersive-mode tutorial over the app. Keeping
        // their system bars visible avoids that overlay without acknowledging a system prompt.
        if (Intent?.GetBooleanExtra("preserveConfiguration", false) == true)
        {
            return;
        }

        if (Window is not { } window)
        {
            return;
        }

        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller != null)
        {
            controller.Hide(WindowInsetsCompat.Type.SystemBars());
            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }
    }

    private sealed class FrameCounter : Java.Lang.Object, AChoreographer.IFrameCallback
    {
        public void DoFrame(long frameTimeNanos)
        {
            AppHost.Session.OnAndroidFrame();
            AChoreographer.Instance!.PostFrameCallback(this);
        }
    }
}
