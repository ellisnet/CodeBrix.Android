#if __ANDROID__
using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AAppCompatDelegate = global::AndroidX.AppCompat.App.AppCompatDelegate;
using AComponentCallbacks = global::Android.Content.IComponentCallbacks;
using AConfiguration = global::Android.Content.Res.Configuration;
using AContext = global::Android.Content.Context;
using AContextThemeWrapper = global::Android.Views.ContextThemeWrapper;
using ADynamicColors = Google.Android.Material.Color.DynamicColors;
using AMaterialColors = Google.Android.Material.Color.MaterialColors;
using AUiMode = global::Android.Content.Res.UiMode;
using WColor = Windows.UI.Color;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// The theme bridge (plan 2.11, D-P14, D-O4): Material 3 colour roles - the baseline Material palette, or the
/// wallpaper's dynamic palette (API 31+) when dynamic colour is on - are written into the curated Fluent keys
/// (<see cref="MaterialRoleMap"/>) of the framework's Light and Dark theme dictionaries, so an app that sets no
/// key looks Material; keys an app sets are its own brushes and always win. The app's RequestedTheme reaches
/// the native layer as the AppCompat night mode (Material dialogs and widget defaults follow it) without the
/// activity being recreated (CodeBrixActivity handles uiMode changes). Everything the bridge writes is recorded
/// and put back when the palette is switched to <c>Fluent</c>.
/// </summary>
internal static class ThemeBridge
{
    private static readonly Dictionary<(ResourceDictionary, string), object> _originals = new();
    private static readonly Dictionary<(ResourceDictionary, string), SolidColorBrush> _ownBrushes = new();
    private static readonly Dictionary<string, int> _lightRoles = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> _darkRoles = new(StringComparer.Ordinal);
    private static ConfigurationCallbacks _callbacks;
    private static bool _materialPalette = true;
    private static bool _dynamicColor = true;
    private static bool _themeHooked;
    private static bool _reresolving;

    /// <summary>
    /// True (default) to write the Material roles into the curated Fluent keys; false puts the framework's
    /// Fluent colours back (the UIReqs harness runs the copied, Fluent-specified scenarios so).
    /// </summary>
    internal static bool MaterialPalette
    {
        get => _materialPalette;
        set
        {
            if (_materialPalette != value)
            {
                _materialPalette = value;
                Apply();
            }
        }
    }

    /// <summary>True (default) to take the roles from the wallpaper's dynamic palette where the device offers one.</summary>
    internal static bool DynamicColor
    {
        get => _dynamicColor;
        set
        {
            if (_dynamicColor != value)
            {
                _dynamicColor = value;
                Apply();
            }
        }
    }

    /// <summary>True when the device offers dynamic colour (API 31+ and a supporting system).</summary>
    internal static bool IsDynamicColorAvailable => ADynamicColors.IsDynamicColorAvailable;

    /// <summary>True when the roles in use come from the dynamic palette.</summary>
    internal static bool UsesDynamicColor => _dynamicColor && IsDynamicColorAvailable;

    /// <summary>How many framework brushes the last <see cref="Apply"/> re-coloured or gave a brush of its own (diagnostics).</summary>
    internal static int WrittenCount { get; private set; }

    /// <summary>The curated keys the last <see cref="Apply"/> did not find in the framework dictionaries (diagnostics).</summary>
    internal static IReadOnlyList<string> MissingKeys { get; private set; } = Array.Empty<string>();

    /// <summary>The role colours (ARGB) of the light theme in use.</summary>
    internal static IReadOnlyDictionary<string, int> LightRoles => _lightRoles;

    /// <summary>The role colours (ARGB) of the dark theme in use.</summary>
    internal static IReadOnlyDictionary<string, int> DarkRoles => _darkRoles;

    /// <summary>Installs the bridge (idempotent; UI thread): applies the palette and follows configuration and theme changes.</summary>
    internal static void EnsureInstalled()
    {
        if (_callbacks == null && global::Android.App.Application.Context is { } context)
        {
            _callbacks = new ConfigurationCallbacks();
            context.RegisterComponentCallbacks(_callbacks);
        }

        HookRequestedTheme();
        Apply();
        ApplyNightMode();
    }

