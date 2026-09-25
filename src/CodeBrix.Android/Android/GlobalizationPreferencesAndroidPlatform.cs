using System.Collections.Generic;
using CodeBrix.Android.Portable;
using CodeBrix.Platform.Contracts;
using ALocaleList = global::Android.OS.LocaleList;

namespace CodeBrix.Android.Android;

/// <summary>
/// The Android implementation of <see cref="IGlobalizationPreferencesPlatform"/>: the
/// user's preferred languages are the device locale list (LocaleList.Default, which
/// follows the per-app language setting of API 33 when the user picked one), as BCP-47
/// tags, most preferred first.
/// </summary>
internal sealed class GlobalizationPreferencesAndroidPlatform : IGlobalizationPreferencesPlatform
{
    /// <inheritdoc />
    public IReadOnlyList<string> Languages
    {
        get
        {
            var tags = new List<string>();
            var locales = ALocaleList.Default;
            for (var i = 0; i < locales.Size(); i++)
            {
                tags.Add(locales.Get(i)?.ToLanguageTag());
            }

            return LanguageTags.Normalize(tags);
        }
    }
}
