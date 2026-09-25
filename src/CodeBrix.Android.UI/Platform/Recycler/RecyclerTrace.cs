namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>Diagnostics of the RecyclerView-backed lists (logcat tag CodeBrix.Recycler, verbose level).</summary>
internal static class RecyclerTrace
{
    /// <summary>The logcat tag.</summary>
    internal const string Tag = "CodeBrix.Recycler";

    /// <summary>True when verbose tracing is on (`adb shell setprop log.tag.CodeBrix.Recycler VERBOSE` before the app starts).</summary>
    internal static readonly bool IsEnabled = global::Android.Util.Log.IsLoggable(Tag, global::Android.Util.LogPriority.Verbose);

    /// <summary>Writes one trace line when tracing is on.</summary>
    internal static void Write(string message)
    {
        if (IsEnabled)
        {
            global::Android.Util.Log.Verbose(Tag, message);
        }
    }
}
