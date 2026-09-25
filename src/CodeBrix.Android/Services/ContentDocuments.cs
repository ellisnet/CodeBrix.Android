using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.Android;
using CodeBrix.Android.Portable;
using Microsoft.Win32.SafeHandles;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using AContentResolver = global::Android.Content.ContentResolver;
using ADocument = global::Android.Provider.DocumentsContract.Document;
using ADocumentsContract = global::Android.Provider.DocumentsContract;
using AFileObserver = global::Android.OS.FileObserver;
using AFileObserverEvents = global::Android.OS.FileObserverEvents;
using AOpenableColumns = global::Android.Provider.IOpenableColumns;
using AUri = global::Android.Net.Uri;

namespace CodeBrix.Android.Services;

/// <summary>
/// A document of the Storage Access Framework (a content URI) as a CodeBrix.Platform StorageFile implementation
/// (pin 1.0.268.12, WPE1-1 C0b: StorageFile.FromImplementation). Streams, name, content type, size and delete go
/// to the document itself - nothing is copied up front (the AP4 pickers copied every picked document into the
/// cache).
/// </summary>
/// <remarks>
/// <para><see cref="Path"/> is the compatibility path for code written against a desktop head, which opens or writes
/// <c>file.Path</c> with System.IO (every picker flow of the paste-always corpus does): it is created on the FIRST
/// read of Path only - for an opened document a copy of it in the cache, for a document created by the save picker
/// an empty cache file whose content is written back to the document every time it is closed after writing. Code
/// that uses the StorageFile's own streams (FileIO, OpenStreamForReadAsync, OpenAsync) never makes one.</para>
/// <para>Not supported (NotSupportedException): OpenTransactedWriteAsync (a transaction commits by replacing a
/// file on disk), Rename, Copy/Move targets. GetParentAsync returns null (a single document grant has no parent).</para>
/// </remarks>
internal sealed class ContentDocumentFile : StorageFile.ImplementationBase
{
    private static readonly StorageProvider DocumentsProvider = new("Android.DocumentsProvider", "StorageProviderLocalDisplayName");

    private readonly object _pathGate = new();
    private readonly bool _createdForSave;
    private string _compatibilityPath;

    /// <summary>Creates the implementation of a document.</summary>
    /// <param name="uri">The document's content URI.</param>
    /// <param name="displayName">The document's display name (with its extension).</param>
    /// <param name="mimeType">The document's MIME type, or null.</param>
    /// <param name="createdForSave">True for a document the save picker created (its compatibility path writes back).</param>
    internal ContentDocumentFile(AUri uri, string displayName, string mimeType, bool createdForSave)
        : base(string.Empty)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        DocumentName = string.IsNullOrEmpty(displayName) ? PickerRequests.SafeFileName(uri.LastPathSegment, "document") : displayName;
        MimeType = mimeType;
        _createdForSave = createdForSave;
    }

    /// <summary>The document's content URI.</summary>
    internal AUri Uri { get; }

    /// <summary>The document's display name.</summary>
    internal string DocumentName { get; }

    /// <summary>The document's MIME type, or null.</summary>
    internal string MimeType { get; }

    /// <summary>True once the compatibility path exists (diagnostics, tests).</summary>
    internal bool HasCompatibilityPath => _compatibilityPath != null;

    /// <inheritdoc />
    public override StorageProvider Provider => DocumentsProvider;

    /// <inheritdoc />
    public override string Name => DocumentName;

    /// <inheritdoc />
    public override string DisplayName => System.IO.Path.GetFileNameWithoutExtension(DocumentName);

    /// <inheritdoc />
    public override string FileType => System.IO.Path.GetExtension(DocumentName);

    /// <inheritdoc />
    public override string ContentType => string.IsNullOrEmpty(MimeType) ? "application/octet-stream" : MimeType;

    /// <inheritdoc />
    public override string Path
    {
        get
        {
            lock (_pathGate)
            {
                return _compatibilityPath ??= _createdForSave
                    ? ContentDocuments.CreateWriteBackFile(Uri, DocumentName)
                    : ContentDocuments.CopyToCache(Uri, DocumentName);
            }
        }
    }

    /// <inheritdoc />
    public override DateTimeOffset DateCreated => ContentDocuments.Query(Uri).Modified;

    /// <inheritdoc />
    protected override bool IsEqual(StorageFile.ImplementationBase implementation) =>
        implementation is ContentDocumentFile other && other.Uri.Equals(Uri);

    /// <inheritdoc />
    public override Task<StorageFolder> GetParentAsync(CancellationToken ct) => Task.FromResult<StorageFolder>(null);

    /// <inheritdoc />
    public override Task<BasicProperties> GetBasicPropertiesAsync(CancellationToken ct)
    {
        var info = ContentDocuments.Query(Uri);
        return Task.FromResult(new BasicProperties((ulong)Math.Max(0, info.Size), info.Modified));
    }

    /// <inheritdoc />
    public override async Task<IRandomAccessStreamWithContentType> OpenAsync(CancellationToken ct, FileAccessMode accessMode, StorageOpenOptions options)
    {
        var stream = await OpenStreamAsync(ct, accessMode, options).ConfigureAwait(false);
        return new RandomAccessStreamWithContentType(stream, ContentType);
    }

    /// <inheritdoc />
    public override Task<Stream> OpenStreamAsync(CancellationToken ct, FileAccessMode accessMode, StorageOpenOptions options) =>
        Task.FromResult(ContentDocuments.OpenStream(Uri, accessMode == FileAccessMode.ReadWrite));

    /// <inheritdoc />
    public override Task<StorageStreamTransaction> OpenTransactedWriteAsync(CancellationToken ct, StorageOpenOptions option) =>
        throw NotSupported(nameof(OpenTransactedWriteAsync));

    /// <inheritdoc />
    public override Task DeleteAsync(CancellationToken ct, StorageDeleteOption options)
    {
        ContentDocuments.Delete(Uri);
        return Task.CompletedTask;
    }
}

