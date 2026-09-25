#if __ANDROID__
using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.Services;
using CodeBrix.Android.UI.Hosting;
using AActivity = global::Android.App.Activity;
using AActivityResult = global::AndroidX.Activity.Result.ActivityResult;
using AIActivityResultCallback = global::AndroidX.Activity.Result.IActivityResultCallback;
using AIntent = global::Android.Content.Intent;
using AResult = global::Android.App.Result;
using AStartActivityForResult = global::AndroidX.Activity.Result.Contract.ActivityResultContracts.StartActivityForResult;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// The hosting layer's <see cref="IAndroidActivityBridge"/>: the WinRT services (pickers over the Storage Access
/// Framework, the share sheet, the launcher) show system UI over the app through it. Results come back through
/// the activity's AndroidX ActivityResultRegistry (one registration per request, released when the result
/// arrives), so CodeBrixActivity needs no OnActivityResult override.
/// </summary>
internal sealed class SystemUiBridge : IAndroidActivityBridge
{
    private static int _next;

    /// <summary>Installs the bridge for the WinRT services (once).</summary>
    internal static void Install() => AndroidActivityBridge.Current ??= new SystemUiBridge();

    /// <inheritdoc />
    public AActivity CurrentActivity => ActivityRegistry.Current;

    /// <inheritdoc />
    public Task<(AResult ResultCode, AIntent Data)> StartActivityForResultAsync(AIntent intent, CancellationToken cancellationToken)
    {
        if (ActivityRegistry.Current is not { IsDestroyed: false } activity)
        {
            throw new InvalidOperationException("No CodeBrix activity is showing to start the system activity from.");
        }

        var completion = new TaskCompletionSource<(AResult, AIntent)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var key = "codebrix.result." + Interlocked.Increment(ref _next);
        global::AndroidX.Activity.Result.ActivityResultLauncher launcher = null;
        var callback = new ResultCallback(result =>
        {
            launcher?.Unregister();
            completion.TrySetResult(((AResult)(result?.ResultCode ?? (int)AResult.Canceled), result?.Data));
        });
        launcher = activity.ActivityResultRegistry.Register(key, new AStartActivityForResult(), callback);
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        }

        launcher.Launch(intent);
        return completion.Task;
    }

    /// <inheritdoc />
    public global::Android.Net.Uri GetShareableUri(string filePath) => SharedFileProvider.UriFor(filePath);

    private sealed class ResultCallback : global::Java.Lang.Object, AIActivityResultCallback
    {
        private readonly Action<AActivityResult> _onResult;

        internal ResultCallback(Action<AActivityResult> onResult) => _onResult = onResult;

        public void OnActivityResult(global::Java.Lang.Object result) => _onResult(result as AActivityResult);
    }
}
#endif
