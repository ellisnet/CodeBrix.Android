using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Android.UI.TextLayout.Portable;
using CodeBrix.Platform.Foundation.Contracts;
using CodeBrix.Platform.UI;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using AAssetManager = global::Android.Content.Res.AssetManager;

namespace CodeBrix.Android.UI.TextLayout.Android;

/// <summary>
/// The Android font source of the text engine (IFontSourcePlatform&lt;SKTypeface&gt;, pin 1.0.268.12, WPE1-2 C5): the
/// typefaces TextLayout.Core's engine (and PlotterView.Core) shape and draw with, loaded from the APK assets as SkiaSharp
/// typefaces. The CodeBrix.Android font rule, the same one FontAndroidPlatform applies to the native text views
/// (<see cref="FontFallbackPolicy"/>): a family that names a font file loads it (a <c>.ttf.manifest</c> beside it
/// chooses the face by weight, style and stretch); a family NAME ("Segoe UI", Core's default) is the app's
/// DefaultTextFontFamily file; only an app that configured no default font file gets SkiaSharp's default typeface,
/// with one warning. Process-wide cache: the same request returns the same task and the same typeface to every caller.
/// </summary>
internal sealed class FontSourceAndroidPlatform : IFontSourcePlatform<SKTypeface>
{
    private readonly ConcurrentDictionary<FontRequest, Task<SKTypeface>> _cache = new();
    private readonly ConcurrentDictionary<string, FontManifest> _manifests = new(StringComparer.Ordinal);
    private readonly Func<AAssetManager> _assets;
    private readonly ILogger _log;
    private int _warnedNoDefault;

    /// <summary>Creates the font source.</summary>
    /// <param name="assets">The application's asset manager.</param>
    /// <param name="log">Where load failures and the no-default-font warning go.</param>
    internal FontSourceAndroidPlatform(Func<AAssetManager> assets, ILogger log)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _log = log;
    }

    /// <summary>How many typefaces were loaded from the assets (diagnostics).</summary>
    internal int LoadedCount => _cache.Count;

    /// <inheritdoc />
    public string DefaultTextFontFamily => FeatureConfiguration.Font.DefaultTextFontFamily;

    /// <inheritdoc />
    public string SymbolsFont => FeatureConfiguration.Font.SymbolsFont;

    /// <inheritdoc />
    public bool RestrictToEmbeddedFonts => FeatureConfiguration.Font.RestrictToEmbeddedFonts;

    /// <inheritdoc />
    public IReadOnlyList<string> FallbackFontFamilies => FeatureConfiguration.Font.FallbackFontFamilies;

    /// <inheritdoc />
    public Task<SKTypeface> GetTypefaceAsync(string familyName, ushort weight, int stretch, int style)
    {
        var request = FontRequest.Create(familyName, DefaultTextFontFamily, weight, stretch, style);
        return _cache.GetOrAdd(request, r => Task.FromResult(Load(r)));
    }

    /// <inheritdoc />
    public SKTypeface GetLoadedEmbeddedDefaultTypeface(ushort weight, int stretch, int style)
    {
        var request = FontRequest.Create(DefaultTextFontFamily, DefaultTextFontFamily, weight, stretch, style);
        return _cache.GetOrAdd(request, r => Task.FromResult(Load(r))) is { IsCompletedSuccessfully: true } task ? task.Result : null;
    }

    private SKTypeface Load(FontRequest request)
    {
        SKTypeface loaded = null;
        var resolution = FontFallbackPolicy.Resolve(request.Family, request.DefaultFamily, assetPath =>
        {
            loaded = TryLoadAsset(assetPath, request);
            return loaded != null;
        });

        if (resolution.Source != FontResolutionSource.AndroidDefault && loaded != null)
        {
            return loaded;
        }

        if (Interlocked.Exchange(ref _warnedNoDefault, 1) == 0)
        {
            _log?.LogWarning(
                "No default text font file is configured (FeatureConfiguration.Font.DefaultTextFontFamily = '{Default}'), so the text engine draws font family '{Family}' with SkiaSharp's default typeface. " +
                "CodeBrix apps never fall back to a system font: set DefaultTextFontFamily to an ms-appx:/// font file (for example a CodeBrix font package).",
                request.DefaultFamily, request.Family);
        }

        return SKTypeface.FromFamilyName(null, new SKFontStyle(request.Weight, request.Stretch, request.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright));
    }

    private SKTypeface TryLoadAsset(string assetPath, FontRequest request)
    {
        var assets = _assets();
        if (assets == null)
        {
            return null;
        }

        var faceAssetPath = assetPath;
        if (GetManifest(assets, assetPath)?.Select(request.Weight, request.Italic, request.Stretch) is { } face
            && AppAssetPath.TryGetAssetPath(face.Source, out var facePath))
        {
            faceAssetPath = facePath;
        }

        try
        {
            using var stream = assets.Open(faceAssetPath);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            using var data = SKData.CreateCopy(memory.ToArray());
            return SKTypeface.FromData(data);
        }
        catch (Exception exception) when (exception is Java.IO.IOException or IOException)
        {
            _log?.LogWarning(exception, "Could not load the font asset '{Asset}' for the text engine.", faceAssetPath);
            return null;
        }
    }

    private FontManifest GetManifest(AAssetManager assets, string fontAssetPath)
    {
        var manifestPath = FontFamilySource.GetManifestPath(fontAssetPath);
        return _manifests.GetOrAdd(manifestPath, path =>
        {
            try
            {
                using var stream = assets.Open(path);
                using var reader = new StreamReader(stream);
                return FontManifest.Parse(reader.ReadToEnd());
            }
            catch (Exception exception) when (exception is Java.IO.IOException or IOException)
            {
                return null;
            }
            catch (FormatException exception)
            {
                _log?.LogWarning(exception, "The font manifest '{Manifest}' is invalid.", path);
                return null;
            }
        });
    }
}