/// <summary>
/// A document tree of the Storage Access Framework (ACTION_OPEN_DOCUMENT_TREE) or a directory inside one as a
/// CodeBrix.Platform StorageFolder implementation (WPE1-1 C0b: StorageFolder.FromImplementation): listing,
/// lookup by name, creating files and folders, delete. <see cref="StorageFolder.Path"/> is empty (a document tree
/// has no file-system path an app may open).
/// </summary>
internal sealed class ContentDocumentFolder : StorageFolder.ImplementationBase
{
    private static readonly StorageProvider DocumentsProvider = new("Android.DocumentsProvider", "StorageProviderLocalDisplayName");

    /// <summary>Creates the implementation of a directory of a tree.</summary>
    /// <param name="treeUri">The tree URI the user granted.</param>
    /// <param name="documentId">The directory's document id inside the tree.</param>
    /// <param name="displayName">The directory's display name.</param>
    internal ContentDocumentFolder(AUri treeUri, string documentId, string displayName)
        : base(string.Empty)
    {
        TreeUri = treeUri ?? throw new ArgumentNullException(nameof(treeUri));
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
        FolderName = displayName ?? documentId;
    }

    /// <summary>The granted tree.</summary>
    internal AUri TreeUri { get; }

    /// <summary>This directory's document id.</summary>
    internal string DocumentId { get; }

    /// <summary>This directory's display name.</summary>
    internal string FolderName { get; }

    /// <summary>This directory's document URI.</summary>
    internal AUri DocumentUri => ADocumentsContract.BuildDocumentUriUsingTree(TreeUri, DocumentId);

    /// <inheritdoc />
    public override StorageProvider Provider => DocumentsProvider;

    /// <inheritdoc />
    public override string Name => FolderName;

    /// <inheritdoc />
    public override string DisplayName => FolderName;

    /// <inheritdoc />
    protected override bool IsEqual(StorageFolder.ImplementationBase implementation) =>
        implementation is ContentDocumentFolder other && other.TreeUri.Equals(TreeUri) && other.DocumentId == DocumentId;

