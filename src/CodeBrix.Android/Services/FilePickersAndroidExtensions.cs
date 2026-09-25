using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.Android;
using CodeBrix.Platform.Extensions.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Pickers;
using AActivityFlags = global::Android.Content.ActivityFlags;
using ADocumentsContract = global::Android.Provider.DocumentsContract;
using AIntent = global::Android.Content.Intent;
using AMimeTypeMap = global::Android.Webkit.MimeTypeMap;
using AResult = global::Android.App.Result;
using AUri = global::Android.Net.Uri;

namespace CodeBrix.Android.Services;

/// <summary>
/// FileOpenPicker on Android: the Storage Access Framework document picker (ACTION_OPEN_DOCUMENT; ACTION_GET_CONTENT
/// through a chooser for the Pictures and Videos libraries, so gallery apps are offered), FileTypeFilter as MIME
/// types. Each picked document is a StorageFile over its content URI (StorageFile.FromImplementation with a
/// <see cref="ContentDocumentFile"/>, pin 1.0.268.12): nothing is copied unless the app reads the file's Path.
/// </summary>
internal sealed class FileOpenPickerAndroidExtension : IFileOpenPickerExtension
{
    private readonly FileOpenPicker _picker;

    /// <summary>Creates the extension of <paramref name="picker"/>.</summary>
    internal FileOpenPickerAndroidExtension(FileOpenPicker picker) => _picker = picker;

    /// <inheritdoc />
    public async Task<StorageFile> PickSingleFileAsync(CancellationToken token)
    {
        var files = await PickAsync(false, token).ConfigureAwait(false);
        return files.Count > 0 ? files[0] : null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(CancellationToken token) => PickAsync(true, token);

    /// <inheritdoc />
    public void Customize(FileOpenPicker picker)
    {
    }

    private async Task<IReadOnlyList<StorageFile>> PickAsync(bool multiple, CancellationToken token)
    {
        var bridge = AndroidActivityBridge.Required;
        var library = _picker.SuggestedStartLocation is PickerLocationId.PicturesLibrary or PickerLocationId.VideosLibrary;
        var intent = new AIntent(library ? AIntent.ActionGetContent : AIntent.ActionOpenDocument);
        intent.AddCategory(AIntent.CategoryOpenable);
        intent.SetType(PickerRequests.BaseMimeType(_picker.SuggestedStartLocation));
        intent.PutExtra(AIntent.ExtraAllowMultiple, multiple);
        if (!library && PickerRequests.FilterMimeTypes(_picker.FileTypeFilter, e => AMimeTypeMap.Singleton?.GetMimeTypeFromExtension(e)) is { } mimeTypes)
        {
            intent.PutExtra(AIntent.ExtraMimeTypes, mimeTypes);
        }

        var request = library ? AIntent.CreateChooser(intent, string.Empty) : intent;
        var (result, data) = await bridge.StartActivityForResultAsync(request, token).ConfigureAwait(false);
        if (result != AResult.Ok || data == null)
        {
            return Array.Empty<StorageFile>();
        }

        var uris = new List<AUri>();
        if (data.ClipData is { } clip)
        {
            for (var i = 0; i < clip.ItemCount; i++)
            {
                if (clip.GetItemAt(i)?.Uri is { } uri)
                {
                    uris.Add(uri);
                }
            }
        }
        else if (data.Data is { } single)
        {
            uris.Add(single);
        }

        var files = new List<StorageFile>();
        foreach (var uri in uris)
        {
            PickerGrants.KeepRead(uri, data.Flags);
            var info = ContentDocuments.Query(uri);
            files.Add(StorageFile.FromImplementation(new ContentDocumentFile(uri, info.Name, info.MimeType, createdForSave: false)));
        }

        return files;
    }
}

/// <summary>
/// FileSavePicker on Android: ACTION_CREATE_DOCUMENT with the suggested name and the save extension's MIME type. The
/// StorageFile returned is the created document itself (StorageFile.FromImplementation with a
/// <see cref="ContentDocumentFile"/>): the app's writes through the file's streams go to the document; code that
/// writes the file's Path gets a cache file copied back to the document after each write.
/// </summary>
internal sealed class FileSavePickerAndroidExtension : IFileSavePickerExtension
{
    private readonly FileSavePicker _picker;

