using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable;
using Microsoft.Extensions.Logging;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// Loads the CodeBrix.Android add-in assemblies an app ships and runs their module initializers,
/// once, right after the platform bootstrap (the WPC1 "twin loading by name" pattern, done by the
/// framework at start-up): an add-in registers its Core contracts and its element handlers from
/// its module initializer, and several of those contracts are resolved by Core through
/// ApiExtensibility only (the Skia canvas factory, ISvgProvider), so nothing would otherwise load
/// the add-in before its first element is created. An add-in the app does not ship is skipped.
/// </summary>
internal static class AddInLoader
{
    private static readonly object _gate = new();
    private static bool _loaded;

    /// <summary>Loads every add-in the app ships (idempotent; the first call does the work).</summary>
    internal static void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
        }

        var log = HostLog.For("CodeBrix.Android.UI.AddIns");
        foreach (var name in AddInAssemblyNames.All)
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(new AssemblyName(name));
            }
            catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                continue;
            }

            RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
            log.LogDebug("Add-in {AddIn} loaded.", name);
        }
    }
}
