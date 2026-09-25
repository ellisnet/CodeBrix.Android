#if __ANDROID__
using AFileProvider = global::AndroidX.Core.Content.FileProvider;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// The CodeBrix FileProvider: content:// URIs for the files the WinRT services hand to other apps (a bitmap on
/// the clipboard, an image in the share sheet), all under the cache folder "codebrix-shared"
/// (Resources/xml/codebrix_shared_paths.xml). Declared in every CodeBrix app's manifest through these
/// attributes (authority "&lt;application id&gt;.codebrix.fileprovider"; the MAUI Essentials technique).
/// </summary>
[global::Android.Content.ContentProvider(
    new[] { "${applicationId}.codebrix.fileprovider" },
    Name = "codebrix.android.ui.SharedFileProvider",
    Exported = false,
    GrantUriPermissions = true)]
[global::Android.App.MetaData("android.support.FILE_PROVIDER_PATHS", Resource = "@xml/codebrix_shared_paths")]
internal sealed class SharedFileProvider : AFileProvider
{
    /// <summary>The provider authority of the running app.</summary>
    internal static string Authority => global::Android.App.Application.Context.PackageName + ".codebrix.fileprovider";

    /// <summary>The content URI of a file under the shared cache folder.</summary>
    internal static global::Android.Net.Uri UriFor(string filePath) =>
        GetUriForFile(global::Android.App.Application.Context, Authority, new global::Java.IO.File(filePath));
}
#endif