    /// <inheritdoc />
    public override async Task<StorageFile> CreateFileAsync(string desiredName, CreationCollisionOption options, CancellationToken cancellationToken)
    {
        var existing = Find(desiredName);
        if (existing != null)
        {
            switch (options)
            {
                case CreationCollisionOption.FailIfExists:
                    throw new IOException($"The item '{desiredName}' already exists.");
                case CreationCollisionOption.OpenIfExists:
                    if (!existing.IsDirectory)
                    {
                        return FileOf(existing);
                    }

                    throw new IOException($"'{desiredName}' is a folder.");
                case CreationCollisionOption.ReplaceExisting:
                    ContentDocuments.Delete(ADocumentsContract.BuildDocumentUriUsingTree(TreeUri, existing.DocumentId));
                    break;
                default:
                    desiredName = await FindAvailableNumberedFileNameAsync(desiredName).ConfigureAwait(false);
                    break;
            }
        }

        var mime = ContentDocuments.MimeTypeOf(desiredName);
        var created = ADocumentsContract.CreateDocument(ContentDocuments.Resolver, DocumentUri, mime, desiredName)
            ?? throw new IOException($"The document provider did not create '{desiredName}'.");
        var info = ContentDocuments.Query(created);
        return StorageFile.FromImplementation(new ContentDocumentFile(created, info.Name ?? desiredName, info.MimeType ?? mime, createdForSave: true));
    }

    /// <inheritdoc />
    public override async Task<StorageFolder> CreateFolderAsync(string folderName, CreationCollisionOption option, CancellationToken token)
    {
        var existing = Find(folderName);
        if (existing != null)
        {
            switch (option)
            {
                case CreationCollisionOption.FailIfExists:
                    throw new IOException($"The item '{folderName}' already exists.");
                case CreationCollisionOption.OpenIfExists:
                    if (existing.IsDirectory)
                    {
                        return FolderOf(existing);
                    }

                    throw new IOException($"'{folderName}' is a file.");
                case CreationCollisionOption.ReplaceExisting:
                    ContentDocuments.Delete(ADocumentsContract.BuildDocumentUriUsingTree(TreeUri, existing.DocumentId));
                    break;
                default:
                    folderName = await FindAvailableNumberedFolderNameAsync(folderName).ConfigureAwait(false);
                    break;
            }
        }

        var created = ADocumentsContract.CreateDocument(ContentDocuments.Resolver, DocumentUri, ADocument.MimeTypeDir, folderName)
            ?? throw new IOException($"The document provider did not create the folder '{folderName}'.");
        return StorageFolder.FromImplementation(new ContentDocumentFolder(TreeUri, ADocumentsContract.GetDocumentId(created), folderName));
    }

    /// <inheritdoc />
    public override Task<StorageFolder> GetFolderAsync(string name, CancellationToken token)
    {
        var child = Find(name);
        return child is { IsDirectory: true }
            ? Task.FromResult(FolderOf(child))
            : throw new FileNotFoundException($"There is no folder '{name}' in '{FolderName}'.");
    }

    /// <inheritdoc />
    public override Task<StorageFile> GetFileAsync(string name, CancellationToken token)
    {
        var child = Find(name);
        return child is { IsDirectory: false }
            ? Task.FromResult(FileOf(child))
            : throw new FileNotFoundException($"There is no file '{name}' in '{FolderName}'.");
    }

    /// <inheritdoc />
    public override Task<IStorageItem> GetItemAsync(string name, CancellationToken token)
    {
        var child = Find(name) ?? throw new FileNotFoundException($"There is no item '{name}' in '{FolderName}'.");
        return Task.FromResult(ItemOf(child));
    }

    /// <inheritdoc />
    public override Task<StorageFolder> GetParentAsync(CancellationToken token) => Task.FromResult<StorageFolder>(null);

    /// <inheritdoc />
    public override Task<IStorageItem> TryGetItemAsync(string name, CancellationToken token)
    {
        var child = Find(name);
        return Task.FromResult(child == null ? null : ItemOf(child));
    }

