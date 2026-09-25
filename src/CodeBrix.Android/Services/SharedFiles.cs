using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Android.Android;
using Windows.Storage.Streams;
using AUri = global::Android.Net.Uri;

namespace CodeBrix.Android.Services;

/// <summary>
/// Files the app hands to other apps (a bitmap on the clipboard, a file shared through the share sheet): they are
/// written under the cache folder "codebrix-shared" and exposed as content URIs by the CodeBrix FileProvider
/// (declared by CodeBrix.Android.UI: authority "&lt;application id&gt;.codebrix.fileprovider").
/// </summary>
internal static class SharedFiles
{
    private static int _next;

    /// <summary>The folder shared files are written to.</summary>
    internal static string Directory => Path.Combine(AndroidContext.Current.CacheDir!.AbsolutePath, "codebrix-shared");

    /// <summary>Writes <paramref name="bytes"/> to a new shared file and returns its content URI (null when there is no bridge).</summary>
    /// <param name="bytes">The file content.</param>
    /// <param name="fileName">The file name to use (made unique).</param>
    internal static AUri Share(byte[] bytes, string fileName)
    {
        if (AndroidActivityBridge.Current is not { } bridge)
        {
            return null;
        }

        var folder = Path.Combine(Directory, System.Threading.Interlocked.Increment(ref _next).ToString(System.Globalization.CultureInfo.InvariantCulture));
        System.IO.Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, string.IsNullOrWhiteSpace(fileName) ? "shared" : Path.GetFileName(fileName));
        File.WriteAllBytes(path, bytes);
        return bridge.GetShareableUri(path);
    }

    /// <summary>Reads a stream reference fully.</summary>
    internal static async Task<byte[]> ReadAllAsync(RandomAccessStreamReference reference)
    {
        if (reference == null)
        {
            return Array.Empty<byte>();
        }

        using var stream = await reference.OpenReadAsync();
        using var source = stream.AsStreamForRead();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
