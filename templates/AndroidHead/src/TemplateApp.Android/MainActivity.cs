using Android.App;
using Android.Content.PM;
using CodeBrix.Android.UI.Hosting;

namespace TemplateApp;

/// <summary>
/// The launcher activity that shows the app's XAML window. HandledConfigurationChanges keeps the
/// activity alive when the window is resized, docked, rotated or switched to dark mode. SingleTask is
/// required: the app has one XAML window, so a repeated launch (launcher icon, deep link, notification)
/// reaches this running activity and arrives in MainApplication's OnLaunchIntent.
/// </summary>
[Activity(
    Label = "TemplateApp",
    MainLauncher = true,
    Theme = "@style/Theme.Material3.DayNight.NoActionBar",
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
public class MainActivity : CodeBrixActivity
{
}
