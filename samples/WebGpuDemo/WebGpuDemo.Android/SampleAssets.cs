using System;
using System.IO;
using AApplication = Android.App.Application;

namespace WebGpuDemo;

/// <summary>
/// The Models page loads its glTF files by path, so the APK's Assets/models tree is copied to the app's files folder
/// (once per installed build: the folder is named after the package's last update time).
/// </summary>
public static class SampleAssets
{
    private static readonly object Gate = new();
    private static string _folder;

    /// <summary>Copies the models out of the APK if needed and returns the folder that holds models/.</summary>
    /// <returns>The folder.</returns>
    public static string EnsureCopied()
    {
        lock (Gate)
        {
            if (_folder != null)
            {
                return _folder;
            }

            var context = AApplication.Context;
            var stamp = context.PackageManager.GetPackageInfo(context.PackageName, 0).LastUpdateTime;
            var folder = Path.Combine(context.FilesDir.AbsolutePath, "Assets-" + stamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!Directory.Exists(Path.Combine(folder, "models")))
            {
                // A Content file is an APK asset at its ms-appx path: ms-appx:///Assets/models/... -> Assets/models/...
                CopyTree(context.Assets, "Assets/models", Path.Combine(folder, "models"));
            }

            _folder = folder;
            return folder;
        }
    }

    private static void CopyTree(Android.Content.Res.AssetManager assets, string assetPath, string target)
    {
        var children = assets.List(assetPath) ?? Array.Empty<string>();
        if (children.Length == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using var input = assets.Open(assetPath);
            using var output = File.Create(target);
            input.CopyTo(output);
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var child in children)
        {
            CopyTree(assets, assetPath + "/" + child, Path.Combine(target, child));
        }
    }
}
