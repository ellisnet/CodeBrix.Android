namespace PolyHavenBrowser.Rendering;

/// <summary>
/// The CodeBrix.Platform head the application is running on, as detected from the loaded
/// head runtime assembly. Each application head loads exactly one of these runtimes.
/// </summary>
public enum PlatformHead
{
    /// <summary>The head runtime could not be identified (e.g. a unit-test host).</summary>
    Unknown,

    /// <summary>The Linux X11 head (CodeBrix.Platform.UI.Runtime.Skia.X11).</summary>
    LinuxX11,

    /// <summary>The native Linux Wayland head (CodeBrix.Platform.UI.Runtime.Skia.Wayland).</summary>
    LinuxWayland,

    /// <summary>The Linux frame-buffer head (CodeBrix.Platform.UI.Runtime.Skia.Linux.FrameBuffer).</summary>
    LinuxFrameBuffer,

    /// <summary>The macOS head (CodeBrix.Platform.UI.Runtime.Skia.MacOS).</summary>
    MacOS,

    /// <summary>The Windows Win32 Skia head (CodeBrix.Platform.UI.Runtime.Skia.Win32).</summary>
    Win32Skia,

    /// <summary>The Windows WPF Skia head (CodeBrix.Platform.UI.Runtime.Skia.Wpf).</summary>
    WinWpfSkia,
}

/// <summary>
/// Declares which platforms the app's Vulkan rendering backend
/// (<see cref="VulkanSceneRenderer"/>) may be used on. This is a deliberate, hardcoded
/// allow-list - not a runtime driver probe - so Vulkan is only ever attempted on platforms
/// the app has okayed it for: the Linux X11 and Wayland heads and the two Windows heads.
/// macOS (no system Vulkan loader) and the Linux frame-buffer head are excluded, and an
/// unrecognized host is conservatively treated as unsupported.
/// </summary>
public static class VulkanPlatformSupport
{
    // Head runtime assembly names (exactly one is loaded per app), checked as prefixes so
    // satellite assemblies such as CodeBrix.Platform.UI.Runtime.Skia.Win32.Support match
    // their head too. Longer names are listed before their prefixes (FrameBuffer before
    // X11/Wayland ordering doesn't matter, but Wpf/Win32 are distinct).
    private const string HeadAssemblyPrefix = "CodeBrix.Platform.UI.Runtime.Skia.";

    private static readonly Lazy<PlatformHead> DetectedHead = new(DetectCurrentHead);

    /// <summary>Whether Vulkan rendering is okayed for the given platform head.</summary>
    public static bool IsSupported(PlatformHead head) => head switch
    {
        PlatformHead.LinuxX11 => true,
        PlatformHead.LinuxWayland => true,
        PlatformHead.Win32Skia => true,
        PlatformHead.WinWpfSkia => true,
        _ => false,
    };

    /// <summary>Whether Vulkan rendering is okayed for the platform the app is running on.</summary>
    public static bool IsCurrentPlatformSupported => IsSupported(CurrentHead);

    /// <summary>The platform head the app is running on (detected once, then cached).</summary>
    public static PlatformHead CurrentHead => DetectedHead.Value;

    /// <summary>
    /// Classifies a CodeBrix.Platform head runtime assembly name (e.g.
    /// <c>CodeBrix.Platform.UI.Runtime.Skia.X11</c>), returning
    /// <see cref="PlatformHead.Unknown"/> for anything that is not a head runtime.
    /// </summary>
    public static PlatformHead ClassifyAssemblyName(string? assemblyName)
    {
        if (assemblyName is null || !assemblyName.StartsWith(HeadAssemblyPrefix, StringComparison.Ordinal))
        {
            return PlatformHead.Unknown;
        }

        var head = assemblyName[HeadAssemblyPrefix.Length..];
        if (head == "X11") { return PlatformHead.LinuxX11; }
        if (head == "Wayland") { return PlatformHead.LinuxWayland; }
        if (head == "Linux.FrameBuffer") { return PlatformHead.LinuxFrameBuffer; }
        if (head == "MacOS") { return PlatformHead.MacOS; }
        if (head == "Wpf") { return PlatformHead.WinWpfSkia; }
        if (head == "Win32" || head.StartsWith("Win32.", StringComparison.Ordinal))
        {
            return PlatformHead.Win32Skia;
        }

        return PlatformHead.Unknown;
    }

    // Each head's Program.cs loads exactly one head runtime assembly (via
    // CodeBrixPlatformHostBuilder.Use*), so by the time any UI runs, scanning the loaded
    // assemblies identifies the head without this library referencing any of them.
    private static PlatformHead DetectCurrentHead()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var head = ClassifyAssemblyName(assembly.GetName().Name);
            if (head != PlatformHead.Unknown)
            {
                return head;
            }
        }

        return PlatformHead.Unknown;
    }
}
