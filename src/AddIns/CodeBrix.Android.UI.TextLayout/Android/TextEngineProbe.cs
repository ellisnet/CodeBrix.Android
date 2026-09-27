using System;
using System.Reflection;
using CodeBrix.Android.UI.TextLayout.Portable;
using CodeBrix.Platform.UI.TextLayout;

namespace CodeBrix.Android.UI.TextLayout.Android;

/// <summary>
/// What the text engine of TextLayout.Core does on this device (diagnostics and the UIReqs fences): it lays text out
/// through the Core's public engine (TextLayoutEngine, the same call an app makes) and reads back the ICU state the
/// engine bound on its first layout. The Android add-in supplies only the font source; everything measured here is the
/// Core engine's own work (HarfBuzz shaping, ICU bidi and line breaking) on the device.
/// </summary>
internal static class TextEngineProbe
{
    /// <summary>The ICU state of the engine after a first layout.</summary>
    /// <param name="Version">The ICU version the engine bound (0 = ICU not loaded).</param>
    /// <param name="LibraryLoaded">Whether the engine holds a native ICU library handle.</param>
    internal readonly record struct IcuState(int Version, bool LibraryLoaded)
    {
        /// <summary>Whether ICU was found and bound.</summary>
        internal bool IsBound => TextEngineIcu.IsBound(Version) && LibraryLoaded;
    }

    /// <summary>Lays one run out with the engine (the app's default text font unless a family is given).</summary>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The em size.</param>
    /// <param name="maxWidth">The wrapping width (null = no wrapping).</param>
    /// <param name="family">The font family source (null = the app's default text font).</param>
    /// <returns>The line count, the base direction and the measured size.</returns>
    internal static (int Lines, bool RightToLeft, float Width, float Height) LayOut(string text, float fontSize, float? maxWidth, string family = null)
    {
        AndroidPlatformBootstrap.EnsureRegistered();
        var fontFamily = family ?? AndroidPlatformBootstrap.FontSource?.DefaultTextFontFamily ?? string.Empty;
        var options = new TextLayoutOptions();
        if (maxWidth is { } width)
        {
            options.MaxWidth = width;
        }

        using var layout = TextLayoutEngine.Layout([new TextRunDescriptor(text, fontFamily, fontSize)], options);
        return (layout.LineCount, layout.IsBaseDirectionRightToLeft, layout.Size.Width, layout.Size.Height);
    }

    /// <summary>Reads the ICU state the engine holds (call after a layout: the engine loads ICU on first use).</summary>
    /// <returns>The state; version 0 when the engine's ICU type cannot be found.</returns>
    internal static IcuState ReadIcuState()
    {
        var engine = typeof(TextLayoutEngine).Assembly.GetType(TextEngineIcu.EngineTypeName, throwOnError: false);
        var icu = engine?.GetNestedType(TextEngineIcu.IcuTypeName, BindingFlags.NonPublic | BindingFlags.Public);
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var version = icu?.GetField(TextEngineIcu.VersionFieldName, Static)?.GetValue(null) is int v ? v : 0;
        var library = icu?.GetField(TextEngineIcu.LibraryFieldName, Static)?.GetValue(null) is IntPtr handle ? handle : IntPtr.Zero;
        return new IcuState(version, library != IntPtr.Zero);
    }
}
