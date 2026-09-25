using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Platform.UI;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using AAssetManager = global::Android.Content.Res.AssetManager;
using ATypeface = global::Android.Graphics.Typeface;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="IFontPlatform"/>, and the typeface resolver the
/// text measure uses. A FontFamily entry that names a font file (<c>ms-appx:///...</c> or an
/// app-relative path) is loaded from the APK assets (the CodeBrix.Android build adds library
/// fonts under their ms-appx path); when a CodeBrix font package ships a <c>.ttf.manifest</c>
/// beside the font, the face is chosen from it by weight, style and stretch. A family NAME
/// that is not a font file (for example Core's default "Segoe UI") resolves to the app's
/// default text font file (FeatureConfiguration.Font.DefaultTextFontFamily): the CodeBrix
/// rule is never to fall back to a system font (<see cref="FontFallbackPolicy"/>). The same
/// typeface is used for the text measure and the native text views, so measure and draw
/// agree. Only an app that configured no default font file gets Android's default typeface,
/// with one warning. Results are cached by (family, default family, weight, style, stretch).
/// </summary>
internal sealed class FontAndroidPlatform : IFontPlatform
{
    private readonly ConcurrentDictionary<(string Source, string Default, int Weight, bool Italic, int Stretch), ResolvedTypeface> _cache = new();
    private readonly ConcurrentDictionary<string, FontManifest> _manifests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _loggedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string> _defaultFamily;
    private int _warnedNoDefault;
    private readonly Func<AAssetManager> _assets;
    private readonly ILogger _log;

    internal FontAndroidPlatform(Func<AAssetManager> assets, ILogger log, Func<string> defaultFamily = null)
    {
        _assets = assets;
        _log = log;
        _defaultFamily = defaultFamily ?? (() => FeatureConfiguration.Font.DefaultTextFontFamily);
    }

    /// <inheritdoc />
    public Task<bool> PreloadAsync(FontFamily family, FontWeight weight, FontStretch stretch, FontStyle style)
    {
        var resolved = Resolve(family?.Source, weight.Weight, style != FontStyle.Normal, (int)stretch);
        return Task.FromResult(resolved.FromAsset);
    }

    /// <summary>Resolves the typeface for a FontFamily source and font attributes.</summary>
    internal ATypeface Resolve(FontFamily family, FontWeight weight, FontStyle style, FontStretch stretch) =>
        Resolve(family?.Source, weight.Weight, style != FontStyle.Normal, (int)stretch).Typeface;

    private ResolvedTypeface Resolve(string source, int weight, bool italic, int stretch)
    {
        weight = weight <= 0 ? 400 : Math.Clamp(weight, 1, 1000);
        var key = (source ?? string.Empty, _defaultFamily() ?? string.Empty, weight, italic, stretch);
        return _cache.GetOrAdd(key, k => Load(k.Source, k.Default, k.Weight, k.Italic, k.Stretch));
    }

    private ResolvedTypeface Load(string source, string defaultFamily, int weight, bool italic, int stretch)
    {
        ATypeface loaded = null;
        var resolution = FontFallbackPolicy.Resolve(source, defaultFamily, assetPath =>
        {
            loaded = TryLoadAsset(assetPath, weight, italic, stretch);
            return loaded != null;
        });

        switch (resolution.Source)
        {
            case FontResolutionSource.FamilyAsset:
                return new ResolvedTypeface(loaded, FromAsset: true);

            case FontResolutionSource.DefaultAsset:
                if (!string.IsNullOrWhiteSpace(source) && _loggedNames.TryAdd(source, true) && _log.IsEnabled(LogLevel.Debug))
                {
                    _log.LogDebug("Font family '{Source}' is not a font file; using the app's default text font '{Default}'.", source, resolution.AssetPath);
                }

                return new ResolvedTypeface(loaded, FromAsset: true);

            default:
                if (Interlocked.Exchange(ref _warnedNoDefault, 1) == 0)
                {
                    _log.LogWarning(
                        "No default text font file is configured (FeatureConfiguration.Font.DefaultTextFontFamily = '{Default}'), so font family '{Source}' uses the Android default typeface. " +
                        "CodeBrix apps never fall back to a system font: set DefaultTextFontFamily to an ms-appx:/// font file (for example a CodeBrix font package).",
                        defaultFamily, source);
                }

                return new ResolvedTypeface(ATypeface.Create(ATypeface.Default, weight, italic), FromAsset: false);
        }
    }

    private ATypeface TryLoadAsset(string assetPath, int weight, bool italic, int stretch)
    {
        var assets = _assets();
        if (assets == null)
        {
            return null;
        }

        var faceAssetPath = assetPath;
        var manifest = GetManifest(assets, assetPath);
        if (manifest?.Select(weight, italic, stretch) is { } face && AppAssetPath.TryGetAssetPath(face.Source, out var facePath))
        {
            faceAssetPath = facePath;
        }

        try
        {
            using var builder = new ATypeface.Builder(assets, faceAssetPath);
            return builder.SetWeight(weight).SetItalic(italic).Build();
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Could not load the font asset '{Asset}'.", faceAssetPath);
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
            catch (Java.IO.IOException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (FormatException e)
            {
                _log.LogWarning(e, "The font manifest '{Manifest}' is invalid.", path);
                return null;
            }
        });
    }

    private readonly record struct ResolvedTypeface(ATypeface Typeface, bool FromAsset);
}
