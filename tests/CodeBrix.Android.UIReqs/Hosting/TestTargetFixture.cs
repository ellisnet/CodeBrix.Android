using System.Reflection;
using CodeBrix.Android.UIReqs.TestTarget;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hosting;

/// <summary>
/// The host's view of the run, for the copied FrameArchive / FrameReview: the orientation this
/// run was declared for and the test assembly (its feature-root metadata). The running app and
/// its UI thread are on the device (tests/CodeBrix.Android.UIReqs.Device).
/// </summary>
public static class TestTargetFixture
{
    /// <summary>The orientation of this run.</summary>
    public static TestDisplayOrientation Orientation => CodeBrix.Android.UIReqs.Hosting.DeviceSession.Orientation;

    /// <summary>The assembly the scenarios run from.</summary>
    public static Assembly TestAssembly => typeof(TestTargetFixture).Assembly;
}