    /// <summary>Creates the extension of <paramref name="picker"/>.</summary>
    internal FileSavePickerAndroidExtension(FileSavePicker picker) => _picker = picker;

    /// <inheritdoc />
    public async Task<StorageFile> PickSaveFileAsync(CancellationToken token)
    {
        var bridge = AndroidActivityBridge.Required;
        var extension = PickerRequests.SaveExtension(_picker.DefaultFileExtension, _picker.FileTypeChoices);
        var mime = string.IsNullOrEmpty(extension) ? null : AMimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension.TrimStart('.').ToLowerInvariant());
        var intent = new AIntent(AIntent.ActionCreateDocument);
        intent.AddCategory(AIntent.CategoryOpenable);
        intent.SetType(string.IsNullOrEmpty(mime) ? "application/octet-stream" : mime);
        var title = PickerRequests.SaveTitle(_picker.SuggestedFileName, extension);
        intent.PutExtra(AIntent.ExtraTitle, title);

        var (result, data) = await bridge.StartActivityForResultAsync(intent, token).ConfigureAwait(false);
        if (result != AResult.Ok || data?.Data is not { } uri)
        {
            return null;
        }

        var info = ContentDocuments.Query(uri);
        return StorageFile.FromImplementation(new ContentDocumentFile(uri, info.Name ?? title, info.MimeType ?? mime, createdForSave: true));
    }

    /// <inheritdoc />
    public void Customize(FileSavePicker picker)
    {
    }
}

/// <summary>
/// FolderPicker on Android: ACTION_OPEN_DOCUMENT_TREE. The chosen tree is a StorageFolder over the document tree
/// (StorageFolder.FromImplementation with a <see cref="ContentDocumentFolder"/>, pin 1.0.268.12 - before it the
/// picker returned null): list, look up, create and delete files and folders; the grant is kept across restarts.
/// Its Path is empty (a document tree has no file-system path).
/// </summary>
internal sealed class FolderPickerAndroidExtension : IFolderPickerExtension
{
    /// <inheritdoc />
    public async Task<StorageFolder> PickSingleFolderAsync(CancellationToken token)
    {
        var bridge = AndroidActivityBridge.Required;
        var intent = new AIntent(AIntent.ActionOpenDocumentTree);
        var (result, data) = await bridge.StartActivityForResultAsync(intent, token).ConfigureAwait(false);
        if (result != AResult.Ok || data?.Data is not { } tree)
        {
            return null;
        }

        PickerGrants.KeepReadWrite(tree, data.Flags);
        var documentId = ADocumentsContract.GetTreeDocumentId(tree);
        var info = ContentDocuments.Query(ADocumentsContract.BuildDocumentUriUsingTree(tree, documentId));
        return StorageFolder.FromImplementation(new ContentDocumentFolder(tree, documentId, info.Name));
    }

    /// <inheritdoc />
    public void Customize(FolderPicker picker)
    {
    }
}

/// <summary>Keeps the permission a picker granted, so a StorageFile / StorageFolder stays usable after a restart.</summary>
internal static class PickerGrants
{
    /// <summary>Takes the persistable read grant of a picked document, when the picker offered one.</summary>
    internal static void KeepRead(AUri uri, AActivityFlags flags) => Keep(uri, flags, AActivityFlags.GrantReadUriPermission);

    /// <summary>Takes the persistable read and write grant of a picked tree, when the picker offered one.</summary>
    internal static void KeepReadWrite(AUri uri, AActivityFlags flags) =>
        Keep(uri, flags, AActivityFlags.GrantReadUriPermission | AActivityFlags.GrantWriteUriPermission);

    private static void Keep(AUri uri, AActivityFlags flags, AActivityFlags wanted)
    {
        if ((flags & AActivityFlags.GrantPersistableUriPermission) == 0)
        {
            return;
        }

        try
        {
            ContentDocuments.Resolver.TakePersistableUriPermission(uri, flags & wanted);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // ACTION_GET_CONTENT results are not persistable: the grant lasts while the app runs.
            ServiceLog.Info($"The grant of {uri} is not persistable: {exception.Message}");
        }
    }
}
