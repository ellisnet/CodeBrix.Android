using CodeBrix.Android.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class LanguageTagsTests
{
    [Fact]
    public void Normalize_converts_underscores_and_keeps_the_preference_order()
    {
        //Act
        var tags = LanguageTags.Normalize(new[] { "fr_CA", "en-US", "de" });

        //Assert
        tags.Should().Equal("fr-CA", "en-US", "de");
    }

    [Fact]
    public void Normalize_drops_duplicates_empty_and_undetermined_tags()
    {
        //Act
        var tags = LanguageTags.Normalize(new[] { "en-US", "", null, "und", "EN_us", "es" });

        //Assert
        tags.Should().Equal("en-US", "es");
    }

    [Fact]
    public void Normalize_falls_back_when_nothing_usable_is_left()
    {
        //Act
        var tags = LanguageTags.Normalize(new[] { "und" });

        //Assert
        tags.Should().Equal(LanguageTags.Fallback);
    }
}
