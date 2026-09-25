using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using Reqnroll;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

// On Android the HOST runner takes every screencap, so it owns the frame files: the real
// FrameArchive and FrameReview (copied verbatim) run in tests/CodeBrix.Android.UIReqs. These
// device-side classes keep the copied scenario code's calls and forward them to the host.

/// <summary>The failure-frame archive, forwarded to the host.</summary>
public static class FrameArchive
{
    /// <summary>The path of the last frame the host archived for the current scenario.</summary>
    public static string? LastSavedPath { get; private set; }

    /// <summary>Starts a scenario (the host tracks the names).</summary>
    public static void BeginScenario(string featureTitle, string scenarioTitle) => LastSavedPath = null;

    /// <summary>Asks the host to archive a frame; returns the host path or null.</summary>
    public static string? TrySave(TestFrame? frame)
    {
        if (frame is null)
        {
            return null;
        }

        LastSavedPath = HostChannel.SaveFrame("archive", frame.Sequence, null);
        return LastSavedPath;
    }

    /// <summary>Describes where failure frames go.</summary>
    public static string Describe() => "failure frames are archived by the host runner";
}

/// <summary>The opt-in review archive, forwarded to the host.</summary>
public static class FrameReview
{
    /// <summary>The host initialises the archive.</summary>
    public static void Initialize()
    {
    }

    /// <summary>The host tracks the scenario.</summary>
    public static void BeginScenario(FeatureContext featureContext, ScenarioContext scenarioContext)
    {
    }

    /// <summary>Asks the host to keep a captured frame under a label (when its archive is on).</summary>
    public static string? Save(TestFrame? frame, string? label = null) =>
        frame is null ? null : HostChannel.SaveFrame("review", frame.Sequence, label);

    /// <summary>Describes the review archive.</summary>
    public static string Describe() => "the review archive is the host runner's (CODEBRIX_UIREQS_FRAME_SAVE on the host)";
}