    /// <summary>
    /// Writes (or, with the Fluent palette, takes back) the Material roles: every curated key of the framework's
    /// Light and Dark theme dictionaries gets a brush of its own with the role's colour (the framework's brushes
    /// are never re-coloured: they are shared between the theme dictionaries and between aliases), then Core
    /// re-resolves every ThemeResource reference (the path a theme change takes) and the native handlers re-map.
    /// </summary>
    internal static void Apply()
    {
        try
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
            {
                return;
            }

            var dictionaries = new List<(ResourceDictionary Dictionary, bool Dark)>(FrameworkThemeDictionaries());
            if (!_materialPalette)
            {
                if (_ownBrushes.Count > 0)
                {
                    Restore();
                    ReresolveThemeResources();
                }

                return;
            }

            // Phase 1: record the framework's own entries before anything is written (a lazily created alias must
            // never see a Material colour).
            foreach (var entry in MaterialRoleMap.Entries)
            {
                foreach (var (dictionary, _) in dictionaries)
                {
                    Snapshot(dictionary, entry.Key);
                }
            }

            var context = global::Android.App.Application.Context;
            ReadRoles(context, dark: false, _lightRoles);
            ReadRoles(context, dark: true, _darkRoles);

            // Phase 2: a brush of its own per key and theme.
            var missing = new List<string>();
            var written = 0;
            foreach (var entry in MaterialRoleMap.Entries)
            {
                var found = false;
                foreach (var (dictionary, dark) in dictionaries)
                {
                    if (!dictionary.TryGetValue(entry.Key, out _) || MaterialRoleMap.ColorOf(entry, dark ? _darkRoles : _lightRoles) is not { } argb)
                    {
                        continue;
                    }

                    if (!_ownBrushes.TryGetValue((dictionary, entry.Key), out var own))
                    {
                        own = new SolidColorBrush();
                        _ownBrushes[(dictionary, entry.Key)] = own;
                    }

                    own.Color = ToColor(argb);
                    if (!dictionary.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, own))
                    {
                        dictionary[entry.Key] = own;
                    }

                    found = true;
                    written++;
                }

                if (!found)
                {
                    missing.Add(entry.Key);
                }
            }

