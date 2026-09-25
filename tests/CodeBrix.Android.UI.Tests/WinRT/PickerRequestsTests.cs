using System.Collections.Generic;
using CodeBrix.Android.Services;
using SilverAssertions;
using Windows.Storage.Pickers;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class PickerRequestsTests
{
    private static string Map(string extension) => extension switch
    {
        "pdf" => "application/pdf",
        "png" => "image/png",
        "jpg" => "image/jpeg",
        "jpeg" => "image/jpeg",
        _ => null,
    };

    [Theory]
    [InlineData(PickerLocationId.PicturesLibrary, "image/*")]
    [InlineData(PickerLocationId.VideosLibrary, "video/*")]
    [InlineData(PickerLocationId.DocumentsLibrary, "*/*")]
    public void The_start_location_chooses_the_broad_type(PickerLocationId location, string expected)
    {
        //Act & Assert
        PickerRequests.BaseMimeType(location).Should().Be(expected);
    }

    [Fact]
    public void Known_extensions_become_distinct_mime_types()
    {
        //Act
        var types = PickerRequests.FilterMimeTypes(new[] { ".pdf", ".jpg", "jpeg", ".PNG" }, Map);

        //Assert
        types.Should().Equal("application/pdf", "image/jpeg", "image/png");
    }

    [Fact]
    public void A_star_or_an_unknown_extension_means_no_restriction()
    {
        //Act & Assert
        PickerRequests.FilterMimeTypes(new[] { ".pdf", "*" }, Map).Should().BeNull();
        PickerRequests.FilterMimeTypes(new[] { ".pdf", ".cbv" }, Map).Should().BeNull();
        PickerRequests.FilterMimeTypes(new List<string>(), Map).Should().BeNull();
    }

    [Fact]
    public void The_default_extension_is_the_save_extension()
    {
        //Act & Assert
        PickerRequests.SaveExtension("webm", null).Should().Be(".webm");
    }

    [Fact]
    public void Without_a_default_the_first_choice_extension_is_used()
    {
        //Arrange
        var choices = new Dictionary<string, IList<string>> { ["PDF document"] = new List<string> { ".pdf" }, ["Text"] = new List<string> { ".txt" } };

        //Act & Assert
        PickerRequests.SaveExtension(null, choices).Should().Be(".pdf");
        PickerRequests.SaveExtension(null, null).Should().BeEmpty();
    }

    [Fact]
    public void The_save_title_gets_the_extension_once()
    {
        //Act & Assert
        PickerRequests.SaveTitle("report", ".pdf").Should().Be("report.pdf");
        PickerRequests.SaveTitle("report.PDF", ".pdf").Should().Be("report.PDF");
        PickerRequests.SaveTitle(null, ".pdf").Should().Be("Untitled.pdf");
    }

    [Fact]
    public void A_display_name_becomes_a_safe_file_name()
    {
        //Act & Assert
        PickerRequests.SafeFileName("a/b\\c:d.pdf", "document").Should().Be("a_b_c_d.pdf");
        PickerRequests.SafeFileName("  ", "document").Should().Be("document");
        PickerRequests.SafeFileName("..", "document").Should().Be("document");
    }
}
