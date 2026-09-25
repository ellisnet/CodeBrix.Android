// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UWP/System/Launcher.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Threading.Tasks;
using CodeBrix.Android.Android;
using CodeBrix.Platform.Extensions.System;
using Windows.System;
using AActivityFlags = global::Android.Content.ActivityFlags;
using AActivityNotFoundException = global::Android.Content.ActivityNotFoundException;
using AIntent = global::Android.Content.Intent;
using APackageManager = global::Android.Content.PM.PackageManager;
using AUri = global::Android.Net.Uri;

namespace CodeBrix.Android.Services;

//was previously: public static partial class Launcher (platform half: LaunchUriPlatformAsync / QueryUriSupportPlatformAsync)
/// <summary>
/// Windows.System.Launcher on Android: a URI opens with an ACTION_VIEW intent (the browser for http(s), the
/// mail app for mailto:, the dialer for tel:, ...); QueryUriSupportAsync asks the package manager whether any
/// activity handles it. The Windows "ms-settings:" URIs are not mapped.
/// </summary>
internal sealed class LauncherAndroidExtension : ILauncherExtension
{
    /// <inheritdoc />
    public Task<bool> LaunchUriAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        try
        {
            var intent = new AIntent(AIntent.ActionView, AUri.Parse(uri.OriginalString));
            if (AndroidActivityBridge.Current?.CurrentActivity is { } activity)
            {
                activity.StartActivity(intent);
            }
            else
            {
                intent.AddFlags(AActivityFlags.NewTask);
                AndroidContext.Current.StartActivity(intent);
            }

            return Task.FromResult(true);
        }
        catch (AActivityNotFoundException)
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<LaunchQuerySupportStatus> QueryUriSupportAsync(Uri uri, LaunchQuerySupportType launchQuerySupportType)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var intent = new AIntent(AIntent.ActionView, AUri.Parse(uri.OriginalString));
        var matches = AndroidContext.Current.PackageManager!.QueryIntentActivities(
            intent,
            APackageManager.ResolveInfoFlags.Of((long)global::Android.Content.PM.PackageInfoFlags.MatchDefaultOnly));
        return Task.FromResult(matches is { Count: > 0 } ? LaunchQuerySupportStatus.Available : LaunchQuerySupportStatus.NotSupported);
    }
}
