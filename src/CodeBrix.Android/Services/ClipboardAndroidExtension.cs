// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UWP/ApplicationModel/DataTransfer/Clipboard.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeBrix.Android.Android;
using CodeBrix.Platform.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer;
using AClipboardManager = global::Android.Content.ClipboardManager;
using AClipData = global::Android.Content.ClipData;
using AClipDescription = global::Android.Content.ClipDescription;
using AContext = global::Android.Content.Context;
using AHandler = global::Android.OS.Handler;
using ALooper = global::Android.OS.Looper;
using AUri = global::Android.Net.Uri;

namespace CodeBrix.Android.Services;

//was previously: public static partial class Clipboard (the static WinRT class, platform half)
/// <summary>
/// The Android clipboard behind Windows.ApplicationModel.DataTransfer.Clipboard (plan 2.3 registry row): text,
/// links and HTML go to the ClipboardManager as ClipData items; a bitmap goes as a content URI of a PNG in the
/// app's cache, shared through the CodeBrix FileProvider (<see cref="SharedFiles"/>). Reading builds a
/// DataPackage from the primary clip. ContentChanged follows PrimaryClipChanged.
/// </summary>
internal sealed partial class ClipboardAndroidExtension : IClipboardExtension
{
    private const string ClipboardDataLabel = "CodeBrix";

    private EventHandler<object> _contentChanged;

    /// <inheritdoc />
    public event EventHandler<object> ContentChanged
    {
        add => _contentChanged += value;
        remove => _contentChanged -= value;
    }

    private static AClipboardManager Manager => AndroidContext.Current.GetSystemService(AContext.ClipboardService) as AClipboardManager;

    /// <inheritdoc />
    public void SetContent(DataPackage content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The formats are read asynchronously; the clip is set once all of them are ready, on the main looper.
        _ = SetContentAsync(content);
    }

    /// <summary>Puts the package's formats on the clipboard (awaitable; tests use it).</summary>
    internal async Task SetContentAsync(DataPackage content)
    {
        var data = content?.GetView();
        var items = new List<AClipData.Item>();
        var mimeTypes = new List<string>();

        if (data?.Contains(StandardDataFormats.Text) == true)
        {
            var text = await data.GetTextAsync();
            items.Add(new AClipData.Item(text));
            mimeTypes.Add(AClipDescription.MimetypeTextPlain);
        }

        if (data != null)
        {
            var uri = DataPackage.CombineUri(
                data.Contains(StandardDataFormats.WebLink) ? (await data.GetWebLinkAsync()).ToString() : null,
                data.Contains(StandardDataFormats.ApplicationLink) ? (await data.GetApplicationLinkAsync()).ToString() : null,
                data.Contains(StandardDataFormats.Uri) ? (await data.GetUriAsync()).ToString() : null);
            if (!string.IsNullOrEmpty(uri))
            {
                items.Add(new AClipData.Item(AUri.Parse(uri)));
                mimeTypes.Add(AClipDescription.MimetypeTextUrilist);
            }
        }

        if (data?.Contains(StandardDataFormats.Html) == true)
        {
            var html = await data.GetHtmlFormatAsync();
            var plainText = TagMatch().Replace(html, " ").Trim();
            items.Add(new AClipData.Item(plainText, html));
            mimeTypes.Add(AClipDescription.MimetypeTextHtml);
        }

        if (data?.Contains(StandardDataFormats.Bitmap) == true)
        {
            var bytes = await SharedFiles.ReadAllAsync(await data.GetBitmapAsync());
            if (bytes.Length > 0 && SharedFiles.Share(bytes, ImageFormats.FileNameFor(bytes, "clipboard")) is { } bitmapUri)
            {
                items.Add(new AClipData.Item(bitmapUri));
                mimeTypes.Add(ImageFormats.MimeTypeOf(bytes));
            }
        }

        RunOnMainLooper(() =>
        {
            if (items.Count == 0)
            {
                Clear();
                return;
            }

            var clip = new AClipData(new AClipDescription(ClipboardDataLabel, mimeTypes.ToArray()), items[0]);
            for (var i = 1; i < items.Count; i++)
            {
                clip.AddItem(items[i]);
            }

            if (Manager is { } manager)
            {
                manager.PrimaryClip = clip;
            }
        });
    }

    /// <inheritdoc />
    public DataPackageView GetContent()
    {
        var package = new DataPackage();
        var clip = Manager?.PrimaryClip;
        Uri applicationLink = null;
        string html = null;
        string text = null;
        Uri uri = null;
        Uri webLink = null;

        // Each format is used once: the last occurrence in the clip wins.
        for (var i = 0; clip != null && i < clip.ItemCount; i++)
        {
            if (clip.GetItemAt(i) is not { } item)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(item.Text))
            {
                text = item.Text;
            }

            if (item.Uri?.ToString() is { } itemUri && !itemUri.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
            {
                DataPackage.SeparateUri(itemUri, out var separatedWebLink, out var separatedApplicationLink);
                webLink = separatedWebLink != null ? new Uri(separatedWebLink) : null;
                applicationLink = separatedApplicationLink != null ? new Uri(separatedApplicationLink) : null;
                uri = new Uri(itemUri);
            }

            if (item.HtmlText != null)
            {
                html = item.HtmlText;
            }
        }

        if (applicationLink != null)
        {
            package.SetApplicationLink(applicationLink);
        }

        if (html != null)
        {
            package.SetHtmlFormat(html);
        }

        if (text != null)
        {
            package.SetText(text);
        }

        if (uri != null)
        {
            package.SetUri(uri);
        }

        if (webLink != null)
        {
            package.SetWebLink(webLink);
        }

        return package.GetView();
    }

    /// <inheritdoc />
    public void Clear() => RunOnMainLooper(() => Manager?.ClearPrimaryClip());

    /// <inheritdoc />
    public void Flush()
    {
        // Android keeps the clip after the app exits.
    }

    /// <inheritdoc />
    public void StartContentChanged() => RunOnMainLooper(() =>
    {
        if (Manager is { } manager)
        {
            manager.PrimaryClipChanged += OnPrimaryClipChanged;
        }
    });

    /// <inheritdoc />
    public void StopContentChanged() => RunOnMainLooper(() =>
    {
        if (Manager is { } manager)
        {
            manager.PrimaryClipChanged -= OnPrimaryClipChanged;
        }
    });

    private static void RunOnMainLooper(Action action)
    {
        if (ALooper.MyLooper() == ALooper.MainLooper)
        {
            action();
        }
        else
        {
            new AHandler(ALooper.MainLooper).Post(action);
        }
    }

    [GeneratedRegex("(<.*?>\\s*)+", RegexOptions.Singleline)]
    private static partial Regex TagMatch();

    private void OnPrimaryClipChanged(object sender, EventArgs e) => _contentChanged?.Invoke(null, null);
}
