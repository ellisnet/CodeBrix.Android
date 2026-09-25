using CodeBrix.Platform.Foundation.Contracts;
using CodeBrix.Platform.Foundation.Extensibility;
using SkiaSharp;

namespace CodeBrix.Android.UI.TextLayout.Android;

/// <summary>
/// What the text engine gets when it asks for its font source (diagnostics and the UIReqs fences): the contract is
/// resolved the way TextLayout.Core resolves it (ApiExtensibility), not through this assembly's own field.
/// </summary>
internal static class FontSourceProbe
{
    /// <summary>The type name of the registered IFontSourcePlatform&lt;SKTypeface&gt;, or null when none is registered.</summary>
    internal static string RegisteredSourceType()
    {
        AndroidPlatformBootstrap.EnsureRegistered();
        return ApiExtensibility.CreateInstance<IFontSourcePlatform<SKTypeface>>(null)?.GetType().Name;
    }

    /// <summary>The family name of the typeface the registered source gives a FontFamily source (normal face).</summary>
    internal static string FamilyNameOf(string familySource)
    {
        AndroidPlatformBootstrap.EnsureRegistered();
        var source = ApiExtensibility.CreateInstance<IFontSourcePlatform<SKTypeface>>(null);
        var typeface = source?.GetTypefaceAsync(familySource, 400, 5, 0).GetAwaiter().GetResult();
        return typeface?.FamilyName;
    }

    /// <summary>The family name of the app's default text font, as the registered source loads it.</summary>
    internal static string DefaultFamilyName()
    {
        AndroidPlatformBootstrap.EnsureRegistered();
        var source = ApiExtensibility.CreateInstance<IFontSourcePlatform<SKTypeface>>(null);
        return source?.GetLoadedEmbeddedDefaultTypeface(400, 5, 0)?.FamilyName;
    }
}
