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
/// A file:// URI - what desktop code hands over for a file it saved (new Uri(path)) - cannot be given to another app
/// on Android (FileUriExposedException), so it is opened as a content URI with read permission granted to the
/// viewer: the picked or saved document itself when the path is a picker's compatibility path, otherwise a copy of
/// the file shared through the CodeBrix FileProvider.
/// </summary>
internal sealed class LauncherAndroidExtension : ILauncherExtension
{
    /// <inheritdoc />
    public Task<bool> LaunchUriAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        try
        {
            var intent = ViewIntentFor(uri);
            if (intent == null)
            {
                return Task.FromResult(false);
            }

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
        var intent = ViewIntentFor(uri);
        if (intent == null)
        {
            return Task.FromResult(LaunchQuerySupportStatus.NotSupported);
        }

        var matches = AndroidContext.Current.PackageManager!.QueryIntentActivities(
            intent,
            APackageManager.ResolveInfoFlags.Of((long)global::Android.Content.PM.PackageInfoFlags.MatchDefaultOnly));
        return Task.FromResult(matches is { Count: > 0 } ? LaunchQuerySupportStatus.Available : LaunchQuerySupportStatus.NotSupported);
    }

    /// <summary>
    /// The ACTION_VIEW intent for <paramref name="uri"/>, or null when there is nothing to open (a file:// URI whose
    /// file does not exist). A file:// URI becomes a content URI with read permission granted (see the class remarks).
    /// </summary>
    /// <param name="uri">The URI to open.</param>
    /// <returns>The intent, or null.</returns>
    internal static AIntent ViewIntentFor(Uri uri)
    {
        if (!uri.IsFile)
        {
            return new AIntent(AIntent.ActionView, AUri.Parse(uri.OriginalString));
        }

        var path = uri.LocalPath;
        var name = System.IO.Path.GetFileName(path);
        var content = ContentDocuments.DocumentForPath(path);
        if (content == null)
        {
            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            content = SharedFiles.Share(System.IO.File.ReadAllBytes(path), name);
            if (content == null)
            {
                return null;
            }
        }

        var intent = new AIntent(AIntent.ActionView);
        intent.SetDataAndType(content, ContentDocuments.MimeTypeOf(name));
        intent.AddFlags(AActivityFlags.GrantReadUriPermission);
        return intent;
    }
}
