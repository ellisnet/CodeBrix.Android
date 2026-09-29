using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UIReqs.Device.TestTarget;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>What the runtime knows about the running app: its activity, the session and the requested orientation.</summary>
internal static class AppHost
{
    /// <summary>The scenario activity (null before it is created).</summary>
    internal static CodeBrixActivity? Activity { get; set; }

    /// <summary>The orientation the host runner declared for this run.</summary>
    internal static TestDisplayOrientation RequestedOrientation { get; set; } = TestDisplayOrientation.Portrait;

    /// <summary>True when the host forbids changing the device's existing configuration.</summary>
    internal static bool PreserveDeviceConfiguration { get; set; }

    /// <summary>The session over the running app.</summary>
    internal static TestTargetSession Session { get; } = new(Panel);

    private static (int Width, int Height, double Density) Panel()
    {
        var layout = Activity?.RootLayout;
        var density = Activity?.Resources?.DisplayMetrics?.Density ?? 1f;
        return layout == null ? (0, 0, density) : (layout.Width, layout.Height, density);
    }
}
