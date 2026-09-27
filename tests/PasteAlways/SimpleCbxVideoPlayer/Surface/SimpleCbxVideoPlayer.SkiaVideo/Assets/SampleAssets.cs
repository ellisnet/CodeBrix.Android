using System;
using System.IO;

namespace SimpleCbxVideoPlayer.SkiaVideo.Assets;

/// <summary>
/// Finds the video and colour-lookup-table corpus the application carries with it.
/// </summary>
/// <remarks>
/// The corpus is ordinary data that the Core project copies to the output folder, so it sits beside the
/// running executable under <c>Assets/</c> - no configuration file, no search of the file system, and the
/// same layout on every head and in the test run. A copy of the application that has lost its
/// <c>Assets</c> folder finds nothing, which is why the user interface says so rather than failing.
/// </remarks>
public static class SampleAssets
{
    /// <summary>The video corpus, relative to the folder the application runs from.</summary>
    public const string AuthoringRelativePath = "Assets/authoring";

    /// <summary>The colour-lookup-table corpus, relative to the folder the application runs from.</summary>
    public const string LutsRelativePath = "Assets/LUTs";

    /// <summary>Finds the corpus beside the running application.</summary>
    /// <returns>The folder the corpus sits in, or null when the application carries no corpus.</returns>
    public static string FindAssetsRoot() => FindAssetsRoot(AppContext.BaseDirectory);

    /// <summary>Finds the corpus beside a folder of your choosing.</summary>
    /// <param name="applicationFolder">The folder the corpus is expected to sit in.</param>
    /// <returns>The folder itself when it holds the corpus, and null when it does not.</returns>
    public static string FindAssetsRoot(string applicationFolder)
    {
        if (string.IsNullOrWhiteSpace(applicationFolder)) { return null; }

        return Directory.Exists(GetAuthoringFolder(applicationFolder)) ? applicationFolder : null;
    }

    /// <summary>Gives the video-corpus folder inside an application folder.</summary>
    /// <param name="applicationFolder">The folder the application runs from.</param>
    /// <returns>The full path of the authoring folder, whether or not it exists.</returns>
    public static string GetAuthoringFolder(string applicationFolder) =>
        Combine(applicationFolder, AuthoringRelativePath);

    /// <summary>Gives the lookup-table folder inside an application folder.</summary>
    /// <param name="applicationFolder">The folder the application runs from.</param>
    /// <returns>The full path of the LUTs folder, whether or not it exists.</returns>
    public static string GetLutsFolder(string applicationFolder) => Combine(applicationFolder, LutsRelativePath);

    private static string Combine(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(root)) { throw new ArgumentException("An application folder is required.", nameof(root)); }

        var parts = relativePath.Split('/');
        var path = root;

        foreach (var part in parts) { path = Path.Combine(path, part); }

        return path;
    }
}
