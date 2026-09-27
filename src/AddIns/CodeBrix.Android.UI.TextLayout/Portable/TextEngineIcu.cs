namespace CodeBrix.Android.UI.TextLayout.Portable;

/// <summary>
/// Where the text engine inside CodeBrix.Platform.UI.TextLayout.Core keeps its ICU state, and what a usable state is.
/// The engine loads the device's ICU on its first layout (on Android API 31+ the NDK-stable libicu.so of the i18n
/// module) and remembers the ICU version whose versioned exports (<c>u_getVersion_NN</c>) it found; bidi resolution and
/// line breaking go through it. These names are private members of the Core's engine (link-compiled framework source),
/// read only by the on-device diagnostics (<c>TextEngineProbe</c>); the host-free tests pin them against the pinned
/// Core's metadata, so a Core that renames them fails a unit test instead of a silent device report.
/// </summary>
internal static class TextEngineIcu
{
    /// <summary>The engine's text type (namespace-qualified), which holds the ICU binding as a nested type.</summary>
    internal const string EngineTypeName = "Microsoft.UI.Xaml.Documents.UnicodeText";

    /// <summary>The nested type of <see cref="EngineTypeName"/> that loads and binds ICU.</summary>
    internal const string IcuTypeName = "ICU";

    /// <summary>The static field holding the ICU version the engine bound to (0 until ICU is loaded).</summary>
    internal const string VersionFieldName = "_icuVersion";

    /// <summary>The static field holding the native handle of the loaded ICU common library.</summary>
    internal const string LibraryFieldName = "_libicuuc";

    /// <summary>The lowest ICU version the engine binds to (its MinSupportedIcuucVersion).</summary>
    internal const int MinimumVersion = 50;

    /// <summary>The highest ICU version the engine binds to (its MaxSupportedIcuucVersion).</summary>
    internal const int MaximumVersion = 100;

    /// <summary>Whether a version the engine reports means ICU was found and bound.</summary>
    /// <param name="version">The engine's ICU version (0 = not loaded).</param>
    /// <returns>True when the version is in the range the engine binds.</returns>
    internal static bool IsBound(int version) => version >= MinimumVersion && version <= MaximumVersion;
}
