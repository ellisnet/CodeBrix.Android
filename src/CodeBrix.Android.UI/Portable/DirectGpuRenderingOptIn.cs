using CodeBrix.Platform.UI.Hosting;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// Applies the application's direct GPU rendering opt-in (CodeBrixApplication.UseDirectGpuRenderingMode) to the
/// CodeBrix.Platform latch, <see cref="DirectGpuRenderingMode"/> - the same one-way, app-wide switch the desktop heads'
/// UseDirectGpuRenderingMode() host-builder call turns on (through the Core assembly's friend grant to this assembly).
/// </summary>
internal static class DirectGpuRenderingOptIn
{
    /// <summary>
    /// Turns the latch on when the application asked for it; never turns it off (there is deliberately no way to).
    /// </summary>
    /// <param name="requested">The application's UseDirectGpuRenderingMode.</param>
    /// <returns>Whether direct GPU rendering is enabled afterwards.</returns>
    internal static bool Apply(bool requested)
    {
        if (requested)
        {
            DirectGpuRenderingMode.Enable();
        }

        return DirectGpuRenderingMode.IsEnabled;
    }
}
