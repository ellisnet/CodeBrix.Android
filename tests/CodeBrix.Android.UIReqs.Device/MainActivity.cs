using Android.App;
using Android.OS;
using AndroidX.Core.View;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UIReqs.Device.Runtime;
using AChoreographer = Android.Views.Choreographer;

namespace CodeBrix.Android.UIReqs.Device;

/// <summary>
/// The scenario activity: full screen (system bars hidden, so a screencap is the app window and
/// nothing but the app), counts Android frames for the frame handshake, and starts the step server.
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
        AppHost.Activity = this;
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

    private void HideSystemBars()
    {
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
