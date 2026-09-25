// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UWP/ApplicationModel/DataTransfer/DataTransferManager.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Platform.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer;
using AActivityFlags = global::Android.Content.ActivityFlags;
using AClipData = global::Android.Content.ClipData;
using AIntent = global::Android.Content.Intent;

namespace CodeBrix.Android.Services;

//was previously: public partial class DataTransferManager (platform half: IsSupported / ShowShareUIAsync)
/// <summary>
/// DataTransferManager.ShowShareUI on Android: the system share sheet (ACTION_SEND through a chooser). Text and
/// links are shared as text (one per line); a bitmap is shared as an image through the CodeBrix FileProvider;
/// DataPackage.Properties.Title becomes the subject and the chooser title.
/// </summary>
internal sealed class DataTransferManagerAndroidExtension : IDataTransferManagerExtension
{
    /// <inheritdoc />
    public bool IsSupported() => true;

    /// <inheritdoc />
    public async Task<bool> ShowShareUIAsync(ShareUIOptions options, DataPackage dataPackage)
    {
        if (AndroidActivityBridge.Current?.CurrentActivity is not { } activity || dataPackage == null)
        {
            return false;
        }

        var view = dataPackage.GetView();
        var lines = new List<string>();
        if (view.Contains(StandardDataFormats.Text))
        {
            lines.Add(await view.GetTextAsync());
        }

        if (view.Contains(StandardDataFormats.WebLink))
        {
            lines.Add((await view.GetWebLinkAsync()).OriginalString);
        }
        else if (view.Contains(StandardDataFormats.ApplicationLink))
        {
            lines.Add((await view.GetApplicationLinkAsync()).OriginalString);
        }
        else if (view.Contains(StandardDataFormats.Uri))
        {
            lines.Add((await view.GetUriAsync()).OriginalString);
        }

        var intent = new AIntent(AIntent.ActionSend);
        if (view.Contains(StandardDataFormats.Bitmap))
        {
            var bytes = await SharedFiles.ReadAllAsync(await view.GetBitmapAsync());
            if (bytes.Length > 0 && SharedFiles.Share(bytes, ImageFormats.FileNameFor(bytes, "shared")) is { } uri)
            {
                intent.SetType(ImageFormats.MimeTypeOf(bytes));
                intent.PutExtra(AIntent.ExtraStream, uri);
                intent.ClipData = AClipData.NewRawUri(string.Empty, uri);
                intent.AddFlags(AActivityFlags.GrantReadUriPermission);
            }
        }

        if (intent.Type == null)
        {
            intent.SetType("text/plain");
        }

        if (lines.Count > 0)
        {
            intent.PutExtra(AIntent.ExtraText, string.Join(Environment.NewLine, lines));
        }

        var title = dataPackage.Properties?.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            intent.PutExtra(AIntent.ExtraSubject, title);
        }

        var chooser = AIntent.CreateChooser(intent, title ?? string.Empty);
        activity.StartActivity(chooser);
        return true;
    }
}
