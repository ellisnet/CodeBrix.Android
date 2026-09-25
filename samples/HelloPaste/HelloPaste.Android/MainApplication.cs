using System;
using Android.App;
using Android.Runtime;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;

namespace HelloPaste;

/// <summary>The Android application: logs the visual tree after each layout change (the smoke script reads it).</summary>
[Application]
public class MainApplication : CodeBrixApplication
{
    /// <summary>The constructor Android calls.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    /// <inheritdoc />
    protected override bool LogVisualTreeAfterLayout => true;

    /// <inheritdoc />
    protected override Microsoft.UI.Xaml.Application CreateApp() => new App();

    /// <inheritdoc />
    protected override void ConfigureLogging(ILoggingBuilder builder)
    {
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddFilter("CodeBrix.Android", LogLevel.Debug);
    }
}
