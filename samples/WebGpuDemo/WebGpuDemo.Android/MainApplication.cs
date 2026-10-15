using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;

namespace WebGpuDemo;

/// <summary>The Android application: the XAML application, and logging that lets the WebGpu add-in's lines through.</summary>
[Application]
public class MainApplication : CodeBrixApplication
{
    /// <summary>The constructor Android calls.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    /// <inheritdoc />
    protected override Microsoft.UI.Xaml.Application CreateApp() => new App();

    // Opt in to direct GPU rendering (EXPERIMENTAL, app-wide, one-way): the WebGpuRenderView then presents straight to a SurfaceView (render path DirectSurface).
    // Off by default; build with -p:WebGpuDemoDirectGpu=true (or uncomment the override) to turn it on.
#if WEBGPU_DEMO_DIRECT_GPU
    protected override bool UseDirectGpuRenderingMode => true;
#else
    // protected override bool UseDirectGpuRenderingMode => true;
#endif

    /// <summary>A later launch of the running app (see MainActivity): logged with its <c>page</c> extra, if any.</summary>
    protected override void OnLaunchIntent(Intent intent)
    {
        Android.Util.Log.Info("WebGpuDemo", $"LAUNCH INTENT action={intent.Action ?? "none"} page={intent.GetStringExtra("page") ?? "none"}");
    }

    /// <inheritdoc />
    protected override void ConfigureLogging(ILoggingBuilder builder)
    {
        // The add-in logs one line when a view gets its device (adapter, backend, render path) and one when the
        // application's direct GPU rendering opt-in cannot be honoured; everything else of the framework at Warning.
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddFilter("CodeBrix.Platform", LogLevel.Warning);
        builder.AddFilter("CodeBrix.Platform.UI.WebGpu", LogLevel.Information);
    }
}
