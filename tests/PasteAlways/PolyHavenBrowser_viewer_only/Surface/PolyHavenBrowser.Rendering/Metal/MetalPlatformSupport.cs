using System.Runtime.InteropServices;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// Declares which platforms the app's Metal rendering backend (<see cref="MetalSceneRenderer"/>)
/// may be used on. Like <see cref="VulkanPlatformSupport"/> this is a deliberate, <b>hardcoded
/// allow-list</b> - not a runtime driver probe - so Metal is only ever attempted where the app
/// has okayed it. It is the mirror image of the Vulkan gate: Metal is okayed on <b>macOS only</b>
/// (Metal is an Apple API), and on <b>every other head</b> it is excluded, exactly as Vulkan is
/// excluded on macOS.
/// <para>
/// A second condition applies: the process architecture must be one the raw <c>objc_msgSend</c>
/// interop in <see cref="MetalInterop"/> supports - Apple Silicon (arm64) or Intel/Rosetta
/// (x86-64). Both Mac architectures are supported; anything else degrades cleanly to unsupported.
/// Head detection is shared with <see cref="VulkanPlatformSupport"/> (the loaded head runtime
/// assembly is head-generic, not Vulkan-specific).
/// </para>
/// </summary>
public static class MetalPlatformSupport
{
    /// <summary>Whether Metal rendering is okayed for the given head on the current process architecture.</summary>
    public static bool IsSupported(PlatformHead head) =>
        head == PlatformHead.MacOS && IsSupportedProcessArchitecture;

    /// <summary>Whether Metal rendering is okayed for the platform the app is running on.</summary>
    public static bool IsCurrentPlatformSupported => IsSupported(CurrentHead);

    /// <summary>The platform head the app is running on (shared with <see cref="VulkanPlatformSupport"/>).</summary>
    public static PlatformHead CurrentHead => VulkanPlatformSupport.CurrentHead;

    /// <summary>
    /// Whether the process architecture is one the raw <c>objc_msgSend</c> interop supports:
    /// arm64 (Apple Silicon) or x64 (Intel Macs, and x64 processes translated by Rosetta 2).
    /// Keyed off the <b>process</b> architecture - which is what the Objective-C calling
    /// convention actually depends on - so a Rosetta-translated x64 process is correctly treated
    /// as x64.
    /// </summary>
    public static bool IsSupportedProcessArchitecture =>
        RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.X64;
}
