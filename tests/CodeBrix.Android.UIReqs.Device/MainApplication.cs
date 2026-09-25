using System;
using Android.App;
using Android.Runtime;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBrix.Android.UIReqs.Device;

/// <summary>The scenario app's Application: its XAML application is the copied UIReqs virtual application.</summary>
[Application]
public class MainApplication : CodeBrixApplication
{
    /// <summary>The constructor Android calls.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    /// <inheritdoc />
    protected override string LogTag => "UIReqs";

    /// <inheritdoc />
    protected override Microsoft.UI.Xaml.Application CreateApp() => new VirtualApplication();

    /// <inheritdoc />
    protected override void ConfigureLogging(ILoggingBuilder builder) => builder.SetMinimumLevel(LogLevel.Warning);
}
