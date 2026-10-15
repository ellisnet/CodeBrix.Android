using System;
using Microsoft.UI.Xaml;

namespace WebGpuDemo;

/// <summary>
/// The pages WebGpuDemo can start on. The launch intent's <c>page</c> extra selects one:
/// <c>adb shell am start -n com.codebrix.webgpudemo/com.codebrix.webgpudemo.MainActivity --es page models</c>.
/// </summary>
public static class StartPages
{
    /// <summary>The Triangle page (the default).</summary>
    public const string Triangle = "triangle";

    /// <summary>The Models page.</summary>
    public const string Models = "models";

    /// <summary>The Renderer class page.</summary>
    public const string Renderer = "renderer";

    /// <summary>The Controls page.</summary>
    public const string Controls = "controls";

    /// <summary>The start page the launch intent asked for (set by MainActivity before the XAML application starts).</summary>
    public static string Requested { get; set; } = Triangle;

    /// <summary>Returns the main page's page for a key (unknown keys give the Triangle page).</summary>
    /// <param name="main">The main page.</param>
    /// <param name="key">The key.</param>
    /// <returns>The page to show.</returns>
    public static UIElement Resolve(Views.MainPage main, string key) => key?.ToLowerInvariant() switch
    {
        Models => main.Models,
        Renderer => main.Renderer,
        Controls => main.Controls,
        _ => main.Triangle,
    };
}
