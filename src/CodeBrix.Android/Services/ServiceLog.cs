namespace CodeBrix.Android.Services;

/// <summary>Logcat output of the WinRT services (tag "CodeBrix.Android.Services").</summary>
internal static class ServiceLog
{
    private const string Tag = "CodeBrix.Android.Services";

    /// <summary>Writes a warning.</summary>
    internal static void Warn(string message) => global::Android.Util.Log.Warn(Tag, message);

    /// <summary>Writes an information line.</summary>
    internal static void Info(string message) => global::Android.Util.Log.Info(Tag, message);
}
