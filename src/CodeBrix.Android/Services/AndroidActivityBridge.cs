using System;
using System.Threading;
using System.Threading.Tasks;
using AActivity = global::Android.App.Activity;
using AIntent = global::Android.Content.Intent;
using AResult = global::Android.App.Result;

namespace CodeBrix.Android.Services;

/// <summary>
/// What the WinRT services of this assembly need from the hosting layer (CodeBrix.Android.UI): the activity
/// the app shows now, and a way to start a system activity (a document picker, a chooser) and get its result
/// without an Activity.OnActivityResult override.
/// </summary>
internal interface IAndroidActivityBridge
{
    /// <summary>The resumed CodeBrix activity, else the most recent live one; null before the first.</summary>
    AActivity CurrentActivity { get; }

    /// <summary>Starts <paramref name="intent"/> for a result from the current activity.</summary>
    /// <param name="intent">The intent.</param>
    /// <param name="cancellationToken">Cancels the wait (the system activity stays up).</param>
    /// <returns>The result code and the result intent (null when there is none).</returns>
    Task<(AResult ResultCode, AIntent Data)> StartActivityForResultAsync(AIntent intent, CancellationToken cancellationToken);

    /// <summary>
    /// A content:// URI other apps can read <paramref name="filePath"/> through (the CodeBrix FileProvider);
    /// the file must be under <see cref="SharedFiles.Directory"/>.
    /// </summary>
    /// <param name="filePath">The file.</param>
    /// <returns>The URI.</returns>
    global::Android.Net.Uri GetShareableUri(string filePath);
}

/// <summary>The bridge the hosting layer installed (CodeBrix.Android.UI does, when its bootstrap runs).</summary>
internal static class AndroidActivityBridge
{
    /// <summary>The installed bridge, or null.</summary>
    internal static IAndroidActivityBridge Current { get; set; }

    /// <summary>The installed bridge; throws when there is none or no activity yet.</summary>
    internal static IAndroidActivityBridge Required =>
        Current is { CurrentActivity: not null } bridge
            ? bridge
            : throw new InvalidOperationException("No CodeBrix activity is showing yet: the API was called too early in the application's life.");
}