    /// <inheritdoc />
    public override Task<BasicProperties> GetBasicPropertiesAsync(CancellationToken ct)
    {
        var info = ContentDocuments.Query(DocumentUri);
        return Task.FromResult(new BasicProperties(0, info.Modified));
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<IStorageItem>> GetItemsAsync(CancellationToken ct)
    {
        var items = new List<IStorageItem>();
        foreach (var child in ContentDocuments.Children(TreeUri, DocumentId))
        {
            items.Add(ItemOf(child));
        }

        return Task.FromResult<IReadOnlyList<IStorageItem>>(items);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<StorageFile>> GetFilesAsync(CancellationToken ct)
    {
        var files = new List<StorageFile>();
        foreach (var child in ContentDocuments.Children(TreeUri, DocumentId))
        {
            if (!child.IsDirectory)
            {
                files.Add(FileOf(child));
            }
        }

        return Task.FromResult<IReadOnlyList<StorageFile>>(files);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<StorageFolder>> GetFoldersAsync(CancellationToken ct)
    {
        var folders = new List<StorageFolder>();
        foreach (var child in ContentDocuments.Children(TreeUri, DocumentId))
        {
            if (child.IsDirectory)
            {
                folders.Add(FolderOf(child));
            }
        }

        return Task.FromResult<IReadOnlyList<StorageFolder>>(folders);
    }

    /// <inheritdoc />
    public override Task DeleteAsync(StorageDeleteOption options, CancellationToken ct)
    {
        ContentDocuments.Delete(DocumentUri);
        return Task.CompletedTask;
    }

    private ContentDocuments.ChildInfo Find(string name)
    {
        foreach (var child in ContentDocuments.Children(TreeUri, DocumentId))
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private IStorageItem ItemOf(ContentDocuments.ChildInfo child) => child.IsDirectory ? FolderOf(child) : FileOf(child);

    private StorageFolder FolderOf(ContentDocuments.ChildInfo child) =>
        StorageFolder.FromImplementation(new ContentDocumentFolder(TreeUri, child.DocumentId, child.Name));

    private StorageFile FileOf(ContentDocuments.ChildInfo child) =>
        StorageFile.FromImplementation(new ContentDocumentFile(
            ADocumentsContract.BuildDocumentUriUsingTree(TreeUri, child.DocumentId), child.Name, child.MimeType, createdForSave: false));
}

/// <summary>The content-resolver work behind <see cref="ContentDocumentFile"/> and <see cref="ContentDocumentFolder"/>.</summary>
internal static class ContentDocuments
{
    private static readonly List<WriteBack> _writeBacks = new();
    private static int _next;

    /// <summary>The application's content resolver.</summary>
    internal static AContentResolver Resolver => AndroidContext.Current.ContentResolver;

    /// <summary>A document's name, MIME type, size and last change (what the provider reports).</summary>
    internal sealed record DocumentInfo(string Name, string MimeType, long Size, DateTimeOffset Modified);

    /// <summary>One child of a tree directory.</summary>
    internal sealed record ChildInfo(string DocumentId, string Name, string MimeType)
    {
        /// <summary>True for a directory.</summary>
        internal bool IsDirectory => MimeType == ADocument.MimeTypeDir;
    }

    /// <summary>The MIME type of a file name's extension (application/octet-stream when unknown).</summary>
    internal static string MimeTypeOf(string name)
    {
        var extension = System.IO.Path.GetExtension(name ?? string.Empty).TrimStart('.').ToLowerInvariant();
        var mime = string.IsNullOrEmpty(extension) ? null : global::Android.Webkit.MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension);
        return string.IsNullOrEmpty(mime) ? "application/octet-stream" : mime;
    }

    /// <summary>What the provider reports about a document (unknown columns stay empty).</summary>
    internal static DocumentInfo Query(AUri uri)
    {
        string name = null;
        string mime = null;
        long size = -1;
        var modified = DateTimeOffset.MinValue;
        var resolver = Resolver;
        try
        {
            mime = resolver.GetType(uri);
            using var cursor = resolver.Query(uri, null, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                var nameIndex = cursor.GetColumnIndex(AOpenableColumns.DisplayName);
                if (nameIndex >= 0 && !cursor.IsNull(nameIndex))
                {
                    name = cursor.GetString(nameIndex);
                }

                var sizeIndex = cursor.GetColumnIndex(AOpenableColumns.Size);
                if (sizeIndex >= 0 && !cursor.IsNull(sizeIndex))
                {
                    size = cursor.GetLong(sizeIndex);
                }

                var modifiedIndex = cursor.GetColumnIndex(ADocument.ColumnLastModified);
                if (modifiedIndex >= 0 && !cursor.IsNull(modifiedIndex))
                {
                    modified = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(modifiedIndex));
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ServiceLog.Warn($"The document {uri} could not be queried: {exception.Message}");
        }

        return new DocumentInfo(name ?? uri.LastPathSegment, mime, size, modified);
    }

    /// <summary>The children of a tree directory.</summary>
    internal static List<ChildInfo> Children(AUri treeUri, string documentId)
    {
        var children = new List<ChildInfo>();
        var uri = ADocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, documentId);
        var columns = new[] { ADocument.ColumnDocumentId, ADocument.ColumnDisplayName, ADocument.ColumnMimeType };
        using var cursor = ContentDocuments.Resolver.Query(uri, columns, null, null, null);
        while (cursor != null && cursor.MoveToNext())
        {
            children.Add(new ChildInfo(cursor.GetString(0), cursor.GetString(1), cursor.GetString(2)));
        }

        return children;
    }

    /// <summary>Opens a document (read, or read-write keeping its content) as a .NET stream over its file descriptor.</summary>
    internal static Stream OpenStream(AUri uri, bool write)
    {
        var resolver = Resolver;
        var descriptor = resolver.OpenFileDescriptor(uri, write ? "rw" : "r");
        if (descriptor != null)
        {
            var handle = new SafeFileHandle(new IntPtr(descriptor.DetachFd()), ownsHandle: true);
            descriptor.Dispose();
            return new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read);
        }

        if (write)
        {
            return resolver.OpenOutputStream(uri, "wt") ?? throw new IOException($"The document {uri} cannot be written.");
        }

        // A provider without file descriptors: a seekable in-memory copy of the stream it hands out.
        using var input = resolver.OpenInputStream(uri) ?? throw new IOException($"The document {uri} cannot be read.");
        var memory = new MemoryStream();
        input.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    /// <summary>Deletes a document.</summary>
    internal static void Delete(AUri uri)
    {
        if (!ADocumentsContract.DeleteDocument(ContentDocuments.Resolver, uri))
        {
            throw new IOException($"The document provider did not delete {uri}.");
        }
    }

    /// <summary>The compatibility path of an opened document: a copy of it in the cache (made on first use).</summary>
    internal static string CopyToCache(AUri uri, string displayName)
    {
        var path = NewLocalPath("codebrix-picked", displayName);
        using (var input = ContentDocuments.Resolver.OpenInputStream(uri) ?? throw new IOException($"The document {uri} cannot be read."))
        using (var output = File.Create(path))
        {
            input.CopyTo(output);
        }

        return path;
    }

    /// <summary>
    /// The compatibility path of a created document: an empty cache file copied back to the document every time it
    /// is closed after writing (made on first use).
    /// </summary>
    internal static string CreateWriteBackFile(AUri uri, string displayName)
    {
        var path = NewLocalPath("codebrix-saved", displayName);
        File.WriteAllBytes(path, Array.Empty<byte>());
        var writeBack = new WriteBack(path, uri);
        lock (_writeBacks)
        {
            _writeBacks.Add(writeBack);
        }

        writeBack.StartWatching();
        return path;
    }

    private static string NewLocalPath(string area, string displayName)
    {
        var folder = System.IO.Path.Combine(AndroidContext.Current.CacheDir.AbsolutePath, area, Interlocked.Increment(ref _next).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return System.IO.Path.Combine(folder, PickerRequests.SafeFileName(displayName, "document"));
    }

    private sealed class WriteBack : AFileObserver
    {
        private readonly string _path;
        private readonly AUri _uri;

        internal WriteBack(string path, AUri uri)
            : base(new global::Java.IO.File(path), AFileObserverEvents.CloseWrite)
        {
            _path = path;
            _uri = uri;
        }

        public override void OnEvent(AFileObserverEvents e, string path)
        {
            try
            {
                using var output = ContentDocuments.Resolver.OpenOutputStream(_uri, "wt")
                    ?? throw new IOException($"The document {_uri} cannot be written.");
                using var input = File.OpenRead(_path);
                input.CopyTo(output);
            }
            catch (Exception exception)
            {
                ServiceLog.Warn($"Saving {_path} to {_uri} failed: {exception.Message}");
            }
        }
    }
}
