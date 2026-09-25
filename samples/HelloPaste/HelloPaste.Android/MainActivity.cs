using Android.App;
using Android.OS;
using CodeBrix.Android.UI.Hosting;

namespace HelloPaste;

/// <summary>
/// The launcher activity. Reads the <c>page</c> intent extra (see <see cref="StartPages"/>)
/// before the XAML application starts, and schedules the on-device self-check once.
/// </summary>
[Activity(
    Name = "com.codebrix.hellopaste.MainActivity",
    Label = "HelloPaste",
    MainLauncher = true,
    Theme = "@style/Theme.Material3.DayNight.NoActionBar",
    ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
public class MainActivity : CodeBrixActivity
{
    private bool _selfCheckScheduled;

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
        if (!_selfCheckScheduled)
        {
            _selfCheckScheduled = true;
            SelfCheck.Runner.Schedule(this);
        }
    }
}
