using System;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// Maps CodeBrix.Platform application URIs to Android asset paths. The CodeBrix.Android
/// build adds every app and library asset to the APK under its ms-appx path, so
/// <c>ms-appx:///Assets/Logo.png</c> is the Android asset <c>Assets/Logo.png</c>.
/// </summary>
internal static class AppAssetPath
{
    /// <summary>The application-package URI scheme prefix.</summary>
    internal const string AppxPrefix = "ms-appx:///";

    /// <summary>
    /// Returns the Android asset path for an <c>ms-appx:///</c> URI or an app-relative path
    /// (<c>/Assets/x.ttf</c>, <c>Assets/x.ttf</c>). A <c>#fragment</c> (font family name) is
    /// removed and percent-escapes are decoded. Returns false for other schemes and for
    /// values that are not paths.
    /// </summary>
    internal static bool TryGetAssetPath(string uriOrPath, out string assetPath)
    {
        assetPath = null;
        if (string.IsNullOrWhiteSpace(uriOrPath))
        {
            return false;
        }

        var value = uriOrPath.Trim();
        var hash = value.IndexOf('#');
        if (hash >= 0)
        {
            value = value.Substring(0, hash);
        }

        if (value.StartsWith(AppxPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(AppxPrefix.Length);
        }
        else if (value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }
        else if (!value.Contains('/') && !value.Contains('\\'))
        {
            // A bare name ("Segoe UI") is not a path.
            return false;
        }

        value = Uri.UnescapeDataString(value.Replace('\\', '/')).TrimStart('/');
        if (value.Length == 0)
        {
            return false;
        }

        assetPath = value;
        return true;
    }
}
