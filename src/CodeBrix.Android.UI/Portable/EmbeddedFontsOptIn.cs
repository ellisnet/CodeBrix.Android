using CodeBrix.Platform.UI;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// Applies the application's embedded-fonts opt-in (CodeBrixApplication.RestrictToEmbeddedFonts) to the
/// CodeBrix.Platform flag <c>FeatureConfiguration.Font.RestrictToEmbeddedFonts</c> - what the desktop heads'
/// RestrictToEmbeddedFonts() host-builder call sets. Not a latch: the flag stays an ordinary setting the app may also
/// set (or clear) directly; this only turns it on when the application asked.
/// </summary>
internal static class EmbeddedFontsOptIn
{
    /// <summary>Sets the flag when the application asked for it; leaves it as it is otherwise.</summary>
    /// <param name="requested">The application's RestrictToEmbeddedFonts.</param>
    /// <returns>Whether font resolution is restricted to embedded fonts afterwards.</returns>
    internal static bool Apply(bool requested)
    {
        if (requested)
        {
            FeatureConfiguration.Font.RestrictToEmbeddedFonts = true;
        }

        return FeatureConfiguration.Font.RestrictToEmbeddedFonts;
    }
}