            WrittenCount = written;
            MissingKeys = missing;
            ReresolveThemeResources();
            HostLog.For("CodeBrix.Android.UI.Policy").LogInformation(
                "Theme bridge: Material roles ({Palette}) written into {Written} framework theme entries; {Missing} curated key(s) not found.",
                UsesDynamicColor ? "dynamic" : "baseline", written, missing.Count);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogError(exception, "The theme bridge failed to apply the Material roles.");
        }
        finally
        {
            ThemeRefresh.RequestRefresh();
        }
    }

    /// <summary>Puts every framework entry the bridge replaced back (the framework's Fluent brushes).</summary>
    internal static void Restore()
    {
        foreach (var ((dictionary, key), original) in _originals)
        {
            if (!_ownBrushes.ContainsKey((dictionary, key)))
            {
                continue;
            }

            // Put back exactly what the dictionary held itself; a key it only reached through a merged dictionary
            // (or an alias resolved for the active theme) is removed again, so the lookup is the framework's once more.
            if (original != null)
            {
                dictionary[key] = original;
            }
            else
            {
                dictionary.Remove(key);
            }
        }

        _ownBrushes.Clear();
        WrittenCount = 0;
    }

    /// <summary>True when <paramref name="brush"/> is a brush the bridge put into a framework theme dictionary.</summary>
    /// <param name="brush">A brush.</param>
    /// <returns>True for a bridge brush.</returns>
    internal static bool IsBridgeBrush(Brush brush)
    {
        foreach (var own in _ownBrushes.Values)
        {
            if (ReferenceEquals(own, brush))
            {
                return true;
            }
        }

        return false;
    }

    private static void Snapshot(ResourceDictionary dictionary, string key)
    {
        if (_originals.ContainsKey((dictionary, key)))
        {
            return;
        }

        // The dictionary's OWN entry (enumerated, not looked up: a lookup resolves aliases for the active theme and
        // searches the merged dictionaries). Null = the key is not one of its own entries.
        object own = null;
        foreach (var pair in dictionary)
        {
            if (pair.Key is string text && string.Equals(text, key, StringComparison.Ordinal))
            {
                own = pair.Value;
                break;
            }
        }

        _originals[(dictionary, key)] = own;
    }

    /// <summary>Makes Core re-resolve every ThemeResource reference in every window (what a theme change does).</summary>
    private static void ReresolveThemeResources()
    {
        try
        {
            _reresolving = true;
            Application.Current?.OnRequestedThemeChanged();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Re-resolving the theme resources failed.");
        }
        finally
        {
            _reresolving = false;
        }
    }

    /// <summary>
    /// The AppCompat night mode for the app's theme: an app that set Application.RequestedTheme gets that theme
    /// natively too; otherwise the system's setting is followed.
    /// </summary>
    internal static void ApplyNightMode()
    {
        try
        {
            var application = Application.Current;
            var mode = application != null && application.IsThemeSetExplicitly
                ? (application.RequestedTheme == ApplicationTheme.Dark ? AAppCompatDelegate.ModeNightYes : AAppCompatDelegate.ModeNightNo)
                : AAppCompatDelegate.ModeNightFollowSystem;
            if (AAppCompatDelegate.DefaultNightMode != mode)
            {
                AAppCompatDelegate.DefaultNightMode = mode;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Setting the night mode failed.");
        }
    }

    /// <summary>The ARGB colour of a Material theme attribute in a light or dark (and dynamic, when on) Material 3 context.</summary>
    /// <param name="role">The attribute name (e.g. "colorPrimary").</param>
    /// <param name="dark">True for the dark theme.</param>
    /// <param name="dynamic">True for the dynamic palette.</param>
    /// <returns>The colour, or null.</returns>
    internal static int? ReadRole(string role, bool dark, bool dynamic)
    {
        var context = RoleContext(global::Android.App.Application.Context, dark, dynamic);
        var attr = MaterialWidgets.AttrId(context, role);
        if (context == null || attr == 0)
        {
            return null;
        }

        return AMaterialColors.GetColor(context, attr, 0x00000000);
    }

    private static void ReadRoles(AContext context, bool dark, Dictionary<string, int> roles)
    {
        roles.Clear();
        var themed = RoleContext(context, dark, UsesDynamicColor);
        if (themed == null)
        {
            return;
        }

        foreach (var role in MaterialRoleMap.Roles())
        {
            var attr = MaterialWidgets.AttrId(themed, role);
            if (attr != 0)
            {
                roles[role] = AMaterialColors.GetColor(themed, attr, 0x00000000);
            }
        }
    }

    private static AContext RoleContext(AContext context, bool dark, bool dynamic)
    {
        if (context?.Resources?.Configuration is not { } current)
        {
            return null;
        }

        var configuration = new AConfiguration(current)
        {
            UiMode = (current.UiMode & ~AUiMode.NightMask) | (dark ? AUiMode.NightYes : AUiMode.NightNo),
        };
        var configured = context.CreateConfigurationContext(configuration);
        var style = MaterialWidgets.StyleId(configured, "Theme.Material3.DayNight.NoActionBar");
        AContext themed = style != 0 ? new AContextThemeWrapper(configured, style) : configured;
        return dynamic && IsDynamicColorAvailable ? ADynamicColors.WrapContextIfAvailable(themed) : themed;
    }

    /// <summary>The framework's theme dictionaries (XamlControlsResources' Light, Dark and Default) with their darkness.</summary>
    private static IEnumerable<(ResourceDictionary Dictionary, bool Dark)> FrameworkThemeDictionaries()
    {
        var seen = new HashSet<ResourceDictionary>();
        foreach (var framework in FrameworkDictionaries(Application.Current.Resources, 0))
        {
            // WinUI's theme dictionary keys: "Light", and "Default" (or "Dark") for the dark theme.
            foreach (var (name, dark) in new[] { ("Light", false), ("Dark", true), ("Default", true) })
            {
                if (framework.ThemeDictionaries.TryGetValue(name, out var value) && value is ResourceDictionary theme && seen.Add(theme))
                {
                    yield return (theme, dark);
                }
            }
        }
    }

    private static IEnumerable<ResourceDictionary> FrameworkDictionaries(ResourceDictionary dictionary, int depth)
    {
        if (dictionary == null || depth > 8)
        {
            yield break;
        }

        if (ThemeRefresh.IsFrameworkDictionary(dictionary))
        {
            yield return dictionary;
            yield break;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            foreach (var found in FrameworkDictionaries(merged, depth + 1))
            {
                yield return found;
            }
        }
    }

    private static WColor ToColor(int argb) => WColor.FromArgb((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    private static void HookRequestedTheme()
    {
        if (_themeHooked || Application.Current is not { } application)
        {
            return;
        }

        _themeHooked = true;
        application.RequestedThemeChanged += () =>
        {
            if (!_reresolving)
            {
                ApplyNightMode();
            }
        };
    }

    private sealed class ConfigurationCallbacks : global::Java.Lang.Object, AComponentCallbacks
    {
        private AUiMode _lastNight = (AUiMode)(-1);

        public void OnConfigurationChanged(AConfiguration newConfig)
        {
            // A day/night flip (or a wallpaper change that re-derived the dynamic palette) re-reads the roles.
            var night = newConfig.UiMode & AUiMode.NightMask;
            if (night != _lastNight)
            {
                _lastNight = night;
                Apply();
            }
        }

        public void OnLowMemory()
        {
        }
    }
}
#endif
